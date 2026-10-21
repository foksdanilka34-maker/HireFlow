using HireFlow.Web.Api;

namespace HireFlow.Web.Tests;

public class ApiAddressTests
{
    // Относительный адрес достраивается от адреса кабинета (включая подпуть), в конце всегда «/»; абсолютный сохраняется.
    [Theory]
    [InlineData("http://localhost:8080/", "api/", "http://localhost:8080/api/")]
    [InlineData("http://localhost:8080/", "api", "http://localhost:8080/api/")]
    [InlineData("http://localhost:8080/", "http://localhost:5184", "http://localhost:5184/")]
    [InlineData("http://host/app/", "api/", "http://host/app/api/")]
    public void Resolve_BuildsApiAddressWithTrailingSlash(string host, string api, string expected)
    {
        Assert.Equal(new Uri(expected), ApiAddress.Resolve(host, api));
    }
}
