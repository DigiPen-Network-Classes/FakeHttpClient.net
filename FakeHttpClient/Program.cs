using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FakeHttpClient
{
    internal class Program
    {
        /// <summary>
        /// FakeHttpClient for testing Assignments in CS 260 Computer Networks I
        ///
        /// This has multiple modes of operation:
        /// 1. If given a url and --interactive=true (the default): request that url, output to the console (including timing information).
        ///
        /// 2. If given a url --interactive=false, request the url and write the output to a file (using the name and/or url as the output file name).
        ///
        /// 3. If given a test file and --interactive, read the list of urls and request each one in a new instance of 1. program
        ///
        /// 4. If given a test file and --interactive=false, read the list of urls, request each one in a new instance of 2. program.
        /// </summary>
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

            if (!string.IsNullOrEmpty(testFile))
            {
                // TODO do file stuff
            }
            else
            {
                var actor = new ProxyRequest(url, testName, interactive, proxyPort, proxyIp);
                await actor.ExecuteAsync();
            }

/*
            var tests = new List<TestDefinition>();
            try
            {
                var json = File.ReadAllText(testFile);
                tests = JsonSerializer.Deserialize<List<TestDefinition>>(json);
            }
            catch (IOException e)
            {
                Console.WriteLine($"Error reading test file: {testFile} - {e.Message}");
                return;
            }
*/
        }
    }
}
