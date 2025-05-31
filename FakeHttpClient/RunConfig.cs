using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FakeHttpClient
{
    public class RunConfig
    {
        private readonly string _testSuite;
        private readonly int _proxyPort;
        private readonly string _proxyIp;
        private readonly bool _interactive;
        private readonly string _url;
        private readonly string _testName;
        private readonly string _testFileOverride;

        public RunConfig(string testSuite,
            int proxyPort,
            string proxyIp,
            bool interactive ,
            string url,
            string testName,
            string testFileOverride)
        {

            _testSuite = testSuite;
            _proxyPort = proxyPort;
            _proxyIp = proxyIp;
            _interactive = interactive;
            _url = url;
            _testName = testName;
            _testFileOverride = testFileOverride;
        }

        private bool HasNoInput => string.IsNullOrEmpty(_url) && string.IsNullOrEmpty(_testSuite) && string.IsNullOrEmpty(_testFileOverride);

        public void Validate()
        {
            if (HasNoInput)
            {
                throw new Exception("No input (file, resource, or url) given");
            }
            if (_proxyPort < 1 || _proxyPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(_proxyPort), "Proxy port must be between 1 and 65535.");
            }

            if (string.IsNullOrEmpty(_proxyIp))
            {
                throw new ArgumentNullException(nameof(_proxyIp), "Proxy IP cannot be null or empty.");
            }
        }

        public bool ExecuteOne => !string.IsNullOrEmpty(_url);

        public async Task<List<TestDefinition>> ReadTests()
        {
            if (string.IsNullOrEmpty(_testFileOverride))
            {
                return await ReadResource();
            }

            return await ReadFile();
        }

        private async Task<List<TestDefinition>> ReadFile()
        {
            Console.WriteLine($"Reading local file: {_testFileOverride}");
            return await TestDefinition.ReadFileAsync(_testFileOverride);
        }

        private async Task<List<TestDefinition>> ReadResource()
        {
            Console.WriteLine($"Reading resource: {_testSuite}");
            return await TestDefinition.ReadResourceAsync(_testSuite);
        }
    }
}
