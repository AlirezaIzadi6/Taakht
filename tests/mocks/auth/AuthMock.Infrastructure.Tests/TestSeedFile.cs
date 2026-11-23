namespace AuthMock.Infrastructure.Tests;

internal static class TestSeedFile
{
    public static string FilePath { get; } =
        Path.Combine(AppContext.BaseDirectory, "data", "users.json");
}
