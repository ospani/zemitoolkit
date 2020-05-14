using System;
using System.IO;
using System.Net;
using System.Threading;

namespace ZemiScrape
{
    public class JSONGetter
    {
        public static string GetAsJSONString(string uri)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                //probably a time out, so wait a bit
                Console.WriteLine($"Error: {ex.Message}");
                Thread.Sleep(2000);
                return null;
            }
        }
    }
}
