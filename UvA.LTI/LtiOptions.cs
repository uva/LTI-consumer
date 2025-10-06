using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace UvA.LTI;

public class LtiOptions
{
    /// <summary>
    /// Lti client ID
    /// </summary>
    public string ClientId { get; set; }
    
    /// <summary>
    /// Authentication endpoint url
    /// </summary>
    public string AuthenticateUrl { get; set; }
    
    /// <summary>
    /// Key set url
    /// </summary>
    public string JwksUrl { get; set; }
    
    /// <summary>
    /// JWT signing key, minimum 128-bit
    /// </summary>
    public string SigningKey { get; set; } = "";
    
    /// <summary>
    /// Endpoint that handles initiation requests. Default: oidc
    /// </summary>
    public string InitiationEndpoint { get; set; } = "oidc";
    
    /// <summary>
    /// Endpoint that handles sign in requests. Default: signin-oidc
    /// </summary>
    public string LoginEndpoint { get; set; } = "signin-oidc";

    /// <summary>
    /// Override url for redirect after login
    /// </summary>
    public string? RedirectUrl { get; set; }
    
    /// <summary>
    /// Token lifetime in minutes
    /// </summary>
    public int TokenLifetime { get; set; } = 120;

    /// <summary>
    /// User agent for HTTP requests
    /// </summary>
    public string UserAgent { get; set; } = "LTI-Client";

    /// <summary>
    /// Mapping of LTI properties to claims
    /// </summary>
    public Func<LtiPrincipal, Dictionary<string, object>> ClaimsMapping { get; set; }
        = p => new Dictionary<string, object>
        {
            [ClaimTypes.Email] = p.Email
        };

    /// <summary>
    /// Set up a redirect for certain requests
    /// </summary>
    public Func<IFormCollection, string?> RedirectFunction { get; set; } = _ => null;

    /// <summary>
    /// Override scheme and hostname for the login endpoint.
    /// The value of this property and the <see cref="LoginEndpoint"/> combined will be used to redirect the user and should match the Redirect URI registered in Canvas.
    /// </summary>
    /// <example>https://example.com</example>
    public string? LoginUrl { get; set; }
    
}