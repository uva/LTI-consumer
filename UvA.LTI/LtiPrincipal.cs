using System.Text.Json;

namespace UvA.LTI;

public class LtiPrincipal
{
    public string? Email { get; set; }
    public string? NameIdentifier { get; set; }
    public LtiContext Context { get; set; }
    public string[] Roles { get; set; }
    public JsonElement? CustomClaims { get; set; }
    public string? Name { get; set; }
}