using System.Diagnostics;
using System.Net;

namespace FakeHttpClient;

internal class Program
{
    public const int TimeoutSeconds = 20;
    private static bool Success { get; set; }

    /// <summary>
    /// FakeHttpClient for testing Assignments in CS 260 Computer Networks I
    ///
    /// This has multiple modes of operation.
    /// 1. If given a url and --interactive=true (the default): request that url, output to the console (including timing information).
    /// (RunOne)
    ///
    /// 2. If given a url --interactive=false, request the url and write the output to a file (using the name and/or url as the output file name).
    /// (RunOne File)
    /// 3. If given a test suite and --interactive, read the list of urls and request each one in a new Console Window.
    ///
    /// 4. If given a test suite and --interactive=false, read the list of urls, request each one and write the output to a file (using the name/url) - each test
    /// gets their own file.
    ///
    /// If you specify a test-file-override (path to some json file) then read the urls from that file instead of a resource.
    /// </summary>
    /// <remarks>
    /// Due to technical reasons and me not having enough time to fix it ...
    /// Modes 3 and 4 will always work NON-interactively on OSX and Linux. The --interactive flag is ignored.
    /// You can have one url be interactive, but multiples is running into problems with the Mono runtime and Terminal behavior.
    /// </remarks>
    /// <param name="testSuite">name of the resource that describes the test(s) to be run</param>
    /// <param name="testFileOverride">If given, read this file instead of a resource.</param>
    /// <param name="proxyPort">the port the proxy (assignment) is listening to</param>
    /// <param name="proxyIp">the IP address of the proxy (assignment) server</param>
    /// <param name="interactive">If true, output to console, otherwise write results to files</param>
    /// <param name="url">If given a url, request that url and output either to file or console depending on interactive flag</param>
    /// <param name="testName">If given, used to name the output file when interactive is false</param>
    /// <param name="raw">If true, use raw TCP Proxy instead of HTTP Proxy (HTTP Proxy just returns the body, not the raw stream)</param>
    public static Task Main(string testSuite = "",
        int proxyPort = 8888,
        string proxyIp = "127.0.0.1",
        bool interactive = true,
        string url = "",
        string testName = "",
        string testFileOverride = "",
        bool raw = true)
    {
        var args = new RunConfig(testSuite, proxyPort, proxyIp, interactive, url, testName, testFileOverride, raw);
        args.Validate();
        // allow more than 2 connections to the same host
        ServicePointManager.DefaultConnectionLimit = 20;

        try
        {
            // NOTE: There is a bug in System.Commandline.2.0.0 and dragonfruit
            // this doesn't 'await' correctly. Fix when 2.0.0.0 is released...
            // and anyway, DragonFruit is deprecated and "going away" at 2.0.0.
            // see https://github.com/dotnet/command-line-api/issues/2152
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            RunTests(args, cts).Wait(cts.Token);
        }
        catch (OperationCanceledException e)
        {
            if (!Success)
            {
                Console.WriteLine($"Cancellation requested or timeout reached: { e.Message}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Exception Running Tests: {e}");
        }

        return Task.CompletedTask;
    }

    private static async Task RunTests(RunConfig args, CancellationTokenSource cts)
    {
        Console.WriteLine($"Begin - {DateTime.Now}");
        var sw = Stopwatch.StartNew();
        if (args.ExecuteOne)
        {
            using var request = ProxyRequestFactory.CreateProxyRequest(args.Interactive, args.Url, args.TestName, args.ProxyPort, args.ProxyIp, args.RawTcp);
            await request.ExecuteAsync(cts.Token);
            Success = true; // so we don't interpret the cancellation as a failure
            await cts.CancelAsync();
        }
        else
        {
            var tests = await args.ReadTests();
            var actor = new Launcher(tests, args.Interactive, args.ProxyPort, args.ProxyIp, args.RawTcp);
            await actor.ExecuteAsync(cts);
            Success = true; // so we don't interpret the cancellation as a failure
            await cts.CancelAsync();
        }
        sw.Stop();
        await Console.Out.WriteLineAsync($"End - {DateTime.Now} (elapsed: {sw.ElapsedMilliseconds} ms)");
        await Console.Out.FlushAsync();
    }
}
