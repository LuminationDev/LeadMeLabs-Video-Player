using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using LeadMeLabs_VideoPlayer.Core;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace LeadMeLabs_VideoPlayer.MVC.View;

public enum PlaybackState
{
	Playing,
	Paused,
	Stopped
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public sealed partial class MainWindow: INotifyPropertyChanged
{
    //Maintain a static reference to the media player so the Manager class can interact with it.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public static MediaElement MediaElementInstance { get; private set; }
	public static MainWindow MainWindowInstance {  get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	public RelayCommand OpenFileCommand { get; }
	public RelayCommand StopCommand { get; }
	public RelayCommand TogglePlayPauseCommand { get; }
	public RelayCommand FullscreenCommand { get; }
	public RelayCommand RepeatCommand { get; }
	public RelayCommand MutedCommand { get; }
	public RelayCommand RewindCommand { get; }
	public RelayCommand SkipCommand { get; }
	
	public MainWindow()
	{
		InitializeComponent();
		InitialiseMediaElement();
		InitialiseMediaVisibilityTimer();
		DataContext = this;
		MainWindowInstance = this;
		Topmost = true;

		MediaControls.Visibility = Visibility.Collapsed;
		
		// Subscribe to the PreviewKeyDown event
		PreviewKeyDown += MainWindow_PreviewKeyDown;
		
		OpenFileCommand = new RelayCommand(_ => OpenFile());
		StopCommand = new RelayCommand(_ => StopVideo());
		TogglePlayPauseCommand = new RelayCommand(_ => TogglePlayPause());
		FullscreenCommand = new RelayCommand(_ => FullScreen());
		RepeatCommand = new RelayCommand(_ => RepeatVideo());
		MutedCommand = new RelayCommand(_ => MutedVideo());
		RewindCommand = new RelayCommand(_ => ChangeTime(false));
		SkipCommand = new RelayCommand(_ => ChangeTime(true));
	}
	
	#region Observers
	private bool _isControlVisible;
	public bool IsControlVisible
	{
		get => _isControlVisible;
		set
		{
			if (_isControlVisible == value) return;
			_isControlVisible = value;
			OnPropertyChanged(nameof(IsControlVisible));
		}
	}
	
	private bool _isPlaying;
	public bool IsPlaying
	{
		get => _isPlaying;
		set
		{
			if (_isPlaying == value) return;
			_isPlaying = value;
			OnPropertyChanged(nameof(IsPlaying));
		}
	}
	
	private bool _isRepeat = true;
	public bool IsRepeat
	{
		get => _isRepeat;
		set
		{
			if (_isRepeat == value) return;
			_isRepeat = value;
			UpdateVideoDetails("isRepeat", value);
			OnPropertyChanged(nameof(IsRepeat));
		}
	}
	
	private bool _isMuted;
	public bool IsMuted
	{
		get => _isMuted;
		set
		{
			if (_isMuted == value) return;
			VideoPlayer.IsMuted = value;
			_isMuted = value;
			UpdateVideoDetails("isMuted", value);
			OnPropertyChanged(nameof(IsMuted));
		}
	}
	
	private bool _fullScreen = true;
	public bool IsFullScreen
	{
		get => _fullScreen;
		set
		{
			if (_fullScreen == value) return;
			_fullScreen = value;
			UpdateVideoDetails("fullScreen", value);
			OnPropertyChanged(nameof(IsFullScreen));
		}
	}
	
	private string _videoTime = "Not playing...";
	public string VideoTime
	{
		get => _videoTime;
		set
		{
			if (_videoTime == value) return;
			_videoTime = value;
			OnPropertyChanged(nameof(VideoTime));
		}
	}

	/**
	 * An object that holds various details about the Video Player that are not included in the primary function. If any
	 * of them are updated the whole object is send to the tablet, this way extra details can be added at anytime.
	 * This can include:
	 *		_isRepeat
	 *		_isMuted
	 *		_fullScreen
	 *		videoState - the current playback state
	 */
	private static JObject _videoDetails = new(
			new JProperty("isRepeat", true),
			new JProperty("isMuted", false),
			new JProperty("fullScreen", true),
			new JProperty("videoState", "")
		);
	
	private static JObject VideoDetails
	{
		get => _videoDetails;
		set
		{
			if (_videoDetails.ToString() == value.ToString()) return;
			_videoDetails = value;

			Controller.Controller.SendMessage("videoPlayerDetails," + _videoDetails);
		}
	}

	/// <summary>
	/// Update a key's value in the VideoDetails object, the object requires a deep clone and being set again
	/// as a full object instead of just changing a single value otherwise the observer pattern won't be triggered.
	/// </summary>
	/// <param name="key">A string of the token to change</param>
	/// <param name="value">A JToken of the new value</param>
	public static void UpdateVideoDetails(string key, JToken value)
	{
		JToken details = VideoDetails.DeepClone();
		details[key] = value;
		VideoDetails = (JObject)details;
	}

	//Implement the INotifyPropertyChanged interface
	public event PropertyChangedEventHandler? PropertyChanged;
 
	private void OnPropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
	#endregion

	#region MediaElement Setup
	private DispatcherTimer? _videoDurationTimer;
	
	private void InitialiseMediaElement()
    {
		MediaElementInstance = VideoPlayer;
		VideoPlayer.MediaOpened += VideoPlayer_MediaOpened;
		VideoPlayer.MediaEnded += MediaElement_MediaEnded;

		InitialiseTickTimer();
    }

	private void InitialiseTickTimer()
	{
		_videoDurationTimer = new()
		{
			Interval = TimeSpan.FromSeconds(1)
		};
		_videoDurationTimer.Tick += Timer_Tick;
		_videoDurationTimer.Start();
	}

	private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
	{
		// Set the maximum value of the slider to the total duration of the video
		VideoSlider.Maximum = VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;

		IsPlaying = true;
	}

	private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
	{
		_videoDurationTimer?.Stop();
		
		// Set the Position to the beginning of the media
		VideoPlayer.Position = TimeSpan.Zero;

		// Check if repeat is on
		if (IsRepeat)
		{
			// Play the media again
			VideoPlayer.Play();
			InitialiseTickTimer();
		} else
        {
			VideoPlayer.Pause();
			IsPlaying = false;
        }
	}
	
	/// <summary>
	/// Handles the tick event of a timer, updating the displayed video time and slider value.
	/// </summary>
	private void Timer_Tick(object? sender, EventArgs e)
	{
		if (VideoPlayer.Source != null)
		{
			if (!VideoPlayer.NaturalDuration.HasTimeSpan) return;
			
			VideoTime = $"{VideoPlayer.Position:mm\\:ss} / {VideoPlayer.NaturalDuration.TimeSpan:mm\\:ss}";

			// Update the slider value to reflect the current video position
			VideoSlider.Value = VideoPlayer.Position.TotalSeconds;
			
			//TODO if the pipe server is not sending messages then this may backup??
			// Send a message through the pipe server to update the current time
			int totalSeconds = Convert.ToInt32(VideoPlayer.Position.TotalSeconds);
			Controller.Controller.SendMessage("videoTime," + totalSeconds);
		}
		else
		{
			VideoTime = "No file selected...";
		}
	}

	/// <summary>
	/// Handles the preview key down event for the main window, checking for the Escape key.
	/// If the Escape key is pressed, restores the window to single border and normal state.
	/// </summary>
	private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		// Check if the Escape key is pressed
		if (e.Key != Key.Escape) return;
		
		WindowStyle = WindowStyle.SingleBorderWindow;
		WindowState = WindowState.Normal;
	}
	#endregion
	
	#region Media View
	private DispatcherTimer? _visibilityTimer;
	private Point _lastMousePosition;

	/// <summary>
	/// Initializes and configures a timer to control the visibility of media controls.
	/// </summary>
	private void InitialiseMediaVisibilityTimer()
	{
		// Initialize and configure the DispatcherTimer
		_visibilityTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(2)
		};
		_visibilityTimer.Tick += VisibilityTimer_Tick;
		
		// Set the initial mouse position
		_lastMousePosition = Mouse.GetPosition(this);
	}
	
