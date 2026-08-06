using System.Net;

namespace OracleOfBatman.Graph.Tests.Fakes;

public sealed class FakeHttpMessageHandler(
  HttpStatusCode statusCode,
  string content,
  Action<HttpRequestMessage>? onRequest = null) : HttpMessageHandler
{
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    onRequest?.Invoke(request);
    var response = new HttpResponseMessage(statusCode) { Content = new StringContent(content) };
    return Task.FromResult(response);
  }
}
