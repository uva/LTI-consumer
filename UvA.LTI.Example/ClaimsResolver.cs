using System.Security.Claims;

namespace UvA.LTI.Example;

public class ClaimsResolver : ILtiClaimsResolver
{
    public Task<Dictionary<string, object>> ResolveClaims(LtiPrincipal principal)
    {
        var claims = new Dictionary<string, object>
        {
            [ClaimTypes.NameIdentifier] = principal.NameIdentifier,
            ["contextLabel"] = principal.Context.Label
        };

        return Task.FromResult(claims);
    }
}
