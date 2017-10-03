using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace YouTubeTransportControl.Demo
{
	public class MainWindowViewModel : INotifyPropertyChanged
	{
		public MainWindowViewModel()
		{
			TimelineDuration = new TimeSpan(0, 77, 21);
			TimelinePositionString = "Timeline Position = <Not Set>";
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected bool SetField<T>(ref T field, T value, string propertyName)
		{
			if (EqualityComparer<T>.Default.Equals(field, value)) return false;
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}

		private TimeSpan timelinePosition;
		private TimeSpan timelineDuration;

		public TimeSpan TimelinePosition
		{
			get { return timelinePosition; }
			set
			{
				SetField(ref timelinePosition, value, "TimelinePosition");
				TimelinePositionString = String.Format("Timeline Position = {0:00}:{1:00}.{2:000} / {3}",
					TimelinePosition.TotalMinutes,
					TimelinePosition.Seconds,
					TimelinePosition.Milliseconds, 
					TimelineDuration.ToString());
			}
		}

		private string timelinePositionString;

		public string TimelinePositionString
		{
			get { return timelinePositionString; }
			set { SetField(ref timelinePositionString, value, "TimelinePositionString"); }
		}

		public TimeSpan TimelineDuration
		{
			get { return timelineDuration; }
			set { SetField(ref timelineDuration, value, "TimelineDuration"); }
		}
	}
}