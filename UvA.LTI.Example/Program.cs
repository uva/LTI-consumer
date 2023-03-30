using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UvA.LTI;
using UvA.LTI.Example;

var key = "blawlaekltjwelkrjtwlkejlekwjrklwejr32423";
var signingKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key));

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddAuthorization()
    .AddAuthentication(opt => opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters.ValidateAudience = false;
        opt.TokenValidationParameters.ValidIssuer = "lti";
        opt.TokenValidationParameters.IssuerSigningKey = signingKey;
    });

//The mapping of claims can be defined in the ClaimsMapping delegate in options or
//by adding a ILtiClaimsResolver service to use DI when mapping the claims:
//builder.Services.AddScoped<ILtiClaimsResolver, ClaimsResolver>();

var app = builder.Build();

app.UseLti(new LtiOptions
{
    ClientId = "104400000000000213",
    AuthenticateUrl = "https://uvadlo-dev.test.instructure.com/api/lti/authorize_redirect",
    JwksUrl = "https://canvas.test.instructure.com/api/lti/security/jwks",
    SigningKey = key,
    ClaimsMapping = c => new Dictionary<string, object>
    {
        [ClaimTypes.NameIdentifier] = c.NameIdentifier,
        ["contextLabel"] = c.Context.Label
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("Test", r => r.Response.WriteAsJsonAsync(r.User.Claims.Select(c => new { c.Type, c.Value })))
    .RequireAuthorization();

app.Run();