using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LeadMeLabs_VideoPlayer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
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
		}

		#region MediaElement Setup
		private void InitialiseMediaElement()
        {
			MediaElementInstance = videoPlayer;
			videoPlayer.MediaOpened += VideoPlayer_MediaOpened;
			videoPlayer.MediaEnded += MediaElement_MediaEnded;

			DispatcherTimer timer = new();
			timer.Interval = TimeSpan.FromSeconds(1);
			timer.Tick += Timer_Tick;
			timer.Start();
		}

		private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
		{
			// Set the maximum value of the slider to the total duration of the video
			videoSlider.Maximum = videoPlayer.NaturalDuration.TimeSpan.TotalSeconds;
		}

		private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
		{
			// Set the Position to the beginning of the media
			videoPlayer.Position = TimeSpan.Zero;

			// Check if repeat is on
			if (IsRepeat)
			{
				// Play the media again
				videoPlayer.Play();
			}
		}

		void Timer_Tick(object? sender, EventArgs e)
		{
			if (videoPlayer.Source != null)
			{
				if (videoPlayer.NaturalDuration.HasTimeSpan)
				{
					lblStatus.Content = String.Format("{0} / {1}", videoPlayer.Position.ToString(@"mm\:ss"), videoPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss"));

					// Update the slider value to reflect the current video position
					videoSlider.Value = videoPlayer.Position.TotalSeconds;
				}
			}
			else
			{
				lblStatus.Content = "No file selected...";
			}
		}
        #endregion

        #region Media Controls
        private bool isRepeat = false;

		public bool IsRepeat
		{
			get { return isRepeat; }
			set
			{
				if (isRepeat != value)
				{
					isRepeat = value;
					OnPropertyChanged(nameof(IsRepeat));
				}
			}
		}

		// Implement the INotifyPropertyChanged interface
		public event PropertyChangedEventHandler? PropertyChanged;

		protected virtual void OnPropertyChanged(string propertyName)
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
			if (result == true)
			{
				// Retrieve the selected file path
				string selectedFilePath = openFileDialog.FileName;

				// Set the MediaElement source to the selected file
				videoPlayer.Source = new Uri(selectedFilePath);

				// Play the video
				videoPlayer.Play();
			}
		}

		/// <summary>
		/// Stop the media element's current source and then set the source
		/// to null to produce a black screen again.
		/// </summary>
		private void BtnStop_Click(object sender, RoutedEventArgs e)
		{
			// Stop the media playback
			videoPlayer.Stop();

			// Set the MediaElement source to null or an empty Uri
			videoPlayer.Source = null;
		}

		private void BtnPlay_Click(object sender, RoutedEventArgs e)
		{
			videoPlayer.Play();
		}

		private void BtnPause_Click(object sender, RoutedEventArgs e)
		{
			videoPlayer.Pause();
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

		private void Window_MouseMove(object sender, MouseEventArgs e)
		{
			// Get the mouse position relative to the window
			Point mousePosition = e.GetPosition(this);

			// Check if the mouse is near the bottom of the screen (adjust the threshold as needed)
			double bottomThreshold = ActualHeight - 100; // 50 is the threshold value
			if (mousePosition.Y > bottomThreshold)
			{
				// Show the StackPanel
				mediaControls.Visibility = Visibility.Visible;
			}
			else
			{
				// Hide the StackPanel
				mediaControls.Visibility = Visibility.Collapsed;
			}
		}
        #endregion

        #region Slider Controls
        private void VideoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			// Update the video player's position based on the slider value
			videoPlayer.Position = TimeSpan.FromSeconds(videoSlider.Value);
		}

		private void Slider_DragStarted(object sender, DragStartedEventArgs e)
		{
			// Pause the video playback when the slider dragging starts
			videoPlayer.Pause();
		}

		private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
		{
			// Resume video playback and seek to the new position when the slider dragging completes
			videoPlayer.Play();
			videoPlayer.Position = TimeSpan.FromSeconds(videoSlider.Value);
		}

		private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			Point clickPosition = e.GetPosition(videoSlider);
			double value = clickPosition.X / videoSlider.ActualWidth * (videoSlider.Maximum - videoSlider.Minimum) + videoSlider.Minimum;
			videoSlider.Value = value;
		}

		private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
		{
            if (sender is not Thumb thumb) return;
            if (thumb?.TemplatedParent is not Slider slider) return;

            if (slider != null)
			{
				double deltaX = e.HorizontalChange;
				double sliderWidth = slider.ActualWidth - thumb.ActualWidth;
				double value = deltaX / sliderWidth * (slider.Maximum - slider.Minimum);
				slider.Value += value;
			}
		}
		#endregion
	}
}
