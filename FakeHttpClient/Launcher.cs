using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace FakeHttpClient
{
    public class Launcher
    {
        private readonly List<TestDefinition> _tests;
        private readonly bool _interactive;
        private readonly int _proxyPort;
        private readonly string _proxyIp;

        public Launcher(List<TestDefinition> tests, bool interactive, int proxyPort, string proxyIp)
        {
            _tests = tests;
            _interactive = interactive;
            _proxyPort = proxyPort;
            _proxyIp = proxyIp;
        }

        public async Task ExecuteAsync()
        {
            var processes = new List<Process>();
            foreach (var test in _tests)
            {
                var p = ExecuteOne(test);
                if (p == null)
                {
                    throw new Exception($"Failed to start test: {test}");
                }
                processes.Add(ExecuteOne(test));
            }

            var allExited = await WaitForAllOrTimeout(processes, TimeSpan.FromSeconds(15));
            if (!allExited)
            {
                Console.WriteLine("Timeout reached! Killing all processes and exiting...");
                foreach(var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error killing process {process.Id}: {ex.Message}");
                    }
                }
                // us too
                try
                {
                    Environment.Exit(1);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private Process ExecuteOne(TestDefinition test)
        {
            var exePath = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("Could not determine the entry assembly location.");
            exePath = $"\"{exePath}\"";
            var args = $"--proxy-ip {_proxyIp} --proxy-port {_proxyPort} --url {test.Url} --test-name {test.Name} --interactive {_interactive}";
            Console.WriteLine($"{exePath} {args}");
            return Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = true,
            });
        }

        private static async Task<bool> WaitForAllOrTimeout(List<Process> processes, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (processes.All(p => p.HasExited))
                {
                    return true; // All processes have exited
                }
                await Task.Delay(100); // Wait a bit before checking again
            }
            return false; // Timeout reached, not all processes exited
        }
    }
}