	/// <summary>
	/// Handles the tick event of the visibility timer, hiding the media controls and stopping the timer.
	/// </summary>
	private async void VisibilityTimer_Tick(object? sender, EventArgs e)
	{
		// Update the visibility binding
		IsControlVisible = false;
		
		// Introduce a delay using Task.Delay
		await Task.Delay(300);
		
		// Timer elapsed, hide the MediaControls
		MediaControls.Visibility = Visibility.Collapsed;
		
		// Stop the timer
		_visibilityTimer?.Stop();
	}
	
	/// <summary>
	/// Handles the mouse move event of the window, controlling the visibility of media controls.
	/// If the mouse has not moved for a short duration, hides the controls; otherwise, makes them visible.
	/// </summary>
	private void Window_MouseMove(object sender, MouseEventArgs e)
	{
		// Get the current mouse position
		Point currentMousePosition = Mouse.GetPosition(this);

		bool hasMoved = !(Math.Abs(currentMousePosition.X - _lastMousePosition.X) > 1) ||
		                !(Math.Abs(currentMousePosition.Y - _lastMousePosition.Y) > 1);
		
		// Update the last mouse position
		_lastMousePosition = currentMousePosition;
		
		// Check if the mouse has moved a little bit before starting the timer
		if (hasMoved)
		{
			_lastMousePosition = currentMousePosition;
			return;
		}
		
		MediaControls.Visibility = Visibility.Visible;
		
		// Update the visibility binding
		IsControlVisible = true;

		// Reset the timer on mouse move
		_visibilityTimer?.Stop();
		_visibilityTimer?.Start();
	}
	#endregion

