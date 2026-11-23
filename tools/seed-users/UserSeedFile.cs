using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthMock.Domain;

namespace SeedUsers;

internal static class UserSeedFile
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(IEnumerable<User> users)
    {
        var lines = users.Select(user => JsonSerializer.Serialize(user, _options));
        return "[\n  " + string.Join(",\n  ", lines) + "\n]\n";
    }
}
