using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace WeatherWidget
{
    // Новая версия на GitHub: раз в сутки виджет смотрит последний релиз
    // (https://api.github.com/repos/<владелец>/<репозиторий>/releases/latest)
    // и, если он новее, показывает в меню «Доступна версия …». Сам ничего не скачивает.
    sealed class UpdateInfo
    {
        public string Version;   // «1.2.0»
        public string Url;       // страница релиза
    }

    static class Updates
    {
        // «владелец/репозиторий» на GitHub; пусто — проверка выключена
        public static readonly string Repo = "STANFORDVIBE/WeatherWidget";

        public static string LatestUrl(string repo)
        {
            return "https://api.github.com/repos/" + repo + "/releases/latest";
        }

        // Последний релиз, если он новее current; иначе null
        public static UpdateInfo Newer(string json, string current)
        {
            Dictionary<string, object> r = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            object tag, url;
            if (!r.TryGetValue("tag_name", out tag) || tag == null) return null;
            string v = tag.ToString().TrimStart('v', 'V');
            Version latest, mine;
            if (!System.Version.TryParse(v, out latest) || !System.Version.TryParse(current, out mine) || latest <= mine) return null;
            UpdateInfo u = new UpdateInfo();
            u.Version = v;
            u.Url = r.TryGetValue("html_url", out url) && url != null ? url.ToString() : "https://github.com/" + Repo + "/releases/latest";
            return u;
        }

        // Не бросает исключений: нет связи или репозиторий не задан — null
        public static UpdateInfo Check(Func<string, string> download)
        {
            if (Repo == "") return null;
            try { return Newer(download(LatestUrl(Repo)), Program.Version); }
            catch { return null; }
        }
    }
}
