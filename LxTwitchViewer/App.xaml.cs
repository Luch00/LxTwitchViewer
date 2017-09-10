using CefSharp;
using System;
using System.IO;
using System.Windows;

namespace LxTwitchViewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application/*, ISingleInstanceApp*/
    {

        [STAThread]
        public static void Main()
        {
            CefSettings settings = new CefSettings
            {
                CachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Luch", "LxTwitchViewer"),
                RemoteDebuggingPort = 8087,
            };
            Cef.Initialize(settings);

            var application = new App();
            application.InitializeComponent();
            application.Run();
        }
    }
}
