using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FakeHttpClient
{
    public class TestDefinition
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";

        public override string ToString()
        {
            return $"{Name}: {Url}";
        }

        /// <summary>
        /// Read the test file into a list of definitions.
        /// Throws exceptions on IO or JSON problems.
        /// </summary>
        /// <param name="testFile"></param>
        /// <returns></returns>
        public static async Task<List<TestDefinition>> ReadAsync(string testFile)
        {
            return await JsonSerializer.DeserializeAsync<List<TestDefinition>>(File.OpenRead(testFile));
        }
    }
}
