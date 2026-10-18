using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace FakeHttpClient
{
    public class TestDefinition
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public RequestMode Mode { get; set; } = RequestMode.Normal;

        /// <summary>
        /// A URL containing this token has it replaced, at load time, with the current IP address of
        /// <see cref="IpPlaceholderHost"/>. The raw-IP delay test has to hit the same box as the
        /// DNS-name delay test, but that box's IP changes yearly. Resolving it via DNS at runtime
        /// means we never hand-edit multi.json (and cut a release) just because the IP moved.
        /// </summary>
        private const string IpPlaceholder = "{meancat-ip}";
        private const string IpPlaceholderHost = "cs260.meancat.com";

        public override string ToString()
        {
            return Mode == RequestMode.Normal ? $"{Name}: {Url}" : $"{Name}: {Url} ({Mode})";
        }

        /// <summary>
        /// Read a named test suite: a loose .json file shipped next to the executable.
        /// Both "multi" and "multi.json" resolve to multi.json in the application directory.
        /// Throws on IO or JSON problems.
        /// </summary>
        public static async Task<List<TestDefinition>> ReadSuiteAsync(string suiteName)
        {
            return await ReadFileAsync(ResolveSuitePath(suiteName));
        }

        /// <summary>
        /// Read a test suite from an arbitrary path (used by --test-file-override).
        /// Throws on IO or JSON problems.
        /// </summary>
        /// <exception cref="FileNotFoundException"></exception>
        public static async Task<List<TestDefinition>> ReadFileAsync(string fileName)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException($"Could not find test file: {fileName}", fileName);
            }
            await using var stream = File.OpenRead(fileName);
            return await ReadStream(stream);
        }

        /// <summary>
        /// Resolve a suite name to a file path next to the executable. Accepts the name with or
        /// without the .json extension; returns the best-guess path even if it does not exist so
        /// callers get a sensible "not found" message.
        /// </summary>
        private static string ResolveSuitePath(string suiteName)
        {
            var dir = AppContext.BaseDirectory;
            var candidates = suiteName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? new[] { suiteName }
                : new[] { suiteName + ".json", suiteName };

            foreach (var candidate in candidates)
            {
                var path = Path.Combine(dir, candidate);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return Path.Combine(dir, candidates[0]);
        }

        private static async Task<List<TestDefinition>> ReadStream(Stream stream)
        {
            var tests = await JsonSerializer.DeserializeAsync<List<TestDefinition>>(stream) ?? new List<TestDefinition>();
            await SubstitutePlaceholdersAsync(tests);
            return tests;
        }

        /// <summary>
        /// Replace <see cref="IpPlaceholder"/> in any test URL with the current IP of
        /// <see cref="IpPlaceholderHost"/>. Only performs the DNS lookup if the placeholder is
        /// actually used, so suites without it (e.g. single.json) need no network access.
        /// </summary>
        private static async Task SubstitutePlaceholdersAsync(List<TestDefinition> tests)
        {
            var needsIp = tests.Where(t => t.Url.Contains(IpPlaceholder)).ToList();
            if (needsIp.Count == 0)
            {
                return;
            }

            var ip = await ResolveIpAsync();
            foreach (var test in needsIp)
            {
                if (ip is null)
                {
                    Console.WriteLine($"WARNING: could not resolve {IpPlaceholderHost}; leaving placeholder in test '{test.Name}'");
                    continue;
                }
                test.Url = test.Url.Replace(IpPlaceholder, ip);
            }
        }

        private static async Task<string?> ResolveIpAsync()
        {
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(IpPlaceholderHost);
                var preferred = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                                ?? addresses.FirstOrDefault();
                return preferred?.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine($"WARNING: DNS lookup for {IpPlaceholderHost} failed: {e.Message}");
                return null;
            }
        }
    }
}