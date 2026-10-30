using System.Text;
using System.Text.Json;

namespace Restaurant_Aqa_Framework.Support;

/// <summary>Reads a claim of a Cognito token without validating it — the backend already did; the suite only
/// needs to know whose token it is.</summary>
public static class JwtClaims
{
    public static string Subject(string token) => Read(token, "sub");

    private static string Read(string token, string claim)
    {
        var payload = token.Split(".")[1].Replace("-", "+", StringComparison.Ordinal).Replace("_", "/", StringComparison.Ordinal);
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return json.RootElement.GetProperty(claim).GetString()
            ?? throw new InvalidOperationException($"The token has no \"{claim}\" claim.");
    }
}
