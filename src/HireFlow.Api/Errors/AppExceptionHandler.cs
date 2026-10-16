using HireFlow.Application.Common;
using HireFlow.Domain;
using Microsoft.AspNetCore.Diagnostics;

namespace HireFlow.Api.Errors;

public class AppExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    // Превращает известные исключения в ProblemDetails, неизвестные отдаёт стандартному обработчику.
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        int? status = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            BadRequestException => StatusCodes.Status400BadRequest,
            DomainException => StatusCodes.Status409Conflict,
            BadHttpRequestException badRequest => badRequest.StatusCode,
            _ => null,
        };

        if (status is null)
            return false;

        context.Response.StatusCode = status.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Status = status, Detail = exception.Message },
        });
    }
}
