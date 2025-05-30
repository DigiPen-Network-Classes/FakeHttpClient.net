using System;
using System.IO;
using System.Linq;
using System.Text;

namespace FakeHttpClient
{
    public static class WriterFactory
    {

        public static TextWriter BuildWriter(bool interactive, string url, string testName)
        {
            if (interactive)
            {
                return Console.Out;
            }

            var safeFileName = BuildFileName(url, testName);
            var execDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(execDir, safeFileName);
            Console.WriteLine($"Writing output to file: {path}");
            return new StreamWriter(File.Create(path), Encoding.UTF8);
        }

        private static string BuildFileName(string url, string testName)
        {
            return string.IsNullOrWhiteSpace(testName) ? $"{SanitizeFileName(url)}.txt" : $"{testName}.txt";
        }

        private static string SanitizeFileName(string url)
        {
            // Remove invalid characters for file names
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new StringBuilder(url.Length);
            foreach (var c in url.Where(c => Array.IndexOf(invalidChars, c) < 0))
            {
                sanitized.Append(c);
            }
            return sanitized.ToString();
        }
    }
}
