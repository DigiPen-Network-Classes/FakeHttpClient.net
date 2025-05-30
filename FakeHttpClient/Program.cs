using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FakeHttpClient
{
    internal class Program
    {
        /// <summary>
        /// FakeHttpClient for testing Assignments in CS 260 Computer Networks I
        ///
        /// This has multiple modes of operation.
        /// 1. If given a url and --interactive=true (the default): request that url, output to the console (including timing information).
        ///
        /// 2. If given a url --interactive=false, request the url and write the output to a file (using the name and/or url as the output file name).
        ///
        /// 3. If given a test file and --interactive, read the list of urls and request each one in a new Console Window.
        ///
        /// 4. If given a test file and --interactive=false, read the list of urls, request each one and write the output to a file (using the name/url) - each test
        /// gets their own file.
        /// </summary>
        /// <remarks>
        /// Due to technical reasons and me not having enough time to fix it ...
        /// Modes 3 and 4 will always work NON-interactively on OSX and Linux. The --interactive flag is ignored.
        /// You can have one url be interactive, but multiples is running into problems with the Mono runtime and Terminal behavior.
        /// </remarks>
        /// <param name="testFile">(optional) name of the file that describes the test(s) to be run</param>
        /// <param name="proxyPort">the port the proxy (assignment) is listening to</param>
        /// <param name="proxyIp">the IP address of the proxy (assignment) server</param>
        /// <param name="interactive">If true, output to console, otherwise write results to files</param>
        /// <param name="url">If given a url, request that url and output either to file or console depending on interactive flag</param>
        /// <param name="testName">If given, used to name the output file when interactive is false</param>
        public static async Task Main(string testFile = "", int proxyPort = 8888, string proxyIp = "127.0.0.1", bool interactive = true,
            string url = "", string testName = "")
        {
            if (!string.IsNullOrEmpty(testFile) && !string.IsNullOrEmpty(url))
            {
                throw new Exception("Cannot specify both a testFile and a Url -- one or the other.");
            }

            if (string.IsNullOrEmpty(testFile) && string.IsNullOrEmpty(url))
            {
                throw new Exception("Must specify either a testFile or a Url -- one or the other.");
            }

            if (proxyPort < 1 || proxyPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(proxyPort), "Proxy port must be between 1 and 65535.");
            }

            if (string.IsNullOrEmpty(proxyIp))
            {
                throw new ArgumentNullException(nameof(proxyIp), "Proxy IP cannot be null or empty.");
            }
            Console.WriteLine($"Begin - {DateTime.Now}");
            var sw = Stopwatch.StartNew();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            if (!string.IsNullOrEmpty(testFile))
            {
                Console.WriteLine($"Reading {testFile}");
                var tests = await TestDefinition.ReadAsync(testFile);
                var actor = new Launcher(tests, interactive, proxyPort, proxyIp);
                await actor.ExecuteAsync(cts);
            }
            else
            {
                await ProxyRequest.ExecuteOne(url, testName, interactive, proxyPort, proxyIp, cts.Token);
            }

            sw.Stop();
            Console.WriteLine($"End - {DateTime.Now} (elapsed: {sw.ElapsedMilliseconds} ms)");
        }
    }
}
