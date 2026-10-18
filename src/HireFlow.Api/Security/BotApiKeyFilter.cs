using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace HireFlow.Api.Security;

public class BotApiKeyFilter(IOptions<BotApiKeyOptions> options) : IEndpointFilter
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(options.Value.ApiKey);

    // Пропускает запрос только с верным служебным ключом бота в заголовке X-Api-Key, иначе отвечает 401.
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var provided = context.HttpContext.Request.Headers[BotApiKeyOptions.HeaderName].ToString();
        var isValid = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), _expected);

        return isValid
            ? next(context)
            : ValueTask.FromResult<object?>(Results.Problem("Missing or invalid API key.", statusCode: StatusCodes.Status401Unauthorized));
    }
}
