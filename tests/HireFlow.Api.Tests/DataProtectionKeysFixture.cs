using HireFlow.Api.Tests.Infrastructure;

namespace HireFlow.Api.Tests;

// Фикстура с папкой ключей Data Protection во временной папке; основной клиент входит при старте и порождает ключ.
public class DataProtectionKeysFixture : ApiAppFixture
{
    public string KeysPath { get; } = Path.Combine(Path.GetTempPath(), "hireflow-tests-keys", Guid.NewGuid().ToString("N"));

    protected override IReadOnlyDictionary<string, string> ExtraSettings =>
        new Dictionary<string, string> { ["Hosting:DataProtectionKeysPath"] = KeysPath };

    public override async Task DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(KeysPath))
                Directory.Delete(KeysPath, recursive: true);

            // Общую родительскую папку убираем, только если она опустела.
            var parent = Path.GetDirectoryName(KeysPath)!;
            if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                Directory.Delete(parent);
        }
    }
}
