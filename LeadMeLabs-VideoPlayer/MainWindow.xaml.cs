using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LeadMeLabs_VideoPlayer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public sealed partial class MainWindow : INotifyPropertyChanged
{
    //Maintain a static reference to the media player so the Manager class can interact with it.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public static MediaElement MediaElementInstance { get; private set; }
	public static MainWindow MainWindowInstance {  get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	public MainWindow()
	{
		InitializeComponent();
		InitialiseMediaElement();
		DataContext = this;
		MainWindowInstance = this;
		Topmost = true;

		MediaControls.Visibility = Visibility.Collapsed;
		
		// Subscribe to the PreviewKeyDown event
		PreviewKeyDown += MainWindow_PreviewKeyDown;
	}

	#region MediaElement Setup
	private void InitialiseMediaElement()
    {
		MediaElementInstance = VideoPlayer;
		VideoPlayer.MediaOpened += VideoPlayer_MediaOpened;
		VideoPlayer.MediaEnded += MediaElement_MediaEnded;

		DispatcherTimer timer = new()
		{
			Interval = TimeSpan.FromSeconds(1)
		};
		timer.Tick += Timer_Tick;
		timer.Start();
	}

	private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
	{
		// Set the maximum value of the slider to the total duration of the video
		VideoSlider.Maximum = VideoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
	}

	private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
	{
		// Set the Position to the beginning of the media
		VideoPlayer.Position = TimeSpan.Zero;

		// Check if repeat is on
		if (IsRepeat)
		{
			// Play the media again
			VideoPlayer.Play();
		} else
        {
			VideoPlayer.Pause();
        }
	}

	private void Timer_Tick(object? sender, EventArgs e)
	{
		if (VideoPlayer.Source != null)
		{
			if (!VideoPlayer.NaturalDuration.HasTimeSpan) return;
			
			LblStatus.Content = $"{VideoPlayer.Position:mm\\:ss} / {VideoPlayer.NaturalDuration.TimeSpan:mm\\:ss}";

			// Update the slider value to reflect the current video position
			VideoSlider.Value = VideoPlayer.Position.TotalSeconds;
		}
		else
		{
			LblStatus.Content = "No file selected...";
		}
	}

	private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		// Check if the Escape key is pressed
		if (e.Key != Key.Escape) return;
		
		WindowStyle = WindowStyle.SingleBorderWindow;
		WindowState = WindowState.Normal;
	}
	#endregion

	#region Media Controls
	private bool _isRepeat = true;

	public bool IsRepeat
	{
		get => _isRepeat;
		set
		{
			if (_isRepeat == value) return;
			_isRepeat = value;
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
			OnPropertyChanged(nameof(IsMuted));
		}
	}

	// Implement the INotifyPropertyChanged interface
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	private void BtnOpen_Click(object sender, RoutedEventArgs e)
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

		// Set the MediaElement source to the selected file
		VideoPlayer.Source = new Uri(selectedFilePath);

		// Play the video
		VideoPlayer.Play();
	}

	/// <summary>
	/// Stop the media element's current source and then set the source
	/// to null to produce a black screen again.
	/// </summary>
	private void BtnStop_Click(object sender, RoutedEventArgs e)
	{
		// Stop the media playback
		VideoPlayer.Stop();

		// Set the MediaElement source to null or an empty Uri
		VideoPlayer.Source = null;
	}

	private void BtnPlay_Click(object sender, RoutedEventArgs e)
	{
		VideoPlayer.Play();
	}

	private void BtnPause_Click(object sender, RoutedEventArgs e)
	{
		VideoPlayer.Pause();
	}

	/// <summary>
	/// Maximise the application window or return the application to normal.
	/// </summary>
	private void BtnFullscreen_Click(object sender, RoutedEventArgs e)
	{
		if (WindowState == WindowState.Normal)
		{
			WindowStyle = WindowStyle.None;
			WindowState = WindowState.Maximized;
			this.Activate();
		}
		else
		{
			WindowStyle = WindowStyle.SingleBorderWindow;
			WindowState = WindowState.Normal;
		}
	}

	private void BtnRepeat_Click(object sender, RoutedEventArgs e)
	{
		Trace.WriteLine("Repeat: " + IsRepeat);
		IsRepeat = !IsRepeat;
	}
	
	private void BtnMuted_Click(object sender, RoutedEventArgs e)
	{
		Trace.WriteLine("Mute: " + IsMuted);
		IsMuted = !IsMuted;
	}

	private void Window_MouseMove(object sender, MouseEventArgs e)
	{
		// Get the mouse position relative to the window
		Point mousePosition = e.GetPosition(this);

		// Check if the mouse is near the bottom of the screen (adjust the threshold as needed)
		double bottomThreshold = ActualHeight - 100; // 50 is the threshold value
		
		// Show/Hide the StackPanel
		MediaControls.Visibility = mousePosition.Y > bottomThreshold ? Visibility.Visible : Visibility.Collapsed;
	}
    #endregion

    #region Slider Controls
    private void VideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		// Update the video player's position based on the slider value
		VideoPlayer.Position = TimeSpan.FromSeconds(VideoSlider.Value);
	}

	private void Slider_DragStarted(object sender, DragStartedEventArgs e)
	{
		// Pause the video playback when the slider dragging starts
		VideoPlayer.Pause();
	}

	private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
	{
		// Resume video playback and seek to the new position when the slider dragging completes
		VideoPlayer.Play();
		VideoPlayer.Position = TimeSpan.FromSeconds(VideoSlider.Value);
	}

	private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		Point clickPosition = e.GetPosition(VideoSlider);
		double value = clickPosition.X / VideoSlider.ActualWidth * (VideoSlider.Maximum - VideoSlider.Minimum) + VideoSlider.Minimum;
		VideoSlider.Value = value;
	}

	private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
	{
        if (sender is not Thumb thumb) return;
        if (thumb.TemplatedParent is not Slider slider) return;
        
        double deltaX = e.HorizontalChange;
        double sliderWidth = slider.ActualWidth - thumb.ActualWidth;
        double value = deltaX / sliderWidth * (slider.Maximum - slider.Minimum);
        slider.Value += value;
	}
	#endregion
}