	#region Media Controls
	/// <summary>
	/// Changes the playback position of the video by skipping forward or rewinding by 10 seconds.
	/// </summary>
	/// <param name="skip">True to skip forward, false to rewind.</param>
	public void ChangeTime(bool skip)
	{
		if (VideoPlayer.Source == null) return;

		if (skip)
		{
			double skipSeconds = 30;
			
			// Check if skipping 30 seconds would go beyond the video's duration
			if (VideoPlayer.Position.TotalSeconds + skipSeconds < VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds)
			{
				VideoPlayer.Position = VideoPlayer.Position.Add(TimeSpan.FromSeconds(skipSeconds));
			}
			else
			{
				// Adjust to the end of the video
				VideoPlayer.Position = VideoPlayer.NaturalDuration.TimeSpan;
			}
		}
		else
		{
			double skipSeconds = 10;
			
			// Check if rewinding 10 seconds would go before the start of the video
			if (VideoPlayer.Position.TotalSeconds - skipSeconds > 0)
			{
				VideoPlayer.Position = VideoPlayer.Position.Subtract(TimeSpan.FromSeconds(skipSeconds));
			}
			else
			{
				// Adjust to the start of the video
				VideoPlayer.Position = TimeSpan.Zero;
			}
		}
	}
	
	/// <summary>
	/// Opens a file dialog to allow the user to select a video file and sets it as the source for the MediaElement.
	/// </summary>
	private void OpenFile()
	{
        // Create an instance of the OpenFileDialog
        OpenFileDialog openFileDialog = new()
        {
            // Set the initial directory (optional)
            InitialDirectory = @"C:\",
 
            // Set the file filter (optional)
            Filter = "Video Files (*.mp4;*.avi)|*.mp4;*.avi|All Files (*.*)|*.*"
        };
 
        // Show the dialog and wait for the user's selection
        bool? result = openFileDialog.ShowDialog();
 
		// Process the selected file
		if (result != true) return;
		
		// Retrieve the selected file path
		string selectedFilePath = openFileDialog.FileName;

		// Load the video and send back the new (current) source
		LoadVideo(selectedFilePath);
	}

	/// <summary>
	/// A centralised function to load a video source into the media player. Onload the source is then sent via the
	/// pipe server to LeadMe (if operating) to update the current source information.
	/// </summary>
	/// <param name="filePath"></param>
	public static void LoadVideo(string filePath)
	{
		// Set the time position to zero
		MediaElementInstance.Position = TimeSpan.Zero;
		
		// Set the MediaElement source to the selected file
		MediaElementInstance.Source = new Uri(filePath);
		
		// Play the video
		MediaElementInstance.Play();
		
		// Check if the string starts with "file://"
		if (filePath.StartsWith("file://"))
		{
			// Remove "file://" prefix
			filePath = filePath.Substring(7); // Remove the first 7 characters
		}
		
		// Send a message through the pipe server to update the currently selected source
		Controller.Controller.SendMessage($"videoActive,{filePath}");
		
		// Send a message through the pipe server to update the current playback state
		UpdateVideoDetails("videoState", PlaybackState.Playing.ToString());
	}
 
