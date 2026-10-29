using System;
using System.Threading;
using System.Windows.Forms;

namespace WeatherWidget
{
    // Новые версии на GitHub: проверка (через минуту после запуска, дальше раз в сутки и из меню)
    // и установка с перезапуском. Окно только показывает пункт меню и закрывается, когда новая версия стоит.
    sealed class AppUpdater : IDisposable
    {
        readonly Action<MethodInvoker> invoke;   // выполнить в потоке окна
        readonly Action changed;                 // пункт меню «Обновить до версии …» изменился
        readonly Action restart;                 // новая версия установлена — сохранить настройки и перезапуститься
        readonly string logPath;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        UpdateInfo available;
        bool installing;

        public AppUpdater(Action<MethodInvoker> invoke, Action changed, Action restart, string logPath)
        {
            this.invoke = invoke;
            this.changed = changed;
            this.restart = restart;
            this.logPath = logPath;
            // через минуту после запуска (не мешать загрузке погоды), дальше раз в сутки
            timer.Interval = 60 * 1000;
            timer.Tick += delegate { timer.Interval = 24 * 60 * 60 * 1000; Check(false); };
            timer.Start();
        }

        // Вышла новая версия; null — нет или ещё не проверяли
        public UpdateInfo Available { get { return available; } }

        // Идёт загрузка обновления
        public bool Installing { get { return installing; } }

        // manual — из меню: сказать и когда новой версии нет, и когда проверить не удалось
        public void Check(bool manual)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string json = null;
                try { json = Http.Download(Updates.LatestUrl(Updates.Repo)); }
                catch (Exception e) { ErrorLog.Note(logPath, "GitHub, проверка обновлений: " + e.GetType().Name + ": " + e.Message, DateTime.Now); }
                UpdateInfo u = null;
                try { if (json != null) u = Updates.Newer(json, Program.Version); } catch { }
                invoke(delegate
                {
                    if (u != null) Show(u);
                    if (!manual) return;
                    if (u != null) { if (u.CanInstall && !installing) AskInstall(u); else if (!u.CanInstall) OpenUrl(u.Url); }
                    else if (json == null)
                        MessageBox.Show(L.T("Не удалось проверить обновления — нет связи с GitHub.", "Couldn't check for updates — GitHub is unreachable."),
                            L.T("Обновления", "Updates"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else
                        MessageBox.Show(L.T("У вас последняя версия: ", "You have the latest version: ") + Program.Version,
                            L.T("Обновления", "Updates"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            });
        }

        // Пункт «Доступна версия …» первым в меню
        public void Show(UpdateInfo u)
        {
            if (available != null && available.Version == u.Version) return;
            available = u;
            changed();
        }

        void AskInstall(UpdateInfo u)
        {
            if (MessageBox.Show(L.T("Вышла версия ", "Version ") + u.Version + L.T(". Обновить сейчас? Виджет перезапустится.", " is out. Update now? The widget will restart."),
                    L.T("Обновления", "Updates"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Install(u);
        }

        // Скачать, проверить и подменить exe, затем перезапуститься новой версией
        public void Install(UpdateInfo u)
        {
            if (installing) return;
            installing = true;
            invoke(delegate { changed(); });   // «Загрузка обновления…»
            string exe = Application.ExecutablePath;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception fail = null;
                try
                {
                    byte[] zip = Http.DownloadBytes(u.ZipUrl);
                    string sha = Http.Download(u.ShaUrl);
                    Updates.Install(exe, Updates.ExtractExe(zip, sha), u.Version);
                }
                catch (Exception e) { fail = e; }
                invoke(delegate
                {
                    installing = false;
                    if (fail == null)
                    {
                        try { restart(); return; }
                        catch (Exception e) { fail = e; }
                    }
                    ErrorLog.Write(logPath, fail, DateTime.Now);
                    changed();
                    if (MessageBox.Show(L.T("Не удалось обновиться: ", "Couldn't update: ") + fail.Message +
                            L.T("\n\nОткрыть страницу релиза, чтобы скачать вручную?", "\n\nOpen the release page to download it manually?"),
                            L.T("Обновления", "Updates"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        OpenUrl(u.Url);
                });
            });
        }

        public static void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(url); } catch { }
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}
