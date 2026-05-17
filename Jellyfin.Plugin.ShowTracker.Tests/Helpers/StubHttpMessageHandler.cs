using System.Net;
using System.Net.Http;

namespace Jellyfin.Plugin.ShowTracker.Tests.Helpers;

/// <summary>
/// HttpMessageHandler that returns a queued sequence of canned responses.
/// Records every request so tests can assert call counts and URLs.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = new();

    public int CallCount => Requests.Count;

    public void Enqueue(HttpStatusCode status, string? jsonBody = null)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = jsonBody == null
                ? new StringContent(string.Empty)
                : new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
        });
    }

    public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responses.Enqueue(responder);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"StubHttpMessageHandler received {request.Method} {request.RequestUri} but no response was queued.");
        }

        var responder = _responses.Dequeue();
        return Task.FromResult(responder(request));
    }
}
