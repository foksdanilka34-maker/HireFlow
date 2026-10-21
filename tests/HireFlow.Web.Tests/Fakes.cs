using System.Net;
using System.Text;

namespace HireFlow.Web.Tests;

// Фейковый обработчик HTTP: возвращает заготовленный ответ или бросает исключение.
public class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    // Ответ с заданным кодом и JSON-телом (или без тела).
    public static FakeHandler Returns(HttpStatusCode status, string? json = null, string mediaType = "application/json") =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = json is null ? null : new StringContent(json, Encoding.UTF8, mediaType),
        });

    // Сетевая ошибка вместо ответа.
    public static FakeHandler NetworkFails() => Throws(new HttpRequestException("network down"));

    // Заданное исключение вместо ответа.
    public static FakeHandler Throws(Exception exception) => new(_ => throw exception);

    // Последний полученный запрос: метод, адрес и тело (null, если тела не было).
    public HttpMethod? LastMethod { get; private set; }
    public string? LastUrl { get; private set; }
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastMethod = request.Method;
        LastUrl = request.RequestUri?.ToString();
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return respond(request);
    }
}
