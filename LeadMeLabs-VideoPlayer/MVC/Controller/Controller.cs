using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using leadme_api;
using LeadMeLabs_VideoPlayer.MVC.View;
using Sentry;
using Action = leadme_api.Action;

namespace LeadMeLabs_VideoPlayer.MVC.Controller;

public static class Controller
{
    //List of the valid file types to try and load
    private static readonly List<string> ValidFileTypes = new() { ".mp4" };

    //Path to the specialised LeadMe video folder
    private static readonly string FolderPath = GetVideoFolder();

    private static string GetVideoFolder()
    {
        string videosFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        
        return videosFolderPath;
    }

    /// <summary>
    /// Load any local video files and send them to a ParentPipeServer. Also Start the pipe server for the 
    /// local application using the leadme_api.dll.
    /// </summary>
    public static void InitialiseManager()
    {
        LoadLocalVideoFiles();
        PipeServer.Run(LogHandler, PauseHandler, ResumeHandler, ShutdownHandler, DetailsHandler, ActionHandler);
    }

    #region Pipe Server
    /// <summary>
    /// Stop the current pipe server, wait for a set period and then restart the server.
    /// </summary>
    public static void RestartPipeServer()
    {
        PipeServer.Close();

        Task.Delay(2000).Wait();

        InitialiseManager();
    }

    /// <summary>
    /// Handle any error messages from the internal pipe server.
    /// </summary>
    private static void LogHandler(string message)
    {
        //For now just disregard as there is nowhere to display the messages.
        Trace.WriteLine(message);
    }

    /// <summary>
    /// Pause the MediaPlayer, if there is no source set this will do nothing.
    /// </summary>
    private static void PauseHandler()
    {
        Application.Current.Dispatcher.Invoke(delegate
        {
            MainWindow.MediaElementInstance.Pause();
        });
    }

    /// <summary>
    /// Resume the MediaPlayer, if there is no source set this will do nothing.
    /// </summary>
    private static void ResumeHandler()
    {
        Application.Current.Dispatcher.Invoke(delegate
        {
            MainWindow.MediaElementInstance.Play();
        });
    }

    /// <summary>
    /// Quit the current application. Performing any checks or required functions before exiting.
    /// </summary>
    private static void ShutdownHandler()
    {
        Application.Current.Dispatcher.Invoke(delegate
        {
            Application.Current.Shutdown();
        });
    }

    private static void DetailsHandler()
    {

    }

    /// <summary>
    /// Perform an action that has been received from the pipe server. This could be a media action or 
    /// setting a new source.
    /// </summary>
    /// <param name="message">A string containing action space and a value separated by a ':'</param>
    private static void ActionHandler(string message)
    {
        string[] tokens = message.Split(',', 2);

        if (tokens.Length < 2)
            return;

        string action = tokens[1];

        Application.Current.Dispatcher.Invoke(delegate
        {
            switch (tokens[0])
            {
                case "media":
                    HandleMediaAction(action);
                    break;

                case "source":
                    LoadSource(action);
                    break;

                case "window":
                    HandleWindowAction(action);
                    break;
            }
        });
    }

    /// <summary>
    /// Handle an action that is related to the MediaElement within the Main window.
    /// </summary>
    private static void HandleMediaAction(string action)
    {
        switch (action)
        {
            case "stop":
                MainWindow.MediaElementInstance.Stop();
                MainWindow.MediaElementInstance.Source = null;
                break;
            case "repeat":
                MainWindow.MainWindowInstance.IsRepeat = !MainWindow.MainWindowInstance.IsRepeat;
                break;
            case "mute":
                MainWindow.MainWindowInstance.IsMuted = true;
                break;
            case "unmute":
                MainWindow.MainWindowInstance.IsMuted = false;
                break;
        }
    }

    /// <summary>
    /// Change the current source of the MediaElement to the supplied one.
    /// </summary>
    private static void LoadSource(string selectedFilePath)
    {
        try
        {
            MainWindow.MediaElementInstance.Position = TimeSpan.Zero;
            MainWindow.MediaElementInstance.Source = new Uri(selectedFilePath);
            MainWindow.MediaElementInstance.Play();
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureMessage("Unable to load file from (" + selectedFilePath + "), Error: " + ex);
        }
    }

    /// <summary>
    /// Handle an action that is related to the Main window of the application.
    /// </summary>
    private static void HandleWindowAction(string action)
    {
        switch (action)
        {
            case "fullscreen":
                Fullscreen();
                break;
        }
    }

    /// <summary>
    /// Toggle the main window between full screen and normal. Depending on what the latest value of the
    /// window state is.
    /// </summary>
    private static void Fullscreen()
    {
        if (Application.Current.MainWindow == null) return;
        
        if (Application.Current.MainWindow.WindowState == WindowState.Normal)
        {
            Application.Current.MainWindow.WindowStyle = WindowStyle.None;
            Application.Current.MainWindow.WindowState = WindowState.Maximized;
            Application.Current.MainWindow.Activate();
        }
        else
        {
            Application.Current.MainWindow.WindowState = WindowState.Normal;
            Application.Current.MainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
        }
    }
    #endregion

    #region Setup
    //A Details class that represents all the information about the experience. This can be sent over
    //a network as a string with the use of the Details Serialize function.
    private static readonly Details Details = new Details
    {
        name = "Video Player",
        globalActions = new List<GlobalAction>
        {
            new GlobalAction { name = "Pause", trigger = "pause" },
            new GlobalAction { name = "Resume", trigger = "resume" },
            new GlobalAction { name = "Shutdown", trigger = "shutdown" }
        },
        levels = new List<Level>
        {
            new Level
            {
                name = "Options",
                trigger = "",
                actions = new List<Action>
                {
                    new Action { name = "Fullscreen", trigger = "window,fullscreen" },
                    new Action { name = "Repeat", trigger = "media,repeat" },
                    new Action { name = "Mute", trigger = "media,mute" },
                    new Action { name = "Unmute", trigger = "media,unmute" }
                }
            },
            new Level
            {
                name = "Sources",
                trigger = "",
                actions = new List<Action>()
            }
        }
    };

    /// <summary>
    /// Load any video files that are in the local Video folder, adding these to the details object
    /// before sending the details object to LeadMe Labs.
    /// </summary>
    private static async void LoadLocalVideoFiles()
    {
        string[] files = Directory.GetFiles(FolderPath);

        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            if (ValidFileTypes.Contains(Path.GetExtension(filePath)))
            {
                // Add to the details being sent to LeadMe
                Details.levels[1].actions.Add(new Action { name = fileName, trigger = $"source,file://{filePath}" });
            }
        }

        // Wait while LeadMe updates the game name
        await Task.Delay(3000);

        // Send the experience details on start up
        ParentPipeClient.Send(LogHandler, Details.Serialize(Details));
    }
    #endregion
}
