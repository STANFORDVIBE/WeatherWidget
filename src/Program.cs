using System;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WeatherForm());
        }
    }
}
