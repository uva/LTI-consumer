namespace UvA.LTI;

public interface ILtiClaimsResolver
{
    public Task<Dictionary<string, object>> ResolveClaims(LtiPrincipal principal);
}
