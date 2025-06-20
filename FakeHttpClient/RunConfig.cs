namespace FakeHttpClient;

    public class RunConfig
    {
        public string TestSuite { get; }
        public int ProxyPort { get; }
        public string ProxyIp { get; }
        public bool Interactive { get; private set; }
        public string Url { get; }
        public string TestName { get; private set; }
        public string TestFileOverride { get; }
        public bool RawTcp { get; }

        public RunConfig(string testSuite,
            int proxyPort,
            string proxyIp,
            bool interactive,
            string url,
            string testName,
            string testFileOverride,
            bool rawTcp)
        {

            TestSuite = testSuite;
            ProxyPort = proxyPort;
            ProxyIp = proxyIp;
            Interactive = interactive;
            Url = url;
            TestName = testName;
            TestFileOverride = testFileOverride;
            RawTcp = rawTcp;
        }

        private bool HasNoInput => string.IsNullOrEmpty(Url) && string.IsNullOrEmpty(TestSuite) && string.IsNullOrEmpty(TestFileOverride);

        public void Validate()
        {
            if (HasNoInput)
            {
                throw new Exception("No input (file, resource, or url) given");
            }
            if (ProxyPort < 1 || ProxyPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(ProxyPort), "Proxy port must be between 1 and 65535.");
            }

            if (string.IsNullOrEmpty(ProxyIp))
            {
                throw new ArgumentNullException(nameof(ProxyIp), "Proxy IP cannot be null or empty.");
            }
        }

        public bool ExecuteOne => !string.IsNullOrEmpty(Url);

        public async Task<List<TestDefinition>> ReadTests()
        {
            if (string.IsNullOrEmpty(TestFileOverride))
            {
                return await ReadResource();
            }

            return await ReadFile();
        }

        private async Task<List<TestDefinition>> ReadFile()
        {
            Console.WriteLine($"Reading local file: {TestFileOverride}");
            return await TestDefinition.ReadFileAsync(TestFileOverride);
        }

        private async Task<List<TestDefinition>> ReadResource()
        {
            Console.WriteLine($"Reading resource: {TestSuite}");
            return await TestDefinition.ReadResourceAsync(TestSuite);
        }
    }
