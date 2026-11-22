using System.Net;
using System.Text;

namespace NotionTodoWidget.Tests;

/// <summary>Records requests (with bodies) and replays queued responses.</summary>
public sealed class FakeHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> Bodies { get; } = new();
    public Queue<Func<HttpResponseMessage>> Responses { get; } = new();

    public void Enqueue(HttpStatusCode code, string json = "{}", int? retryAfter = null)
        => Responses.Enqueue(() =>
        {
            var r = new HttpResponseMessage(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            if (retryAfter is int s) r.Headers.TryAddWithoutValidation("Retry-After", s.ToString());
            return r;
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
        return Responses.Count > 0
            ? Responses.Dequeue()()
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
    }
}
