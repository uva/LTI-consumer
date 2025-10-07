using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace UvA.LTI;

public class LtiMiddleware
{
    private readonly ILogger<LtiMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly LtiOptions _options;

    private SymmetricSecurityKey SigningKey => new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_options.SigningKey));

    public LtiMiddleware(LtiOptions options, ILogger<LtiMiddleware> logger, RequestDelegate next)
    {
        _logger = logger;
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider provider)
    {
        if (context.Request.Path == $"/{_options.InitiationEndpoint}" && await HandleInitiation(context))
            return;
        if (context.Request.Path == $"/{_options.LoginEndpoint}")
        {
            var claimsResolver = provider.GetService<ILtiClaimsResolver>();
            var redirectUrlResolver = provider.GetService<ILtiRedirectUrlResolver>();
            if (await HandleLogin(context, claimsResolver, redirectUrlResolver))
                return;
        }
        await _next(context);
    }

    private async Task<bool> HandleLogin(HttpContext context, ILtiClaimsResolver? claimsResolver, ILtiRedirectUrlResolver? redirectUrlResolver)
    {
        var handler = new JwtSecurityTokenHandler();
        var state = handler.ValidateToken(context.Request.Form["state"][0], new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidIssuer = "lti",
            IssuerSigningKey = SigningKey
        }, out _);

        if (state.FindFirstValue("clientId") != _options.ClientId)
            return false;

        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        var keyset = new JsonWebKeySet(await client.GetStringAsync(_options.JwksUrl));
        
        var id = handler.ValidateToken(context.Request.Form["id_token"][0], new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = false,
            ValidAudience = _options.ClientId,
            IssuerSigningKeys = keyset.Keys
        }, out _);

        if (state.FindFirstValue("nonce") != id.FindFirstValue("nonce"))
            return false;

        var target = state.FindFirstValue("target");
        if (target == null)
        {
            _logger.LogError("Redirect target missing");
            throw new Exception();
        }

        var jsonOptions = new JsonSerializerOptions {PropertyNamingPolicy = JsonNamingPolicy.CamelCase};

        var claimContext = id.FindFirstValue(LtiClaimTypes.Context);
        var claimCustom = id.FindFirstValue(LtiClaimTypes.Custom);
        var claimLis = id.FindFirstValue(LtiClaimTypes.Lis);

        var ltiPrincipal = new LtiPrincipal
        {
            Email = id.FindFirstValue(ClaimTypes.Email),
            NameIdentifier = id.FindFirstValue(ClaimTypes.NameIdentifier),
            Name = id.FindFirstValue(ClaimTypes.Name),
            Context = JsonSerializer.Deserialize<LtiContext>(claimContext, jsonOptions),
            Roles = id.FindAll(LtiClaimTypes.Roles).Select(c => c.Value).ToArray(),
            CustomClaims = claimCustom == null ? null : JsonDocument.Parse(claimCustom).RootElement,
            Lis = claimLis == null ? null : JsonSerializer.Deserialize<LtiLis>(claimLis, jsonOptions),
            Locale = id.FindFirstValue("locale"),
            CanvasPlacement = id.FindFirstValue(LtiClaimTypes.CanvasPlacement)
        };

        var claims = claimsResolver == null ? _options.ClaimsMapping(ltiPrincipal) :
                                              await claimsResolver.ResolveClaims(ltiPrincipal);

        var securityToken = handler.CreateToken(new SecurityTokenDescriptor
        {
            Expires = DateTime.UtcNow.AddMinutes(_options.TokenLifetime),
            Issuer = "lti",
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha512Signature),
            Claims = claims
        });

        var token = handler.WriteToken(securityToken);

        if (redirectUrlResolver != null)
        {
            var redirectUrl = await redirectUrlResolver.ResolveRedirectUrl(token, claims);
            if (redirectUrl != null)
            {
                context.Response.Redirect(redirectUrl);
                return true;
            }
        }
        
        context.Response.Redirect($"{_options.RedirectUrl ?? target}/#{token}");
        return true;
    }

    private async Task<bool> HandleInitiation(HttpContext context)
    {
        if (!context.Request.Form["target_link_uri"].Any())
        {
            _logger.LogError("Missing target link uri");
            return false;
        }

        if (context.Request.Form["client_id"][0] != _options.ClientId)
        {
            _logger.LogInformation($"Skipping request for {context.Request.Form["client_id"]} in {_options.ClientId}");
            return false;
        }

        var redirect = _options.RedirectFunction(context.Request.Form);
        if (redirect != null)
        {
            await GenerateForm(redirect, context.Request.Form.ToDictionary(f => f.Key, f => f.Value.First() ?? ""))
                .ExecuteAsync(context);
            return true;
        }

        var nonce = Guid.NewGuid().ToString();
        
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Expires = DateTime.UtcNow.AddMinutes(3),
            Issuer = "lti",
            Claims = new Dictionary<string, object>
            {
                ["nonce"] = nonce,
                ["target"] = context.Request.Form["target_link_uri"][0],
                ["clientId"] = _options.ClientId
            },
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha512Signature)
        });
        var state = handler.WriteToken(token);

        var redirectUri = !string.IsNullOrWhiteSpace(_options.LoginUrl)
            ? $"{_options.LoginUrl}/{_options.LoginEndpoint}"
            : $"{context.Request.Scheme}://{context.Request.Host}/{_options.LoginEndpoint}"; 
        
        var pars = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "id_token",
            ["response_mode"] = "form_post",
            ["redirect_uri"] = redirectUri, 
            ["login_hint"] = context.Request.Form["login_hint"],
            ["scope"] = "openid",
            ["state"] = state,
            ["nonce"] = nonce,
            ["prompt"] = "none",
            ["lti_message_hint"] = context.Request.Form["lti_message_hint"]
        };
        await GenerateForm(_options.AuthenticateUrl, pars).ExecuteAsync(context);
        return true;
    }

    static IResult GenerateForm(string targetUrl, IEnumerable<KeyValuePair<string, string>> pars)
        => Results.Content($@"<html>
<head>
    <title>Working...</title>
</head>
<body>
    <form method='POST' name='hiddenform' action='{targetUrl}'>
        {string.Join('\n', pars.Select(p => $"<input type='hidden' name={p.Key} value='{p.Value}' />"))}
        <noscript><p>Script is disabled. Click Submit to continue.</p><input type='submit' value='Submit' /></noscript>
    </form>
    <script language='javascript'>
        window.setTimeout(function() {{ document.forms[0].submit(); }}, 0);
    </script>
</body>
</html>", "text/html");
}

public static class LtiMiddlewareExtensions
{
    public static IApplicationBuilder UseLti(this IApplicationBuilder builder, 
        LtiOptions options) =>
        builder.UseMiddleware<LtiMiddleware>(options);
}