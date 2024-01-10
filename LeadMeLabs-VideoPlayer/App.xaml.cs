using System;
using System.Windows;
using LeadMeLabs_VideoPlayer.MVC.Controller;
using LeadMeLabs_VideoPlayer.MVC.View;
using Sentry;

namespace LeadMeLabs_VideoPlayer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        InitSentry();

        MainWindow wnd = new();
        wnd.Show();
        
        wnd.WindowStyle = WindowStyle.None;
        wnd.WindowState = WindowState.Maximized;
        
        // Start up the pipe server to receive commands from LeadMe Labs
        Controller.InitialiseManager();

        // Check for any arguments, [0] - generic cmd (always present), [1] - source, [2] - should repeat
        string[] args = Environment.GetCommandLineArgs();

        if (args.Length > 1)
        {
            wnd.WindowStyle = WindowStyle.None;
            wnd.WindowState = WindowState.Maximized;

            wnd.VideoPlayer.Source = new Uri(args[1]);
            wnd.VideoPlayer.Play();
        }

        if (args.Length <= 2) return;
        wnd.IsRepeat = args[2].Equals("true");
        
        if (args.Length <= 3) return;
        wnd.IsMuted = args[3].Equals("true");
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
