using System;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Погода — виджет для рабочего стола")]
[assembly: AssemblyProduct("WeatherWidget")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: InternalsVisibleTo("WeatherWidget.Tests")]
[assembly: InternalsVisibleTo("WeatherWidget.Tools")]

namespace WeatherWidget
{
    static class Program
    {
        public const string Version = "1.1.0";

        // имена — в пределах сеанса пользователя (Local\): у каждого пользователя Windows свой виджет
        const string MutexName = @"Local\WeatherWidget.Running", WakeName = @"Local\WeatherWidget.Show";

        [STAThread]
        static void Main()
        {
            // Одна копия: два виджета писали бы в один settings.txt (например, автозапуск + запуск exe вручную).
            // Вторая копия только показывает первую и закрывается.
            bool first;
            using (Mutex running = new Mutex(true, MutexName, out first))
            {
                if (!first) { WakeFirstCopy(); return; }
                Run();
            }
        }

        static void Run()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            // по умолчанию .NET Framework держит не больше 2 соединений с сервером — остальные запросы
            // (погода, поиск, запасной источник) стояли бы в очереди за зависшим
            ServicePointManager.DefaultConnectionLimit = 8;

            // ошибка в отрисовке или обработчике не закрывает виджет молча — она попадает в error.log
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { ErrorLog.Write(ErrorLog.DefaultPath, e.Exception, DateTime.Now); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                ErrorLog.Write(ErrorLog.DefaultPath, e.ExceptionObject as Exception, DateTime.Now);
            };

            L.Use(L.SystemDefault());   // язык и единицы из настроек (если выбраны в меню) применит WeatherForm
            Units.Use(Units.SystemDefault());
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (EventWaitHandle wake = new EventWaitHandle(false, EventResetMode.AutoReset, WakeName))
            using (WeatherForm form = new WeatherForm())
            {
                form.EnableTray();
                // вторая копия будит первую — показать виджет
                Thread waiter = new Thread(delegate()
                {
                    while (true) { wake.WaitOne(); form.SafeInvoke(delegate { form.ShowWidget(); }); }
                });
                waiter.IsBackground = true;
                waiter.Start();
                Application.Run(form);
            }
        }

        static void WakeFirstCopy()
        {
            try
            {
                NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);   // иначе Windows не даст окну выйти вперёд
                using (EventWaitHandle wake = EventWaitHandle.OpenExisting(WakeName)) wake.Set();
            }
            catch { }   // первая копия ещё запускается или уже закрылась
        }
    }
}
