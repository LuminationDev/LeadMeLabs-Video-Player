using System;
using System.Diagnostics;
using System.Windows;
using Sentry;

namespace LeadMeLabs_VideoPlayer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            InitSentry();

            MainWindow wnd = new();
            wnd.Show();
            
            // Start up the pipe server to recieve commands from LeadMe Labs
            Manager.InitialiseManager();

            string[] args = Environment.GetCommandLineArgs();

            if (args.Length > 1)
            {
                wnd.WindowStyle = WindowStyle.None;
                wnd.WindowState = WindowState.Maximized;

                wnd.videoPlayer.Source = new Uri(args[1]);
                wnd.videoPlayer.Play();
            }

            if (args.Length > 2)
            {
                if (args[2].Equals("true"))
                {
                    wnd.IsRepeat = true;
                }
            }
        }

        private void InitSentry()
        {
            SentrySdk.Init(options =>
            {
                options.Dsn = "https://9b87fd592fb64ef7ad64c180fc560ef6@o1294571.ingest.sentry.io/4505264495984640";
                options.Debug = false;
                options.TracesSampleRate = 0.1;

                // This option is recommended. It enables Sentry's "Release Health" feature.
                options.AutoSessionTracking = true;
                options.IsGlobalModeEnabled = true;
                options.EnableTracing = true;
            });
        }
    }
}
