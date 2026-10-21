using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace HireFlow.Web.Api;

// Просит браузер отправлять cookie сессии в API на другом порту.
public class CookieHandler : DelegatingHandler
{
    // Включает отправку cookie для каждого запроса.
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}
