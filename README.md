# LTI 1.3 consumer middleware

This middleware provides a very basic LTI 1.3 consumer setup.
It handles the initiation and sign-in flow and generates a JWT for use in a client application.
After login, a redirect is done to `https://{redirect_url}/#{token}`. 

## Usage

Add the `UvA.LTI` NuGet package and set up the LTI middleware:

```csharp
app.UseLti(new LtiOptions 
{
    SigningKey = "sufficiently-long-key-for-signing-jwt-tokens",
    ClientId = "LTI client ID",
    AuthenticateUrl = "https://example.lms/lti/authenticate",
    JwksUrl = "https://example.lms/lti/.well-known/jwks"
});
```

## Options

| Property           | Description                                                                                                                  | Default       |
|--------------------|------------------------------------------------------------------------------------------------------------------------------|---------------|
| SigningKey         | A key used for signing JWT tokens passed to the client                                                                       | _required_    |
| ClientId           | Client ID as registered in the LMS                                                                                           | _required_    |
| AuthenticateUrl    | Authentication url of the LMS                                                                                                | _required_    |
| JwksUrl            | Url for LMS signing keys                                                                                                     | _required_    |
| InitiationEndpoint | Endpoint exposed for initiation flow                                                                                         | `oidc`        |
| LoginEndpoint      | Endpoint exposed for login flow                                                                                              | `signin-oidc` |
| RedirectUrl        | Override redirect url after login                                                                                            |               |
| TokenLifetime      | Generated JWT lifetime, in minutes                                                                                           | 120           |
| ClaimsMapping      | Mapping of LTI claims to JWT claims                                                                                          | Only email    |
| RedirectFunction   | Function that allows redirects to other tool versions to be set up <br/> based on parameters sent to the initiation endpoint | None          |
 

## Example

Some standard claims:
```cs
app.UseLti(new LtiOptions
{
    AuthenticateUrl = config["Endpoint"],
    ClientId = config["ClientId"],
    InitiationEndpoint = "oidc",
    LoginEndpoint = "signin-oidc",
    SigningKey = config["Jwt:Key"],
    JwksUrl = config["JwksUrl"],
    RedirectUrl = "", // always redirect to root 
    ClaimsMapping = p => new Dictionary<string, object>
    {
        // get course id from either the context or a custom claim
        ["courseId"] = int.TryParse(p.Context.Id, out _) ? p.Context.Id : p.CustomClaims?.GetProperty("courseid").ToString(),
        ["courseName"] = p.Context.Title,
        [ClaimTypes.Role] = p.Roles.Any(e => e.Contains("http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor"))
            ? "Teacher" : "Student", 
        [ClaimTypes.Email] = p.Email,
        [ClaimTypes.NameIdentifier] = p.NameIdentifier.Split("_").Last(),
    }
});
```
Alternatively the claims mapping can be handled by a service implementing the ILtiClaimsResolver interface instead of setting the ClaimsMapping delegate on LtiOptions.
```cs
builder.Services.AddScoped<ILtiClaimsResolver, ClaimsResolver>();
app.UseLti(new LtiOptions{...});
```
```cs
public class ClaimsResolver : ILtiClaimsResolver
{
    public Task<Dictionary<string, object>> ResolveClaims(LtiPrincipal principal)
    {
        var claims = new Dictionary<string, object>
        {
            // get course id from either the context or a custom claim
            ["courseId"] = int.TryParse(principal.Context.Id, out _) ? principal.Context.Id : principal.CustomClaims?.GetProperty("courseid").ToString(),
            ["courseName"] = principal.Context.Title,
            [ClaimTypes.Role] = principal.Roles.Any(e => e.Contains("http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor"))
            ? "Teacher" : "Student",
            [ClaimTypes.Email] = principal.Email,
            [ClaimTypes.NameIdentifier] = principal.NameIdentifier.Split("_").Last(),
        };

        return Task.FromResult(claims);
    }
}
```
The redirect url can be overriden in the options or by registering a service implementing the ILtiRedirectUrlResolver.
By using the redirect resolver it's possible to specify custom redirect url's for each user based on the supplied claims.
```cs
builder.Services.AddScoped<ILtiRedirectUrlResolver, RedirectUrlResolver>();
```
See also the example project, which shows a working example that can be used with Canvas (once an LTI developer key has been registered with redirect URI `https://localhost:5000/signin-oidc` and the included JSON config).
