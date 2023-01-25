using System.Security.Claims;
using UvA.LTI;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseLti(new LtiOptions
{
    ClientId = "104400000000000213",
    AuthenticateUrl = "https://uvadlo-dev.test.instructure.com/api/lti/authorize_redirect",
    JwksUrl = "https://canvas.test.instructure.com/api/lti/security/jwks",
    SigningKey = "blawlaekltjwelkrjtwlkejlekwjrklwejr32423",
    ClaimsMapping = c => new Dictionary<string, object>
    {
        [ClaimTypes.NameIdentifier] = c.NameIdentifier,
        ["contextLabel"] = c.Context.Label
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();