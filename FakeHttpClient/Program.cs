using System.Diagnostics;
using System.Net;

namespace FakeHttpClient;

internal class Program
{
    public const int TimeoutSeconds = 20;
    private const int GraceSeconds = 5;

    private const int ExitAllPassed = 0;
    private const int ExitSomeFailed = 1;
    private const int ExitToolError = 2;

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
    /// A test suite is a loose .json file shipped next to the executable (e.g. multi.json); --test-suite names it.
    /// If you specify a test-file-override (path to some json file) then read the urls from that file instead.
    /// </summary>
    /// <remarks>
    /// Due to technical reasons and me not having enough time to fix it ...
    /// Modes 3 and 4 will always work NON-interactively on OSX and Linux. The --interactive flag is ignored.
    /// You can have one url be interactive, but multiples is running into problems with the Mono runtime and Terminal behavior.
    /// </remarks>
    /// <param name="testSuite">name of the test-suite json file (next to the exe) that describes the test(s) to be run</param>
    /// <param name="testFileOverride">If given, read this json file path instead of a named test suite.</param>
    /// <param name="proxyPort">the port the proxy (assignment) is listening to</param>
    /// <param name="proxyIp">the IP address of the proxy (assignment) server</param>
    /// <param name="interactive">If true, output to console, otherwise write results to files</param>
    /// <param name="url">If given a url, request that url and output either to file or console depending on interactive flag</param>
    /// <param name="testName">If given, used to name the output file when interactive is false</param>
    /// <param name="raw">If true, use raw TCP Proxy instead of HTTP Proxy (HTTP Proxy just returns the body, not the raw stream)</param>
    /// <param name="mode">How to send the request when using --url: Normal, NoHalfClose, SplitRequest, LargeRequest, or a comma-separated combination (requires --raw true). Test suites set this per test.</param>
    /// <param name="resultsFile">If given, write machine-readable results to this path: CSV if it ends in .csv, otherwise JSON.</param>
    /// <returns>Exit code: 0 if every test passed, 1 if any test failed, 2 if the tests could not be run.</returns>
    public static int Main(string testSuite = "",
        int proxyPort = 8888,
        string proxyIp = "127.0.0.1",
        bool interactive = true,
        string url = "",
        string testName = "",
        string testFileOverride = "",
        bool raw = true,
        string mode = "",
        string resultsFile = "")
    {
        RunConfig args;
        try
        {
            args = new RunConfig(testSuite, proxyPort, proxyIp, interactive, url, testName, testFileOverride, raw, mode);
            args.Validate();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
            return ExitToolError;
        }
        // allow more than 2 connections to the same host
        ServicePointManager.DefaultConnectionLimit = 20;

        try
        {
            // NOTE: There is a bug in System.Commandline.2.0.0 and dragonfruit
            // this doesn't 'await' correctly. Fix when 2.0.0.0 is released...
            // and anyway, DragonFruit is deprecated and "going away" at 2.0.0.
            // see https://github.com/dotnet/command-line-api/issues/2152
            //
            // The requests themselves stop at the timeout (via cts) and write their results;
            // don't abandon them here at the same moment or their output files are lost.
            // The extra grace period is only a backstop in case something ignores cancellation.
            var tests = args.ExecuteOne ? null : args.ReadTests().GetAwaiter().GetResult();
            var timeoutSeconds = EffectiveTimeoutSeconds(tests);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var started = DateTimeOffset.Now;
            var sw = Stopwatch.StartNew();
            var run = RunTests(args, tests, cts);
            if (!run.Wait(TimeSpan.FromSeconds(timeoutSeconds + GraceSeconds)))
            {
                Console.WriteLine("Tests did not finish after the timeout; giving up.");
                return ExitToolError;
            }

            var summary = ResultsWriter.Summarize(started, sw.ElapsedMilliseconds, run.Result);
            ResultsWriter.WriteTable(Console.Out, summary);
            if (!string.IsNullOrEmpty(resultsFile))
            {
                ResultsWriter.WriteFile(resultsFile, summary);
                Console.WriteLine($"Results written to {Path.GetFullPath(resultsFile)}");
            }
            return summary.Failed == 0 ? ExitAllPassed : ExitSomeFailed;
        }
        catch (Exception e)
        {
            var inner = e is AggregateException { InnerException: not null } ae ? ae.InnerException : e;
            // a missing or malformed suite file is the user's to fix: say what's wrong, skip the stack trace
            Console.WriteLine(inner is FileNotFoundException or System.Text.Json.JsonException
                ? $"Error: {inner.Message}"
                : $"Exception Running Tests: {inner}");
            return ExitToolError;
        }
    }

    /// <summary>
    /// The overall timeout: the default, or longer if some test in the suite needs more time
    /// (a longer MaxMs, a start delay, or a stall).
    /// </summary>
    private static int EffectiveTimeoutSeconds(List<TestDefinition>? tests)
    {
        var longestMs = tests?.Max(t => (t.StartDelayMs ?? 0) + (t.StallMs ?? 0) + (t.MaxMs ?? 0)) ?? 0;
        return Math.Max(TimeoutSeconds, (int)Math.Ceiling(longestMs / 1000.0) + 10);
    }

    private static async Task<List<TestOutcome>> RunTests(RunConfig args, List<TestDefinition>? tests, CancellationTokenSource cts)
    {
        Console.WriteLine($"Begin - {DateTime.Now}");
        var sw = Stopwatch.StartNew();
        List<TestOutcome> outcomes;
        if (args.ExecuteOne)
        {
            var test = new TestDefinition { Name = args.TestName, Url = args.Url, Mode = args.Mode };
            using var request = ProxyRequestFactory.CreateProxyRequest(args.Interactive, test, args.ProxyPort, args.ProxyIp, args.RawTcp);
            outcomes = [await request.ExecuteAsync(cts.Token)];
        }
        else
        {
            var actor = new Launcher(tests!, args.Interactive, args.ProxyPort, args.ProxyIp, args.RawTcp);
            outcomes = await actor.ExecuteAsync(cts);
        }
        await cts.CancelAsync();
        sw.Stop();
        await Console.Out.WriteLineAsync($"End - {DateTime.Now} (elapsed: {sw.ElapsedMilliseconds} ms)");
        await Console.Out.FlushAsync();
        return outcomes;
    }
}