	/// <summary>
	/// Stop the media element's current source and then set the source
	/// to null to produce a black screen again.
	/// </summary>
	private void StopVideo()
	{
		// Stop the media playback
		VideoPlayer.Stop();
		VideoPlayer.Position = TimeSpan.Zero;
		
		// Set the MediaElement back to 0
		VideoSlider.Value = 0;
		IsPlaying = false;
		
		// Send a message through the pipe server to update the current state
		UpdateVideoDetails("videoState", PlaybackState.Stopped.ToString());
	}
	
	/// <summary>
	/// Toggles the play/pause state of the video player.
	/// If the video is playing, pauses it; if paused, resumes playback.
	/// </summary>
	private void TogglePlayPause()
	{
		if (VideoPlayer.Source == null) return;
		
		if (IsPlaying)
		{
			VideoPlayer.Pause();
			IsPlaying = false;
		}
		else
		{
			VideoPlayer.Play();
			IsPlaying = true;
		}
		
		PlaybackState state = IsPlaying ? PlaybackState.Playing : PlaybackState.Paused;
		// Send a message through the pipe server to update the current state
		UpdateVideoDetails("videoState", state.ToString());
	}
 
	/// <summary>
	/// Maximise the application window or return the application to normal.
	/// </summary>
	private void FullScreen()
	{
		if (WindowState == WindowState.Normal)
		{
			WindowStyle = WindowStyle.None;
			WindowState = WindowState.Maximized;
			this.Activate();
			IsFullScreen = true;
		}
		else
		{
			WindowStyle = WindowStyle.SingleBorderWindow;
			WindowState = WindowState.Normal;
			IsFullScreen = false;
		}
	}
 
	/// <summary>
	/// Toggle the IsRepeat variable to the opposite of what it current is. This represents if a video should loop at
	/// the end or stop playing.
	/// </summary>
	private void RepeatVideo()
	{
		IsRepeat = !IsRepeat;
	}
	
	/// <summary>
	/// Toggle the IsMuted variable to the opposite of what it current is. This represents if a video should play sound
	/// or be muted.
	/// </summary>
	private void MutedVideo()
	{
		IsMuted = !IsMuted;
	}
	#endregion

    #region Slider Controls
    /// <summary>
    /// Updates the video player's position based on the changed value of the slider.
    /// </summary>
    private void VideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		// Update the video player's position based on the slider value
		VideoPlayer.Position = TimeSpan.FromSeconds(VideoSlider.Value);
	}

    /// <summary>
    /// Pauses the video playback when the slider dragging starts.
    /// </summary>
	private void Slider_DragStarted(object sender, DragStartedEventArgs e)
	{
		// Pause the video playback when the slider dragging starts
		VideoPlayer.Pause();
	}

    /// <summary>
    /// Resumes video playback and seeks to the new position when the slider dragging completes.
    /// </summary>
	private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
	{
		// Resume video playback and seek to the new position when the slider dragging completes
		VideoPlayer.Play();
		VideoPlayer.Position = TimeSpan.FromSeconds(VideoSlider.Value);
	}

    /// <summary>
    /// Updates the slider value based on the mouse click position and initiates seeking in the video.
    /// </summary>
	private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (VideoPlayer.Source == null) return;
		
		Point clickPosition = e.GetPosition(VideoSlider);
		double value = clickPosition.X / VideoSlider.ActualWidth * (VideoSlider.Maximum - VideoSlider.Minimum) + VideoSlider.Minimum;
		VideoSlider.Value = value;
	}

    /// <summary>
    /// Adjusts the slider value based on the thumb drag and updates the video player's position.
    /// </summary>
	private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
	{
		if (VideoPlayer.Source == null) return;
		
        if (sender is not Thumb thumb) return;
        if (thumb.TemplatedParent is not Slider slider) return;
        
        double deltaX = e.HorizontalChange;
        double sliderWidth = slider.ActualWidth - thumb.ActualWidth;
        double value = deltaX / sliderWidth * (slider.Maximum - slider.Minimum);
        slider.Value += value;
	}
	#endregion
}
