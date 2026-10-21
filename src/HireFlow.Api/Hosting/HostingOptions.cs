namespace HireFlow.Api.Hosting;

// Настройки размещения API: работа за reverse proxy и хранение ключей Data Protection.
public class HostingOptions
{
    public const string SectionName = "Hosting";

    // Доверять X-Forwarded-For/X-Forwarded-Proto от любого прокси; включать только когда API наружу не публикуется.
    public bool TrustForwardedHeaders { get; set; }

    // Папка для ключей Data Protection; без значения ключи хранятся в профиле пользователя.
    public string? DataProtectionKeysPath { get; set; }
}
