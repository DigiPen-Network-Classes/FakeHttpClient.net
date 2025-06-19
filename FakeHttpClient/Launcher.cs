using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FakeHttpClient
{
    public class Launcher
    {
        private readonly List<TestDefinition> _tests;
        private readonly bool _interactive;
        private readonly int _proxyPort;
        private readonly string _proxyIp;
        private readonly bool _tcpProxy;

        /// <summary>
        /// Launch many tests in parallel.
        /// </summary>
        /// <remarks>For now, interactive flag is ignored (because linux/osx/windows problems)</remarks>
        /// <param name="tests"></param>
        /// <param name="interactive"></param>
        /// <param name="proxyPort"></param>
        /// <param name="proxyIp"></param>
        /// <param name="tcpProxy"></param>
        public Launcher(List<TestDefinition> tests, bool interactive, int proxyPort, string proxyIp, bool tcpProxy = false)
        {
            _tests = tests;
            _interactive = interactive;
            _proxyPort = proxyPort;
            _proxyIp = proxyIp;
            _tcpProxy = tcpProxy;
        }

        public async Task ExecuteAsync(CancellationTokenSource tokenSource)
        {
            var token = tokenSource.Token;
            // ignore interactive
            var requests = _tests
                .Select(test => ProxyRequestFactory.CreateProxyRequest(false, test.Url, test.Name, _proxyPort, _proxyIp, _tcpProxy))
                .ToList();
            try
            {
                var tasks = requests.Select(r => r.ExecuteAsync(token)).ToList();
                var allExited = await WaitForAllOrTimeout(tasks, TimeSpan.FromSeconds(Program.TimeoutSeconds));
                if (allExited)
                {
                    // check for exceptions
                    await Task.WhenAll(tasks);
                }
                else
                {
                    // timeout
                    Console.WriteLine("Timeout reached! Cancelling remaining tasks!");
                    tokenSource.Cancel();
                }
            }
            finally
            {
                foreach (var r in requests)
                {
                    r.Dispose();
                }
            }
        }

        private static async Task<bool> WaitForAllOrTimeout(IEnumerable<Task> tasks, TimeSpan timeout)
        {
            var timeoutTask = Task.Delay(timeout);
            var allTasks = Task.WhenAll(tasks);
            var completed = await Task.WhenAny(allTasks, timeoutTask);
            return completed == allTasks;
        }
    }
}
