using System.Text.Json;
using System.Text.Json.Serialization;
using AuthMock.Domain;

namespace AuthMock.Infrastructure;

public static class SeedUsers
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static IReadOnlyList<User> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"User seed file not found: {path}", path);
        }

        List<User>? users;
        try
        {
            users = JsonSerializer.Deserialize<List<User>>(File.ReadAllText(path), _options);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"User seed file is invalid: {path}", exception);
        }

        if (users is null || users.Count == 0)
        {
            throw new InvalidOperationException($"User seed file is empty: {path}");
        }

        var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var user in users)
        {
            if (!usernames.Add(user.Username))
            {
                throw new InvalidOperationException(
                    $"Duplicate username in user seed file: {user.Username}");
            }

            if (!ids.Add(user.Id))
            {
                throw new InvalidOperationException($"Duplicate id in user seed file: {user.Id}");
            }
        }

        return users;
    }
}
