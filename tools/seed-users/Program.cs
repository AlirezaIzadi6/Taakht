using System.Globalization;
using AuthMock.Domain;
using SeedUsers;

var outputPath = "src/mocks/auth/data/users.json";
var check = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--out" when i + 1 < args.Length:
            outputPath = args[++i];
            break;
        case "--check":
            check = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 1;
    }
}

var users = CreateUsers();
var json = UserSeedFile.Serialize(users);

if (check)
{
    var existing = File.Exists(outputPath) ? File.ReadAllText(outputPath) : null;
    if (existing != json)
    {
        Console.Error.WriteLine($"{outputPath} is stale, run make gen-users.");
        return 1;
    }

    Console.WriteLine($"{outputPath} is up to date.");
    return 0;
}

var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
if (directory is not null)
{
    Directory.CreateDirectory(directory);
}

File.WriteAllText(outputPath, json);
Console.WriteLine($"Wrote {users.Count.ToString(CultureInfo.InvariantCulture)} users to {outputPath}");
return 0;

static List<User> CreateUsers()
{
    var users = new List<User>
    {
        Named("ali", "ali@example.com", "+989120000001", UserRole.User),
        Named("sara", "sara@example.com", "+989120000002", UserRole.User),
        Named("reza", "reza@example.com", "+989120000003", UserRole.User),
        Named("mina", "mina@example.com", "+989120000004", UserRole.User),
        Named("hossein", "hossein@example.com", "+989120000005", UserRole.User),
        Named("zahra", "zahra@example.com", "+989120000006", UserRole.User),
        Named("amir", "amir@example.com", "+989120000007", UserRole.User),
        Named("neda", "neda@example.com", "+989120000008", UserRole.User),
        Named("kian", "kian@example.com", "+989120000009", UserRole.User),
        Named("admin", "admin@example.com", "+989120000010", UserRole.Admin),
    };

    for (var i = 1; i <= 2000; i++)
    {
        var suffix = i.ToString("0000", CultureInfo.InvariantCulture);
        users.Add(new User
        {
            Id = "u" + suffix,
            Username = "u" + suffix,
            Email = "u" + suffix + "@example.com",
            Phone = "+98912" + (1000000 + i).ToString(CultureInfo.InvariantCulture),
            Role = UserRole.User,
        });
    }

    return users;
}

static User Named(string username, string email, string phone, UserRole role) =>
    new()
    {
        Id = username,
        Username = username,
        Email = email,
        Phone = phone,
        Role = role,
    };
