using Microsoft.Extensions.Options;

namespace HireFlow.Api.Security;

public static class BotApiKeyExtensions
{
    // Требует служебный ключ бота (заголовок X-Api-Key) для маршрута или группы маршрутов.
    public static TBuilder RequireBotApiKey<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilterFactory((factoryContext, next) =>
        {
            var filter = new BotApiKeyFilter(factoryContext.ApplicationServices.GetRequiredService<IOptions<BotApiKeyOptions>>());
            return invocationContext => filter.InvokeAsync(invocationContext, next);
        });
}
