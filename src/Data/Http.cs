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
            try
            {
                using (WebResponse resp = req.GetResponse())
                using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return r.ReadToEnd();
            }
            catch (WebException e)
            {
                if (e.Response == null) throw;
                // ответ с ошибкой (429, 500…) нужно закрыть, иначе он держит соединение;
                // а его текст («Too many requests…») — в сообщение, чтобы причина попала в журнал
                string body = "";
                try
                {
                    using (WebResponse resp = e.Response)
                    using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        body = r.ReadToEnd().Trim();
                }
                catch { }
                if (body.Length > MaxErrorBody) body = body.Substring(0, MaxErrorBody) + "…";
                throw new WebException(body == "" ? e.Message : e.Message + " " + body, e, e.Status, null);
            }
        }

        const int MaxErrorBody = 200;

        // Файл целиком (архив обновления): ждём дольше, переадресацию GitHub на хранилище проходим сами
        public static byte[] DownloadBytes(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 60000;
            req.ReadWriteTimeout = 60000;
            req.UserAgent = UserAgent;
            using (WebResponse resp = req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (MemoryStream m = new MemoryStream())
            {
                s.CopyTo(m);
                return m.ToArray();
            }
        }
    }
}
