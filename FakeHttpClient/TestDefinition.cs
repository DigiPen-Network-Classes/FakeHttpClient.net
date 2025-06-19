using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        /// Read the test file from embedded resource into a list of definitions.
        /// Throws exceptions on IO or JSON problems.
        /// </summary>
        /// <param name="testFile"></param>
        /// <returns></returns>
        public static async Task<List<TestDefinition>> ReadResourceAsync(string testFile)
        {
            var asm = Assembly.GetExecutingAssembly();
            testFile = $"{asm.GetName().Name}.{testFile}";
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(testFile) ?? throw new FileNotFoundException(testFile);
            return await ReadStream(stream);
        }

        /// <summary>
        /// Read a local file into a list of test definitions.
        /// throws exceptions on IO or JSON problems.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public static async Task<List<TestDefinition>> ReadFileAsync(string fileName)
        {
            var stream = File.OpenRead(fileName);
            if (stream == null)
            {
                throw new FileNotFoundException($"Could not open file: {fileName}");
            }
            return await ReadStream(stream);
        }

        private static async Task<List<TestDefinition>> ReadStream(Stream stream)
        {
            return await JsonSerializer.DeserializeAsync<List<TestDefinition>>(stream) ?? new List<TestDefinition>();
        }
    }
}
