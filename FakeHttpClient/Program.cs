using System;
using System.IO;
using System.Net;
using System.Text;

namespace FakeHttpClient
{
    internal class Program
    {
        /// <summary>
        /// FakeHttpClient for testing Assignments in CS 260 Computer Networks I
        /// </summary>
        /// <param name="testfile">name of the file that describes the test(s) to be run</param>
        /// <param name="proxyPort">the port the proxy (assignment) is listening to</param>
        /// <param name="proxyIp">the IP address of the proxy (assignment) server</param>
        /// <param name="interactive">If true, output to console, otherwise write results to files</param>
        public static void Main(string testfile, int proxyPort = 8888, string proxyIp = "127.0.0.1", bool interactive = true)
        {


            //var proxyIp = "10.211.55.3"; // "127.0.0.1"
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Host = new Uri(url).Host;
            request.ProtocolVersion = HttpVersion.Version10;
            request.Proxy = new WebProxy($"http://{proxyIp}:{proxyPort}");;
            request.UserAgent = "Curl/8.9.1"; // lie about who we are
            request.Method = "GET";
            request.Accept = "*/*";
            request.AllowReadStreamBuffering = false; // disables buffering so we can see the delay in the response

            // do NOT keep alive
            request.KeepAlive = false;

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream() ?? Stream.Null)
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    char[] buf = new char[64];
                    int bytesRead;
                    while ((bytesRead = reader.Read(buf, 0, buf.Length)) > 0)
                    {
                        Console.Write(new string(buf, 0, bytesRead));
                    }
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.Response != null)
                {
                    using (var reader = new StreamReader(ex.Response.GetResponseStream() ?? Stream.Null))
                    {
                        Console.WriteLine(reader.ReadToEnd());
                    }
                }
            }
        }
    }
}
