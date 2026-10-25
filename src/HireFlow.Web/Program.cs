using HireFlow.Web;
using HireFlow.Web.Api;
using HireFlow.Web.Auth;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not set in wwwroot/appsettings.json.");

// В WASM один пользователь на вкладку, поэтому сессия и клиент — singleton.
builder.Services.AddSingleton<AuthState>();
builder.Services.AddSingleton(_ => new HttpClient(new CookieHandler { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = ApiAddress.Resolve(builder.HostEnvironment.BaseAddress, apiBaseUrl),
});
builder.Services.AddSingleton<HireFlowApiClient>();

await builder.Build().RunAsync();
