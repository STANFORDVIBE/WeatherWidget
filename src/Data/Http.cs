using System.IO;
using System.Net;
using System.Text;

namespace WeatherWidget
{
    static class Http
    {
        const int TimeoutMs = 8000;   // дольше ждать нет смысла: параллельно уже спрашиваем запасной источник

        // MET Norway требует, чтобы приложение представлялось в User-Agent
        public static readonly string UserAgent = "WeatherWidget/" + Program.Version + " (desktop weather widget for Windows)";

        public static string Download(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = TimeoutMs;
            req.ReadWriteTimeout = TimeoutMs;
            req.UserAgent = UserAgent;
            // только gzip: «deflate» серверы присылают в обёртке zlib, а .NET Framework ждёт «сырой» deflate и падает
            req.AutomaticDecompression = DecompressionMethods.GZip;
            using (WebResponse resp = req.GetResponse())
            using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return r.ReadToEnd();
        }
    }
}
