using System.Security.Cryptography;
using System.Text;
using HireFlow.Domain.ApiClients;

namespace HireFlow.Application.PublicApi;

public static class ApiKeys
{
    private const string KeyPrefix = "hf_";

    // Создаёт новый ключ: 32 случайных байта в base64url без «=» с префиксом hf_.
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return KeyPrefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // Считает SHA-256 ключа в hex нижнего регистра — так ключ хранится и ищется в БД.
    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    // Возвращает первые символы ключа, по которым его можно узнать в списке.
    public static string Prefix(string key) =>
        key.Length <= ApiClient.KeyPrefixLength ? key : key[..ApiClient.KeyPrefixLength];
}
