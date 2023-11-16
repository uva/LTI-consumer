using Microsoft.IdentityModel.Tokens;

namespace UvA.LTI;

public interface ILtiRedirectUrlResolver
{
    public Task<string?> ResolveRedirectUrl(string token, Dictionary<string, object> claims);
}
