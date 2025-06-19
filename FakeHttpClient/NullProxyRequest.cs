namespace FakeHttpClient;

public class NullProxyRequest : ProxyRequest
{
    public NullProxyRequest(bool interactive, string url, string name, int proxyPort, string proxyIp) : base(interactive, url, name, proxyPort, proxyIp)
    {
    }

    protected override async Task PrepareTest(CancellationToken token)
    {
        await Writer.WriteLineAsync("Prepare Test");
        await Writer.FlushAsync(token);

    }

    protected override async Task ExecuteTest(CancellationToken token)
    {
        await Writer.WriteLineAsync("Execute Test");
        await Writer.FlushAsync(token);
    }
}
