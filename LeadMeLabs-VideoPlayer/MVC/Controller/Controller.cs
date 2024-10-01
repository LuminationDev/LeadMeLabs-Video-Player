using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using leadme_api;
using LeadMeLabs_VideoPlayer.MVC.View;
using Sentry;
using MediaInfo;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Action = leadme_api.Action;

namespace LeadMeLabs_VideoPlayer.MVC.Controller;

public static class Controller
{
    //List of the valid file types to try and load
    private static readonly List<string> ValidFileTypes = new() { ".mp4", ".avi", ".vlc" };

    private static Timer? _syncTimer;

    //Path to the specialised LeadMe video folder (only loads non-VR videos)
    private static readonly string FolderPath = Path.Join(GetVideoFolder(), "Regular");

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
        PipeServer.Run(LogHandler, PauseHandler, ResumeHandler, ShutdownHandler, DetailsHandler, ActionHandler);
        LoadLocalVideoFiles();
    }

    #region Pipe Server

    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void SendMessage(string message)
    {
        ParentPipeClient.Send(LogHandler, message);
        
        // Wait a small delay before sending the next message
        Task.Delay(500).Wait();
    }
    
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
        MainWindow.UpdateVideoDetails("videoState", PlaybackState.Paused.ToString());
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
        MainWindow.UpdateVideoDetails("videoState", PlaybackState.Playing.ToString());
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
                
                case "time":
                    HandleTimeAction(action);
                    break;
                
                case "source":
                    LoadSource(action);
                    break;

                case "window":
                    HandleWindowAction(action);
                    break;
                
                case "sync":
                    HandleSync(action);
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
                MainWindow.MediaElementInstance.Position = TimeSpan.Zero;
                MainWindow.MainWindowInstance.VideoSlider.Value = 0;
                MainWindow.UpdateVideoDetails("videoState", PlaybackState.Stopped.ToString());
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
            case "skipForwards":
                MainWindow.MainWindowInstance.ChangeTime(true);
                break;
            case "skipBackwards":
                MainWindow.MainWindowInstance.ChangeTime(false);
                break;
        }
    }

    /**
     * An incoming message is asking to modify the time of the current video. Update the media player to reflect this
     * new time if possible.
     */
    private static void HandleTimeAction(string time)
    {
        try
        {
            int value = int.Parse(time);
            MainWindow.MediaElementInstance.Position = TimeSpan.FromSeconds(value);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not parse: {time}. {e}");
        }
    }

    /// <summary>
    /// Change the current source of the MediaElement to the supplied one.
    /// </summary>
    private static void LoadSource(string selectedFilePath)
    {
        try
        {
            MainWindow.LoadVideo(selectedFilePath);
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
    /// 
    /// </summary>
    private static void HandleSync(string action)
    {
        Console.WriteLine("About to sync: " + DateTime.Now);
        
        //'time' action has an additional parameter of the time to set it to.
        string[] tokens = action.Split(',', 2);
        
        //if there is not time specified default to 0
        var time = tokens.Length < 2 ? 0 : Convert.ToInt32(tokens[1]);

        // Calculate the initial target time (next 5-second increment)
        DateTime targetTime = GetNext5SecondIncrement();

        // Create and start the synchronization
        System.Action? task = action switch
        {
            "start" =>
                //Start the current video back at 0:00
                () => Application.Current.Dispatcher.Invoke(delegate
                {
                    MainWindow.MainWindowInstance.VideoPlayer.Position = TimeSpan.Zero;
                }),
            "time" =>
                //Set the current video to X seconds (supplied by the tablet)
                () => Application.Current.Dispatcher.Invoke(delegate
                {
                    MainWindow.MainWindowInstance.VideoPlayer.Position = TimeSpan.FromSeconds(time);
                }),
            _ => null
        };

        if (task == null) return;
        
        Sync(targetTime, _ => DateTime.Now >= targetTime, task);
    }
    
    /// <summary>
    /// Calculates the next 5-second increment with a minimum 1-second delay
    /// based on the current system time.
    /// </summary>
    /// <returns>The calculated target time for the next 5-second increment.</returns>
    private static DateTime GetNext5SecondIncrement()
    {
        DateTime now = DateTime.Now;
        int seconds = now.Second;
        int remainder = seconds % 5;

        // Calculate the next 5-second increment
        int nextIncrement = remainder == 0 ? 5 : 5 - remainder;
        DateTime nextTargetTime = now.AddSeconds(nextIncrement);
        
        // Ensure the target time is at least 1 second in the future
        if ((nextTargetTime - now).TotalSeconds < 1)
        {
            nextTargetTime = nextTargetTime.AddSeconds(5);
        }

        return nextTargetTime;
    }
    
    /// <summary>
    /// Synchronizes the execution of a task at the specified target time using a Timer.
    /// </summary>
    /// <param name="targetTime">The target time for task execution.</param>
    /// <param name="shouldExecute">A condition determining whether the task should be executed.</param>
    /// <param name="task">The task to execute.</param>
    private static void Sync(DateTime targetTime, Func<DateTime, bool> shouldExecute, System.Action task)
    {
        // Calculate the initial delay until the target time
        long initialDelay = (long)(targetTime - DateTime.Now).TotalMilliseconds;

        if (initialDelay < 0)
        {
            return;
        }

        // Set up a timer to run the task at the specified time
        _syncTimer = new Timer(_ =>
        {
            if (!shouldExecute(DateTime.Now)) return;
            
            task();
            _syncTimer?.Dispose(); // Task completed, dispose the timer
        }, null, initialDelay, Timeout.Infinite);
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
            new GlobalAction { name = "Stop", trigger = "stop" },
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
                    new Action { name = "Sync", trigger = "sync,start" },
                    new Action { name = "Sync Time", trigger = "sync,time" },
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
        if (!Directory.Exists(FolderPath)) return;
        
        string[] files = Directory.GetFiles(FolderPath);
        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath);
            if (ValidFileTypes.Contains(extension))
            {
                // Calculate video duration
                int duration = GetVideoDuration(filePath);
                
                // Add to the details being sent to LeadMe
                Details.levels[1].actions.Add(new Action
                {
                    name = fileName, 
                    trigger = $"source,file://{filePath}", 
                    extra = new JArray(
                        new JObject(
                            new JProperty("fileType", extension), 
                            new JProperty("duration", duration)
                        )
                    )
                });
            }
        }
    
        // Wait while LeadMe updates the game name
        await Task.Delay(3000);
    
        // Send the experience details on start up
        SendMessage(Details.Serialize(Details));
    }
    
    /// <summary>
    /// Gets the duration of a video file using the MediaInfo library.
    /// </summary>
    /// <param name="filePath">The path to the video file.</param>
    /// <returns>The duration of the video in seconds, or 0 if unsuccessful.</returns>
    private static int GetVideoDuration(string filePath)
    {
        try
        {
            // Create a logger instance for logging purposes
            ILogger logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<MediaInfoWrapper>();
        
            // Use MediaInfoWrapper to obtain video duration
            var media = new MediaInfoWrapper(filePath, logger);
            return media.Success ? media.Duration : 0;
        }
        catch (Exception ex)
        {
            // Log the exception using Sentry for monitoring purposes
            SentrySdk.CaptureMessage($"Unable to calculate duration from ({filePath}), Error: {ex}");
        }
    
        return 0;
    }
    #endregion
}
