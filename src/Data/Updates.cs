using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace WeatherWidget
{
    // Новая версия на GitHub: раз в сутки (и по пункту меню) виджет смотрит последний релиз
    // (https://api.github.com/repos/<владелец>/<репозиторий>/releases/latest) и, если он новее,
    // показывает в меню «Обновить до версии …». Обновление — только по нажатию: скачать архив релиза,
    // сверить SHA-256 (файл .sha256 рядом с архивом) и версию exe, подменить exe и перезапуститься.
    sealed class UpdateInfo
    {
        public string Version;   // «1.2.0»
        public string Url;       // страница релиза
        public string ZipUrl;    // WeatherWidget-<версия>.zip; null — в релизе нет архива
        public string ShaUrl;    // WeatherWidget-<версия>.zip.sha256; null — нечем проверить архив

        // обновиться прямо из виджета можно, только если есть и архив, и его контрольная сумма
        public bool CanInstall { get { return ZipUrl != null && ShaUrl != null; } }
    }

    static class Updates
    {
        // «владелец/репозиторий» на GitHub; пусто — проверка выключена
        public static readonly string Repo = "STANFORDVIBE/WeatherWidget";
        public const string ExeName = "WeatherWidget.exe";

        public static string LatestUrl(string repo)
        {
            return "https://api.github.com/repos/" + repo + "/releases/latest";
        }

        // Последний релиз, если он новее current; иначе null
        public static UpdateInfo Newer(string json, string current)
        {
            Dictionary<string, object> r = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            object tag, url, assets;
            if (!r.TryGetValue("tag_name", out tag) || tag == null) return null;
            string v = tag.ToString().TrimStart('v', 'V');
            Version latest, mine;
            if (!System.Version.TryParse(v, out latest) || !System.Version.TryParse(current, out mine) || latest <= mine) return null;
            UpdateInfo u = new UpdateInfo();
            u.Version = v;
            u.Url = r.TryGetValue("html_url", out url) && url != null ? url.ToString() : "https://github.com/" + Repo + "/releases/latest";
            if (r.TryGetValue("assets", out assets) && assets is object[])
            {
                string zip = "WeatherWidget-" + v + ".zip";
                foreach (object o in (object[])assets)
                {
                    Dictionary<string, object> a = o as Dictionary<string, object>;
                    object name, link;
                    if (a == null || !a.TryGetValue("name", out name) || !a.TryGetValue("browser_download_url", out link) || link == null) continue;
                    if (zip.Equals(name as string, StringComparison.OrdinalIgnoreCase)) u.ZipUrl = link.ToString();
                    else if ((zip + ".sha256").Equals(name as string, StringComparison.OrdinalIgnoreCase)) u.ShaUrl = link.ToString();
                }
            }
            return u;
        }

        // Не бросает исключений: нет связи или репозиторий не задан — null
        public static UpdateInfo Check(Func<string, string> download)
        {
            if (Repo == "") return null;
            try { return Newer(download(LatestUrl(Repo)), Program.Version); }
            catch { return null; }
        }

        // ---------- Установка ----------

        // exe из архива релиза, если SHA-256 архива совпадает с файлом .sha256 («<hex>  <имя>», как у sha256sum)
        public static byte[] ExtractExe(byte[] zip, string shaText)
        {
            string want = (shaText ?? "").Trim().Split(' ', '\t', '\r', '\n')[0].ToLowerInvariant();
            string got;
            using (SHA256 sha = SHA256.Create()) got = BitConverter.ToString(sha.ComputeHash(zip)).Replace("-", "").ToLowerInvariant();
            if (want.Length != 64 || want != got) throw new InvalidDataException("контрольная сумма архива не совпадает");

            using (ZipArchive a = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
                foreach (ZipArchiveEntry e in a.Entries)
                    if (e.Name.Equals(ExeName, StringComparison.OrdinalIgnoreCase))
                        using (Stream s = e.Open())
                        using (MemoryStream m = new MemoryStream())
                        {
                            s.CopyTo(m);
                            return m.ToArray();
                        }
            throw new InvalidDataException("в архиве нет " + ExeName);
        }

        // Подменить exe новой версией. Запущенный exe нельзя перезаписать, но можно переименовать:
        // старый уходит в WeatherWidget.old.exe (удаляется при следующем запуске), новый встаёт на его место.
        // Сначала новый файл проверяется: это сборка виджета ровно той версии, что обещал релиз.
        public static void Install(string exePath, byte[] newExe, string version)
        {
            string fresh = exePath + ".new", old = OldPath(exePath);
            File.WriteAllBytes(fresh, newExe);
            try
            {
                AssemblyName n = AssemblyName.GetAssemblyName(fresh);
                Version want = System.Version.Parse(version);
                if (n.Name != "WeatherWidget" || n.Version == null || n.Version.Major != want.Major || n.Version.Minor != want.Minor ||
                    n.Version.Build != Math.Max(0, want.Build))
                    throw new InvalidDataException("в архиве не та версия программы");
                if (File.Exists(old)) File.Delete(old);
                File.Move(exePath, old);
                try { File.Move(fresh, exePath); }
                catch { File.Move(old, exePath); throw; }   // вернуть как было
            }
            finally
            {
                try { if (File.Exists(fresh)) File.Delete(fresh); } catch { }
            }
        }

        public static string OldPath(string exePath) { return Path.ChangeExtension(exePath, ".old.exe"); }

        // После обновления: удалить прежний exe (раньше нельзя — он ещё работал)
        public static void CleanUp(string exePath)
        {
            try { string old = OldPath(exePath); if (File.Exists(old)) File.Delete(old); } catch { }
        }
    }
}
