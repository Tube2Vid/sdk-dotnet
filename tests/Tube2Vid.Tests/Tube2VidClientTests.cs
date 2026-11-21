using System.Net;
using System.Text;
using Xunit;

namespace Tube2Vid.Tests;

public class Tube2VidClientTests
{
    private sealed record Call(HttpMethod Method, string Url, string? Authorization, string? Body);

    private sealed class FakeHandler : HttpMessageHandler
    {
        public readonly Queue<HttpResponseMessage> Responses = new();
        public readonly List<Call> Calls = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Calls.Add(new Call(request.Method, request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString(), body));
            return Responses.Count > 0 ? Responses.Dequeue() : throw new InvalidOperationException($"unexpected request {request.RequestUri}");
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (Tube2VidClient, FakeHandler) Client(string apiKey = "tv_live_x")
    {
        var handler = new FakeHandler();
        var client = new Tube2VidClient(new Tube2VidClientOptions { ApiKey = apiKey, BaseUrl = "https://example.test/" }, new HttpClient(handler));
        return (client, handler);
    }

    [Fact]
    public async Task SendsApiKeyAndParsesApiErrors()
    {
        var (client, fake) = Client();
        fake.Responses.Enqueue(Json("""{"error":"Insufficient credits","code":"no_credits","creditBalance":0}""", HttpStatusCode.PaymentRequired));
        var e = await Assert.ThrowsAsync<Tube2VidApiException>(() =>
            client.SubmitJobAsync(new JobSubmitRequest("https://youtu.be/x") { Mode = JobMode.Audio }));
        Assert.Equal(HttpStatusCode.PaymentRequired, e.StatusCode);
        Assert.Equal("Insufficient credits", e.Message);
        Assert.Equal("no_credits", e.Code);
        Assert.Equal(0, e.JsonData!.Value.GetProperty("creditBalance").GetInt32());
        Assert.Equal("https://example.test/v1/jobs", fake.Calls[0].Url);
        Assert.Equal("Bearer tv_live_x", fake.Calls[0].Authorization);
        Assert.Equal("""{"url":"https://youtu.be/x","mode":"audio"}""", fake.Calls[0].Body);
    }

    [Fact]
    public async Task NonJsonErrorFallsBackToBody()
    {
        var (client, fake) = Client();
        fake.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("Bad Gateway") });
        var e = await Assert.ThrowsAsync<Tube2VidApiException>(() => client.GetUsageAsync());
        Assert.Equal("Bad Gateway", e.Message);
        Assert.Null(e.Code);
    }

    [Fact]
    public async Task WithoutKeySubmitJobUsesPublicRoute()
    {
        var (client, fake) = Client(apiKey: "");
        fake.Responses.Enqueue(Json("""{"jobId":"j","status":"queued","platform":"youtube","estimatedCredits":0}"""));
        var job = await client.SubmitJobAsync(new JobSubmitRequest("https://youtu.be/x"));
        Assert.Equal(JobState.Queued, job.Status);
        Assert.Equal("https://example.test/v1/public/jobs", fake.Calls[0].Url);
        Assert.Null(fake.Calls[0].Authorization);
    }

    [Fact]
    public async Task DownloadJobFollowsSignedRedirectWithoutApiKey()
    {
        var (client, fake) = Client();
        var redirect = new HttpResponseMessage(HttpStatusCode.Found);
        redirect.Headers.Location = new Uri("https://cdn.test/file.mp4?sig=1");
        fake.Responses.Enqueue(redirect);
        fake.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("bytes") });
        using var file = await client.DownloadJobAsync("job 1");
        Assert.Equal("bytes", await file.Content.ReadAsStringAsync());
        Assert.Equal("https://example.test/v1/jobs/job%201/download", fake.Calls[0].Url);
        Assert.Equal("Bearer tv_live_x", fake.Calls[0].Authorization);
        Assert.Equal("https://cdn.test/file.mp4?sig=1", fake.Calls[1].Url);
        Assert.Null(fake.Calls[1].Authorization);
    }

    [Fact]
    public async Task GetDownloadLinkResolvesRelativeLocation()
    {
        var (client, fake) = Client();
        var redirect = new HttpResponseMessage(HttpStatusCode.Found);
        redirect.Headers.TryAddWithoutValidation("Location", "/signed/file.mp4");
        fake.Responses.Enqueue(redirect);
        Assert.Equal("https://example.test/signed/file.mp4", await client.GetDownloadLinkAsync("j"));
    }

    [Fact]
    public async Task WaitForJobPollsUntilTerminalState()
    {
        var (client, fake) = Client();
        foreach (var s in new[] { "queued", "running", "completed" })
            fake.Responses.Enqueue(Json($$"""{"id":"j","status":"{{s}}","progress":0,"mode":"brand-new","newField":true}"""));
        var seen = new List<JobState>();
        var done = await client.WaitForJobAsync("j", TimeSpan.FromMilliseconds(1), onUpdate: j => seen.Add(j.Status));
        Assert.Equal(JobState.Completed, done.Status);
        Assert.Equal(JobMode.Unknown, done.Mode);
        Assert.Equal([JobState.Queued, JobState.Running, JobState.Completed], seen);
    }

    [Fact]
    public async Task WaitForJobTimesOut()
    {
        var (client, fake) = Client();
        fake.Responses.Enqueue(Json("""{"id":"j","status":"running","progress":0}"""));
        await Assert.ThrowsAsync<TimeoutException>(() =>
            client.WaitForJobAsync("j", TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public async Task StatusHistoryEncodesQuery()
    {
        var (client, fake) = Client(apiKey: "");
        fake.Responses.Enqueue(Json("[]"));
        Assert.Empty(await client.GetStatusHistoryAsync(20, "you tube"));
        Assert.Equal("https://example.test/v1/status/history?limit=20&platform=you%20tube", fake.Calls[0].Url);
    }
}
