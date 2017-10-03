using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace YouTubeTransportControl
{
	[ToolboxItem(true)]
	[DisplayName("VideoTransportControl")]
	[Description("Controls which allows the user navigate video media. In addition is can display a " +
		"waveform repesenting the audio channels for the loaded video media.")]
	//[TemplatePart(Name = "PART_ThumbCanvas", Type = typeof(Canvas))]
	[TemplatePart(Name = "PART_TimelineCanvas", Type = typeof(Canvas))]
	[TemplatePart(Name = "PART_WaveformCanvas", Type = typeof(Canvas))]
	[TemplatePart(Name = "PART_PreviewCanvas", Type = typeof(Canvas))]
	[TemplatePart(Name = "PART_Thumb", Type = typeof(Ellipse))] 
	[TemplatePart(Name = "PART_ControlGrid", Type = typeof(Grid))]
	public class VideoTransportControl : Control
	{
		private Grid controlGrid;

		private Canvas thumbCanvas;
		private Canvas timelineCanvas;
		private Canvas waveformCanvas;
		private Canvas previewCanvas;

		private Rectangle timelineOuterBox = new Rectangle();
		private Rectangle timelineProgressBox = new Rectangle();
		private Rectangle timelineSelectionBox = new Rectangle();

		private Ellipse timelineThumb = new Ellipse();
		private Path previewWindow = new Path();

		private Point mouseDownPosition;
		private Point currentMousePosition;

		private Slider volumeSlider = null;
		private Canvas volumeCanvas = null;
		private Canvas muteVolumeCanvas = null;
		private Canvas fullScreenCanvas = null;
		private TextBlock ellapsedTimeTextBlock;

		private ResourceDictionary resources;
		private double volumeLevel = 100;

		private const int TIMELINE_ANIMATION_DURATION = 400;
		private const string HIGHLIGHT_FILL = "#878787";

		private double __timelineWidth;
		private bool _isDraggingThumb = false;
		private bool _timelineHovered = false;
		private PreviewType _previewType = PreviewType.None;

		#region Initialization.
		static VideoTransportControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(VideoTransportControl),
				new FrameworkPropertyMetadata(typeof(VideoTransportControl)));
		}

		/// <summary>
		/// When overridden in a derived class, is invoked whenever application code
		/// or internal processes call System.Windows.FrameworkElement.ApplyTemplate().
		/// </summary>
		/// <remarks>
		/// Note, in order to make transparent regions clickable, we set their backgrounds 
		/// to new SolidColorBrush(Colors.Transparent);
		/// </remarks>
		public override void OnApplyTemplate()
		{
			resources = Application.LoadComponent(
				new Uri("/YouTubeTransportControl;component/VideoTransportControlStyle.xaml",
				UriKind.RelativeOrAbsolute)) as ResourceDictionary;

			base.OnApplyTemplate();

			//thumbCanvas = GetTemplateChild("PART_ThumbCanvas") as Canvas;
			//thumbCanvas.Background = new SolidColorBrush(Colors.Transparent);
			//thumbCanvas.Children.Add(timelineThumb);

			timelineThumb = EnforceInstance<Ellipse>("PART_Thumb");
			timelineThumb.MouseLeftButtonDown -= TimelineThumb_MouseLeftButtonDown;
			timelineThumb.MouseLeftButtonDown += TimelineThumb_MouseLeftButtonDown;

			timelineCanvas = GetTemplateChild("PART_TimelineCanvas") as Canvas;
			timelineCanvas.Background = new SolidColorBrush(Colors.Transparent);
			timelineCanvas.Children.Add(timelineOuterBox);
			timelineCanvas.Children.Add(timelineProgressBox);
			timelineCanvas.Children.Add(timelineSelectionBox);
			timelineCanvas.Children.Add(timelineThumb);

			previewCanvas = GetTemplateChild("PART_PreviewCanvas") as Canvas;
			previewCanvas.Background = new SolidColorBrush(Colors.Transparent);
			previewCanvas.Children.Add(previewWindow);

			controlGrid = GetTemplateChild("PART_ControlGrid") as Grid;
			controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			controlGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

			// Add play/pause button.
			Canvas playButton = CreatePlayButton();
			Grid.SetColumn(playButton, 0);
			controlGrid.Children.Add(playButton);

			// Add skip buttons. 
			Canvas skipBackwardButton = CreateBackwardSkipButton();
			Grid.SetColumn(skipBackwardButton, 1);
			controlGrid.Children.Add(skipBackwardButton);

			Canvas skipForwardButton = CreateForwardSkipButton();
			Grid.SetColumn(skipForwardButton, 2);
			controlGrid.Children.Add(skipForwardButton);

			// Volume controls.
			CreateVolumeButton();
			Grid.SetColumn(volumeCanvas, 3);
			controlGrid.Children.Add(volumeCanvas);

			CreateVolumnSlider();
			Grid.SetColumn(volumeSlider, 4);
			controlGrid.Children.Add(volumeSlider);

			// Ellapsed.
			CreateEllapsedTimeTextBlock();
			Grid.SetColumn(ellapsedTimeTextBlock, 5);
			controlGrid.Children.Add(ellapsedTimeTextBlock);

			// Full screen.
			CreateFullScreenButton();
			Grid.SetColumn(fullScreenCanvas, 8);
			controlGrid.Children.Add(fullScreenCanvas);
		}

		/// <summary>
		/// Get element from name. If it exist then element instance return, 
		/// if not, new will be created.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="partName">The part name.</param>
		/// <returns></returns>
		private T EnforceInstance<T>(string partName) where T : FrameworkElement, new()
		{
			return GetTemplateChild(partName) as T ?? new T();
		}

		/// <summary>
		/// Called whenever the control's template changes. 
		/// </summary>
		/// <param name="oldTemplate">The old template</param>
		/// <param name="newTemplate">The new template</param>
		protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
		{
			base.OnTemplateChanged(oldTemplate, newTemplate);

			if (timelineCanvas != null)
				timelineCanvas.Children.Clear();



			SetDefaultMeasurements();
		}
		#endregion // Initialization.

		#region Event Overrides.
		/// <summary>
		/// Raises the SizeChanged event, using the specified information as part of the eventual event data. 
		/// </summary>
		/// <param name="sizeInfo">Details of the old and new size involved in the change.</param>
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			//UpdateWaveformCacheScaling();
			SetDefaultMeasurements();
			UpdateAllRegions();
		}

		/// <summary>
		/// Invoked when an unhandled MouseLeftButtonDown routed event is raised on this element. 
		/// Implement this method to add class handling for this event.
		/// </summary>
		/// <param name="e">The MouseButtonEventArgs that contains the event data. The event 
		/// data reports that the left mouse button was pressed.</param>
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);

			Canvas c = e.OriginalSource as Canvas;
			if (c == null)
				c = Utils.FindParent<Canvas>(e.OriginalSource as FrameworkElement);

			if (c != null)
			{
				mouseDownPosition = e.GetPosition(c);
				if (c.Name == "PART_Thumb")
				{
					Trace.Write("OVERRIDE LEFT MOUSE DOWN on Thumb");
				}
				else if (c.Name == "PART_TimelineCanvas")
				{
					CaptureMouse();

					Point relativePosition = e.GetPosition(timelineOuterBox);
					double selectionWidth = (relativePosition.X / timelineOuterBox.Width) * timelineOuterBox.Width;
					timelineSelectionBox.Width = selectionWidth.Clamp(0.0, timelineOuterBox.Width);

					_isDraggingThumb = true;
					timelineProgressBox.Width = timelineSelectionBox.Width;
					Canvas.SetLeft(timelineThumb, timelineProgressBox.Width);

					Trace.WriteLine("OnMouseLeftButtonDown(): on TimeLine");
				}
		

			}
		}

		/// <summary>
		/// Invoked when an unhandled MouseLeftButtonUp routed event reaches an element in 
		/// its route that is derived from this class. Implement this method to add class 
		/// handling for this event.
		/// </summary>
		/// <param name="e">The MouseButtonEventArgs that contains the event data. The event 
		/// data reports that the left mouse button was released.</param>
		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			_isDraggingThumb = false;
			base.OnMouseLeftButtonUp(e);
			ReleaseMouseCapture();
			try
			{
			
			}
			finally
			{
			
			}
		}

		/// <summary>
		/// Invoked when an unhandled Mouse.MouseMove attached event reaches an element in 
		/// its route that is derived from this class. Implement this method to add class 
		/// handling for this event.
		/// </summary>
		/// <param name="e">The MouseEventArgs that contains the event data.</param>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			currentMousePosition = e.GetPosition(thumbCanvas);

			//if (Mouse.Captured == null)
			//{
			//	Canvas c = e.OriginalSource as Canvas;
			//	if (c == null)
			//		c = Utils.FindParent<Canvas>(e.OriginalSource as FrameworkElement);
				
			//}
			SetTimelinePositionOnMouseMove(e);
		}
		#endregion // Event Overrides.

		#region Drawing Methods and Events.
		private void UpdateAllRegions()
		{
			UpdateTimelineCanvas();
		}

		private void UpdateTimelineCanvas()
		{
			if (timelineCanvas == null)
				return;

			SetDefaultMeasurements();

			// Bounding timeline box.
			timelineOuterBox.Fill = new SolidColorBrush(
				(Color)ColorConverter.ConvertFromString("#878787")) { Opacity = 0.25 };
			timelineOuterBox.StrokeThickness = 0.0;
			timelineOuterBox.Width = __timelineWidth;
			timelineOuterBox.Height = TimelineThickness;
			timelineOuterBox.Margin = new Thickness(TimelineExpansionFactor * TimelineThickness,
				(timelineCanvas.RenderSize.Height - TimelineThickness) / 2, 0, 0);
			timelineOuterBox.SnapsToDevicePixels = true;

			// Selection timeline box.
			timelineSelectionBox.Fill = TimelineSelectionBrush;
			timelineSelectionBox.Width = 0.0;
			timelineSelectionBox.Height = TimelineThickness;
			timelineSelectionBox.Margin = new Thickness(TimelineExpansionFactor * TimelineThickness,
				(timelineCanvas.RenderSize.Height - TimelineThickness) / 2, 0, 0);
			timelineSelectionBox.SnapsToDevicePixels = true;

			// Progress timeline box.
			timelineProgressBox.Fill = TimelineProgressBrush;
			timelineProgressBox.StrokeThickness = 0.0;
			timelineProgressBox.Width = 0.0;
			timelineProgressBox.Height = TimelineThickness;
			timelineProgressBox.Margin = new Thickness(TimelineExpansionFactor * TimelineThickness,
				(timelineCanvas.RenderSize.Height - TimelineThickness) / 2, 0, 0);
			timelineProgressBox.SnapsToDevicePixels = true;

			// Animation and selection.
			timelineCanvas.MouseEnter -= TimelineCanvas_MouseEnter;
			timelineCanvas.MouseEnter += TimelineCanvas_MouseEnter;
			timelineCanvas.MouseLeave -= TimelineCanvas_MouseLeave;
			timelineCanvas.MouseLeave += TimelineCanvas_MouseLeave;
			timelineCanvas.MouseMove -= TimelineCanvas_MouseMove;
			timelineCanvas.MouseMove += TimelineCanvas_MouseMove;
			timelineCanvas.MouseDown -= TimelineCanvas_MouseDown;
			timelineCanvas.MouseDown += TimelineCanvas_MouseDown;

			// The draggable thumb.
			timelineThumb.Fill = TimelineThumbBrush;
			timelineThumb.VerticalAlignment = VerticalAlignment.Center;
			timelineThumb.Height = timelineThumb.Width = 0.0;
			timelineThumb.Margin = new Thickness(TimelineExpansionFactor * TimelineThickness, 
				timelineCanvas.RenderSize.Height / 2, 0, 0);
			timelineThumb.SnapsToDevicePixels = true;

			timelineThumb.MouseLeftButtonDown -= TimelineThumb_MouseLeftButtonDown;
			timelineThumb.MouseLeftButtonDown += TimelineThumb_MouseLeftButtonDown;

			timelineThumb.MouseLeftButtonUp -= TimelineThumb_MouseLeftButtonUp;
			timelineThumb.MouseLeftButtonUp += TimelineThumb_MouseLeftButtonUp;

			// Preview window.


			Trace.WriteLine("UpdateTimelineCanvas(): Hit");
		}

		private void TimelineCanvas_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_isDraggingThumb = _timelineHovered;
			Trace.WriteLine("TimelineCanvas_MouseDown");
		}

		private void SetDefaultMeasurements()
		{
			if (timelineCanvas != null)
				__timelineWidth = timelineCanvas.RenderSize.Width - 2 * 2 * TimelineThickness;
		}

		private void TimelineCanvas_MouseEnter(object sender, MouseEventArgs e)
		{
			if (_timelineHovered)
				return;

			timelineThumb.ResetAnimation(Ellipse.WidthProperty, Ellipse.HeightProperty);
			timelineProgressBox.ResetAnimation(Rectangle.HeightProperty, Rectangle.MarginProperty);
			timelineSelectionBox.ResetAnimation(Rectangle.HeightProperty, Rectangle.MarginProperty);
			timelineOuterBox.ResetAnimation(Rectangle.HeightProperty, Rectangle.MarginProperty);

			CircleEase easing = new CircleEase();
			easing.EasingMode = EasingMode.EaseOut;

			// Thumb animation.
			Thickness margin = new Thickness(0, 
				(timelineCanvas.RenderSize.Height - 2 * TimelineExpansionFactor * TimelineThickness) / 2, 0, 0);
			EllpiseDiameterAnimation(timelineThumb, TimelineThickness * TimelineExpansionFactor * 2, margin, easing);

			// Timeline animation.
			margin = new Thickness(TimelineExpansionFactor * TimelineThickness,
				(timelineCanvas.RenderSize.Height - (TimelineThickness * TimelineExpansionFactor)) / 2, 0, 0);
			TimelineHeightAnimation(timelineProgressBox, TimelineThickness * TimelineExpansionFactor, margin, easing);
			TimelineHeightAnimation(timelineSelectionBox, TimelineThickness * TimelineExpansionFactor, margin, easing);
			TimelineHeightAnimation(timelineOuterBox, TimelineThickness * TimelineExpansionFactor, margin, easing);

			double selectionWidth = (currentMousePosition.X / RenderSize.Width) * timelineOuterBox.Width;
			timelineSelectionBox.Width = selectionWidth.Clamp(0.0, timelineSelectionBox.Width);


			_timelineHovered = true;

			Trace.WriteLine($"TimelineCanvas_MouseENTER Canvas: isDragging = {_isDraggingThumb}, timelineHovered = { _timelineHovered}");
		}

		private void TimelineCanvas_MouseLeave(object sender, MouseEventArgs e)
		{
			if (_isDraggingThumb)
				return;

			try
			{
				timelineThumb.ResetAnimation(Ellipse.WidthProperty, Ellipse.HeightProperty);
				timelineProgressBox.ResetAnimation(Rectangle.HeightProperty, Rectangle.MarginProperty);
				timelineSelectionBox.ResetAnimation(Rectangle.HeightProperty, Rectangle.MarginProperty);
				timelineOuterBox.ResetAnimation(Rectangle.HeightProperty, Rectangle.MarginProperty);

				CircleEase easing = new CircleEase();
				easing.EasingMode = EasingMode.EaseOut;

				// Thumb animation.
				Thickness margin = new Thickness(TimelineExpansionFactor * TimelineThickness, 
					timelineCanvas.RenderSize.Height / 2, 0, 0);
				EllpiseDiameterAnimation(timelineThumb, 0.0, margin, easing);

				// Timeline animation.
				margin = new Thickness(TimelineExpansionFactor * TimelineThickness,
					(timelineCanvas.RenderSize.Height - TimelineThickness) / 2, 0, 0);
				TimelineHeightAnimation(timelineProgressBox, TimelineThickness, margin, easing);
				TimelineHeightAnimation(timelineSelectionBox, TimelineThickness, margin, easing);
				TimelineHeightAnimation(timelineOuterBox, TimelineThickness, margin, easing);
			}
			finally
			{
				timelineSelectionBox.Width = 0.0;
				_timelineHovered = false;
			}
			Trace.WriteLine($"TimelineCanvas_MouseLeave Canvas: isDragging = {_isDraggingThumb}, timelineHovered = { _timelineHovered}");
		}

		private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
		{
			SetTimelinePositionOnMouseMove(e);
		}

		private void TimelineThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
			_isDraggingThumb = true;
			Trace.WriteLine("TimelineThumb_MouseLeftButtonDown Dragging Thumb");
		}

		private void TimelineThumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
			_isDraggingThumb = false;
			Trace.WriteLine("TimelineThumb_MouseLeftButtonUp STOPPED Dragging Thumb");
		}

		/// <summary>
		/// Sets the timeline position based on the current move.
		/// </summary>
		private void SetTimelinePositionOnMouseMove(MouseEventArgs e)
		{
			Point relativePosition = e.GetPosition(timelineOuterBox);
			double selectionWidth = (relativePosition.X / timelineOuterBox.Width) * timelineOuterBox.Width;

			if (_timelineHovered)
				timelineSelectionBox.Width = selectionWidth.Clamp(0.0, timelineOuterBox.Width);

			if (_isDraggingThumb)
			{
				timelineProgressBox.Width = timelineSelectionBox.Width;
				Canvas.SetLeft(timelineThumb, timelineProgressBox.Width);
				SetTimelinePosition();

				if (_previewType != PreviewType.None)
					SetDisplayPreview();

				Trace.WriteLine("SetTimelinePositionOnMouseMove(): Mouse moving setting timeline");
			}
		}

		private void SetDisplayPreview()
		{
			// TODO we need request and recieve code here thins about how this could work.
		}

		private Canvas CreatePlayButton()
		{
			Path p = new Path();

			p.StrokeThickness = 0.0;
			p.Fill = Brushes.Gray;

			p.HorizontalAlignment = HorizontalAlignment.Center;
			p.VerticalAlignment = VerticalAlignment.Center;

			Point start = new Point(PlayButtonSize * 0.857, PlayButtonSize / 2.0);
			LineSegment[] segments = new[]
			{
				new LineSegment(new Point(0, 0), true),
				new LineSegment(new Point(0, PlayButtonSize), true)
			};
			PathFigure figure = new PathFigure(start, segments, true);
			p.Data = new PathGeometry(new[] { figure });

			Canvas c = new Canvas();
			c.Height = PlayButtonSize;
			c.Width = PlayButtonSize;
			c.Background = Brushes.Transparent;
			c.Margin = new Thickness(16.0, 0, 0, 8.0);
			c.Children.Add(p);

			c.MouseUp += (s, e) =>
			{
				Canvas tmp = (Canvas)s;

				if (controlGrid.Children.Contains(tmp))
					controlGrid.Children.Remove(tmp);

				Canvas pauseButton = CreatePauseButton();
				Grid.SetColumn(pauseButton, 0);
				controlGrid.Children.Add(pauseButton);
			};

			c.MouseEnter += (s, e) =>
			{
				p.Fill = Brushes.LightGray;
			};
			c.MouseLeave += (s, e) =>
			{
				p.Fill = Brushes.Gray;
			};

			return c;
		}

		private Canvas CreatePauseButton()
		{
			Path p = new Path();

			p.StrokeThickness = 0.0;
			p.Fill = Brushes.Gray;

			p.HorizontalAlignment = HorizontalAlignment.Center;
			p.VerticalAlignment = VerticalAlignment.Center;

			double f = 1.0 / 7.0;
			Point start1 = new Point(PlayButtonSize * f, 0.0);
			LineSegment[] s1 = new LineSegment[]
			{
				new LineSegment(new Point(PlayButtonSize * f, PlayButtonSize), true),
				new LineSegment(new Point((PlayButtonSize * f) * 3.0, PlayButtonSize), true),
				new LineSegment(new Point((PlayButtonSize * f) * 3.0, 0.0), true),
				new LineSegment(new Point(PlayButtonSize * f, 0.0), true)
			};

			Point start2 = new Point((PlayButtonSize * f) * 4, 0.0);
			LineSegment[] s2 = new LineSegment[]
			{
				new LineSegment(new Point((PlayButtonSize * f) * 4, PlayButtonSize), true),
				new LineSegment(new Point((PlayButtonSize * f) * 6, PlayButtonSize), true),
				new LineSegment(new Point((PlayButtonSize * f) * 6, 0.0), true),
				new LineSegment(new Point((PlayButtonSize * f) * 4, 0.0), true)
			};

			PathFigure figure1 = new PathFigure(start1, s1, true);
			PathFigure figure2 = new PathFigure(start2, s2, true);
			p.Data = new PathGeometry(new[] { figure1, figure2 });

			Canvas c = new Canvas();
			c.Height = PlayButtonSize;
			c.Width = PlayButtonSize;
			c.Background = Brushes.Transparent;
			c.Margin = new Thickness(16.0, 0, 0, 8.0);
			c.Children.Add(p);

			c.MouseUp += (s, e) =>
			{
				Canvas tmp = (Canvas)s;

				if (controlGrid.Children.Contains(tmp))
					controlGrid.Children.Remove(tmp);

				Canvas playButton = CreatePlayButton();
				Grid.SetColumn(playButton, 0);
				controlGrid.Children.Add(playButton);
			};

			c.MouseEnter += (s, e) =>
			{
				p.Fill = Brushes.LightGray;
			};
			c.MouseLeave += (s, e) =>
			{
				p.Fill = Brushes.Gray;
			};

			return c;
		}

		private Canvas CreateForwardSkipButton()
		{
			Path p = new Path();

			p.StrokeThickness = 0.0;
			p.Fill = Brushes.Gray;

			p.HorizontalAlignment = HorizontalAlignment.Center;
			p.VerticalAlignment = VerticalAlignment.Center;

			double f = 1.0 / 8.0;
			Point start1 = new Point(PlayButtonSize / 2.0 , PlayButtonSize / 2.0);
			LineSegment[] s1 = new[]
			{
				new LineSegment(new Point(PlayButtonSize * f, PlayButtonSize * (1.0 - f)), true),
				new LineSegment(new Point(PlayButtonSize * f, PlayButtonSize * f), true)
			};

			Point start2 = new Point(PlayButtonSize / 2.0, PlayButtonSize * (1.0 - f));
			LineSegment[] s2 = new LineSegment[]
			{
				new LineSegment(new Point((PlayButtonSize / 2.0) + f * PlayButtonSize, PlayButtonSize * (1.0 - f)), true),
				new LineSegment(new Point((PlayButtonSize / 2.0) + f * PlayButtonSize, PlayButtonSize * f), true),
				new LineSegment(new Point(PlayButtonSize / 2.0, PlayButtonSize * f), true)
			};

			PathFigure figure1 = new PathFigure(start1, s1, true);
			PathFigure figure2 = new PathFigure(start2, s2, true);
			p.Data = new PathGeometry(new[] { figure1, figure2 });

			Canvas c = new Canvas();
			c.Height = PlayButtonSize;
			c.Width = PlayButtonSize;
			c.Background = Brushes.Transparent;
			c.Margin = new Thickness(16.0, 0, 0, 8.0);
			c.Children.Add(p);

			c.MouseEnter += (s, e) =>
			{
				p.Fill = Brushes.LightGray;
			};
			c.MouseLeave += (s, e) =>
			{
				p.Fill = Brushes.Gray;
			};

			return c;
		}

		private Canvas CreateBackwardSkipButton()
		{
			Path p = new Path();

			p.StrokeThickness = 0.0;
			p.Fill = Brushes.Gray;

			p.HorizontalAlignment = HorizontalAlignment.Center;
			p.VerticalAlignment = VerticalAlignment.Center;

			double f = 1.0 / 8.0;
			Point start1 = new Point(PlayButtonSize / 2.0, PlayButtonSize / 2.0);
			LineSegment[] s1 = new[]
			{
				new LineSegment(new Point(PlayButtonSize * (1.0 - f), PlayButtonSize * (1.0 - f)), true),
				new LineSegment(new Point(PlayButtonSize * (1.0 - f), PlayButtonSize * f), true)
			};

			Point start2 = new Point(PlayButtonSize / 2.0, PlayButtonSize * (1.0 - f));
			LineSegment[] s2 = new LineSegment[]
			{
				new LineSegment(new Point((PlayButtonSize / 2.0) - f * PlayButtonSize, PlayButtonSize * (1.0 - f)), true),
				new LineSegment(new Point((PlayButtonSize / 2.0) - f * PlayButtonSize, PlayButtonSize * f), true),
				new LineSegment(new Point(PlayButtonSize / 2.0, PlayButtonSize * f), true)
			};

			PathFigure figure1 = new PathFigure(start1, s1, true);
			PathFigure figure2 = new PathFigure(start2, s2, true);
			p.Data = new PathGeometry(new[] { figure1, figure2 });

			Canvas c = new Canvas();
			c.Height = PlayButtonSize;
			c.Width = PlayButtonSize;
			c.Background = Brushes.Transparent;
			c.Margin = new Thickness(16.0, 0, 0, 8.0);
			c.Children.Add(p);

			c.MouseEnter += (s, e) =>
			{
				p.Fill = Brushes.LightGray;
			};
			c.MouseLeave += (s, e) =>
			{
				p.Fill = Brushes.Gray;
			};

			return c;
		}

		private bool _isVolumeSliderAnimated = false;
		private bool _internalVolumeChange = false;

		private void CreateVolumeButton()
		{
			volumeCanvas = resources["Volume"] as Canvas;

			volumeCanvas.Height = PlayButtonSize;
			volumeCanvas.Width = PlayButtonSize;

			volumeCanvas.Background = Brushes.Transparent;
			volumeCanvas.Margin = new Thickness(16.0, 0, 0, 8.0);

			Path p = volumeCanvas.Children[0] as Path;
			p.Fill = Brushes.Gray;

			volumeCanvas.MouseUp += (s, e) =>
			{
				_internalVolumeChange = true;
				try
				{
					if (controlGrid.Children.Contains(volumeCanvas))
						controlGrid.Children.Remove(volumeCanvas);

					if (muteVolumeCanvas == null)
						CreateMuteVolumeButton();

					Grid.SetColumn(muteVolumeCanvas, 3);
					controlGrid.Children.Add(muteVolumeCanvas);

					volumeSlider.Value = 0.0;
				}
				finally
				{
					_internalVolumeChange = false;
				}
			};
			volumeCanvas.MouseEnter += VolumeCanvas_MouseEnter;
			volumeCanvas.MouseLeave += VolumeCanvas_MouseLeave;
		}

		private void CreateMuteVolumeButton()
		{
			muteVolumeCanvas = resources["MuteVolume"] as Canvas;

			muteVolumeCanvas.Height = PlayButtonSize;
			muteVolumeCanvas.Width = PlayButtonSize;

			muteVolumeCanvas.Background = Brushes.Transparent;
			muteVolumeCanvas.Margin = new Thickness(16.0, 0, 0, 8.0);

			Path p = muteVolumeCanvas.Children[0] as Path;
			p.Fill = Brushes.Gray;

			muteVolumeCanvas.MouseUp += (s, e) =>
			{
				_internalVolumeChange = true;
				try
				{
					if (controlGrid.Children.Contains(muteVolumeCanvas))
						controlGrid.Children.Remove(muteVolumeCanvas);

					if (volumeCanvas == null)
						CreateVolumeButton();

					Grid.SetColumn(volumeCanvas, 3);
					controlGrid.Children.Add(volumeCanvas);

					volumeSlider.Value = volumeLevel;
				}
				finally
				{
					_internalVolumeChange = false;
				}
			};
			muteVolumeCanvas.MouseEnter += VolumeCanvas_MouseEnter;
			muteVolumeCanvas.MouseLeave += VolumeCanvas_MouseLeave;
		}

		private void VolumeCanvas_MouseEnter(object sender, MouseEventArgs e)
		{
			Path tmp = (Path)((Canvas)sender).Children[0];
			tmp.Fill = Brushes.LightGray;
		}

		private void VolumeCanvas_MouseLeave(object sender, MouseEventArgs e)
		{
			Path tmp = (Path)((Canvas)sender).Children[0];
			tmp.Fill = Brushes.Gray;
		}

		public void CreateVolumnSlider()
		{
			volumeSlider = new Slider();
			volumeSlider.Style = resources["VolumeSlider"] as Style;

			volumeSlider.Margin = new Thickness(10.0, 0.0, 0.0, 6.0);

			volumeSlider.HorizontalAlignment = HorizontalAlignment.Stretch;
			volumeSlider.VerticalAlignment = VerticalAlignment.Center;

			volumeSlider.Height = 22;
			volumeSlider.Width = 70; // 60;

			volumeLevel = 100;
			volumeSlider.Maximum = volumeSlider.Value = volumeLevel;
			volumeSlider.Minimum = 0;

			volumeSlider.ValueChanged += (s, e) =>
			{
				if (_internalVolumeChange)
					return; 

				volumeLevel = e.NewValue;
				if (volumeLevel == 0.0)
				{
					if (controlGrid.Children.Contains(volumeCanvas))
						controlGrid.Children.Remove(volumeCanvas);

					if (muteVolumeCanvas == null)
						CreateMuteVolumeButton();

					Grid.SetColumn(muteVolumeCanvas, 3);
					controlGrid.Children.Add(muteVolumeCanvas);
				}
				else if (volumeLevel > 0.0 && e.OldValue == 0.0)
				{
					if (controlGrid.Children.Contains(muteVolumeCanvas))
						controlGrid.Children.Remove(muteVolumeCanvas);

					if (volumeCanvas == null)
						CreateVolumeButton();

					if (!controlGrid.Children.Contains(volumeCanvas))
					{
						Grid.SetColumn(volumeCanvas, 3);
						controlGrid.Children.Add(volumeCanvas);
					}
				}
			};

			//volumeSlider.MouseLeave += (s, e) =>
			//{
			//	if (_isVolumeSliderAnimated)
			//		return;

			//	DeferredAction action = DeferredAction.Create(() =>
			//	{
			//		try
			//		{
			//			_isVolumeSliderAnimated = true;
			//			if (volumeSlider.IsMouseOver)
			//			{
			//				_isVolumeSliderAnimated = false;
			//				return;
			//			}

			//			CircleEase easing = new CircleEase();
			//			easing.EasingMode = EasingMode.EaseOut;

			//			DoubleAnimation widthAnimation = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
			//			widthAnimation.EasingFunction = easing;

			//			Storyboard storyboard = new Storyboard();
			//			Storyboard.SetTarget(widthAnimation, volumeSlider);
			//			Storyboard.SetTargetProperty(widthAnimation, new PropertyPath(Slider.WidthProperty));
			//			storyboard.Children.Add(widthAnimation);
			//			storyboard.Begin(this);
			//		}
			//		finally
			//		{
			//			_isVolumeSliderAnimated = false;
			//		}
			//	});
			//	action.Defer(new TimeSpan(0, 0, 1));
			//};
		}

		private void CreateEllapsedTimeTextBlock()
		{
			ellapsedTimeTextBlock = new TextBlock();

			ellapsedTimeTextBlock.FontSize = 12;
			ellapsedTimeTextBlock.FontFamily = new FontFamily("Sans Serif");

			ellapsedTimeTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
			ellapsedTimeTextBlock.VerticalAlignment = VerticalAlignment.Center;

			ellapsedTimeTextBlock.Foreground = Brushes.Gray;
			ellapsedTimeTextBlock.Background = Brushes.Transparent;

			ellapsedTimeTextBlock.Margin = new Thickness(16.0, 0.0, 0.0, 6.0);

			UpdateTimelinePosition();
		}

		private void CreateFullScreenButton()
		{
			fullScreenCanvas = resources["FullScreen"] as Canvas;

			fullScreenCanvas.Height = PlayButtonSize;
			fullScreenCanvas.Width = PlayButtonSize;

			fullScreenCanvas.Background = Brushes.Transparent;
			fullScreenCanvas.Margin = new Thickness(16.0, 0, 16.0, 8.0);

			Path p = fullScreenCanvas.Children[0] as Path;
			p.Fill = Brushes.Gray;

			fullScreenCanvas.MouseEnter += (s, e) =>
			{
				Path tmp = (Path)((Canvas)s).Children[0];
				tmp.Fill = Brushes.LightGray;
			};
			fullScreenCanvas.MouseLeave += (s, e) =>
			{
				Path tmp = (Path)((Canvas)s).Children[0];
				tmp.Fill = Brushes.Gray;
			};
		}

		private void UpdateTimelinePosition()
		{
			if (ellapsedTimeTextBlock == null)
				return;

			ellapsedTimeTextBlock.Text =
				String.Format("{0:00}:{1:00}.{2:000} / {3:00}:{4:00}.{5:000}",
					TimelinePosition.TotalMinutes,
					TimelinePosition.Seconds,
					TimelinePosition.Milliseconds,
					TimelineDuration.TotalMinutes,
					TimelineDuration.Seconds,
					TimelineDuration.Milliseconds);
		}

		#endregion // Drawing Methods and Events.

		#region Binding Updates.
		private void SetTimelinePosition()
		{
			if (TimelineDuration == null)
				throw new ArgumentNullException("TimelineDuration cannot be null");

			double w = timelineProgressBox.Width / timelineOuterBox.Width;
			double position = w * TimelineDuration.TotalMilliseconds;

			TimelinePosition = TimeSpan.FromMilliseconds(
				Math.Min(TimelineDuration.TotalMilliseconds, Math.Max(0, position)));
		}
		#endregion // Binding Updates.

		#region Animation Methods.
		private void EllpiseDiameterAnimation(Ellipse ellipse, double diameter, Thickness margin, IEasingFunction easing)
		{
			AnimationTimeline widthAnimation = ShapeWidthAnimation(ellipse, diameter, easing);
			AnimationTimeline heightAnimation = ShapeHeightAnimation(ellipse, diameter, easing);
			AnimationTimeline marginAnimation = ShapeMarginAnimation(ellipse, margin, easing);

			Storyboard storyboard = new Storyboard();
			storyboard.Children.Add(widthAnimation);
			storyboard.Children.Add(heightAnimation);
			storyboard.Children.Add(marginAnimation);
			storyboard.Begin(this);
		}

		private void TimelineHeightAnimation(Rectangle rectangle, double height, Thickness margin, IEasingFunction easing)
		{
			AnimationTimeline heightAnimation = ShapeHeightAnimation(rectangle, height, easing);
			AnimationTimeline marginAnimation = ShapeMarginAnimation(rectangle, margin, easing);

			Storyboard storyboard = new Storyboard();
			storyboard.Children.Add(marginAnimation);
			storyboard.Children.Add(heightAnimation);
			storyboard.Begin(this);
		}

		private AnimationTimeline ShapeMarginAnimation(Shape shape, Thickness margin, IEasingFunction easing)
		{
			ThicknessAnimation marginAnimation = new ThicknessAnimation(
				margin, TimeSpan.FromMilliseconds((TIMELINE_ANIMATION_DURATION)));

			if (easing != null)
				marginAnimation.EasingFunction = easing;

			Storyboard.SetTarget(marginAnimation, shape);
			Storyboard.SetTargetProperty(marginAnimation, new PropertyPath(Rectangle.MarginProperty));

			return marginAnimation;
		}

		private AnimationTimeline ShapeWidthAnimation(Shape shape, double width, IEasingFunction easing)
		{
			DoubleAnimation widthAnimation = new DoubleAnimation(
				width, TimeSpan.FromMilliseconds(TIMELINE_ANIMATION_DURATION));

			if (easing != null)
				widthAnimation.EasingFunction = easing;

			Storyboard.SetTarget(widthAnimation, shape);
			Storyboard.SetTargetProperty(widthAnimation, new PropertyPath(Shape.WidthProperty));

			return widthAnimation;
		}

		private AnimationTimeline ShapeHeightAnimation(Shape shape, double height, IEasingFunction easing)
		{
			DoubleAnimation heightAnimation = new DoubleAnimation(
				height, TimeSpan.FromMilliseconds(TIMELINE_ANIMATION_DURATION));

			if (easing != null)
				heightAnimation.EasingFunction = easing;

			Storyboard.SetTarget(heightAnimation, shape);
			Storyboard.SetTargetProperty(heightAnimation, new PropertyPath(Shape.HeightProperty));

			return heightAnimation;
		}
		#endregion // Animation Methods.

		#region Dependency Properties Control.
		/// <summary>
		/// Get or sets the factor the time line expands when hovered.
		/// </summary>
		[Category("Common")]
		public bool IsPlaying
		{
			get { return (bool)GetValue(IsPlayingProperty); }
			set { SetValue(IsPlayingProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="IsPlaying" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty IsPlayingProperty =
			DependencyProperty.Register("IsPlaying", typeof(bool), typeof(VideoTransportControl),
				new UIPropertyMetadata(false, OnIsPlayingChanged, OnCoerceIsPlaying));

		private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnIsPlayingChanged((bool)e.OldValue, (bool)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="IsPlaying"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="IsPlaying"/></param>
		/// <param name="newValue">The new value of <see cref="IsPlaying"/></param>
		protected virtual void OnIsPlayingChanged(bool oldValue, bool newValue)
		{
			//progressLine.StrokeThickness = IsPlaying;
			//CreateProgressIndicator();
		}

		private static object OnCoerceIsPlaying(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				 VideoTransportControl.OnCoerceIsPlaying((bool)baseValue) :
				 baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="IsPlaying"/> when a new value is applied.
		/// </summary>
		/// <param name="baseValue">The value that was set on <see cref="IsPlaying"/></param>
		/// <returns>The adjusted value of <see cref="IsPlaying"/></returns>
		protected virtual bool OnCoerceIsPlaying(bool baseValue)
		{
			return baseValue;
		}
		#endregion // Dependency Properties Control.

		#region Dependency Properties Timeline.
		/// <summary>
		/// Get or sets the position of the progress indicator bar.
		/// </summary>
		[Category("Common")]
		public TimeSpan TimelinePosition
		{
			get { return (TimeSpan)GetValue(TimelinePositionProperty); }
			set { SetValue(TimelinePositionProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="TimelinePosition" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty TimelinePositionProperty =
			 DependencyProperty.Register("TimelinePosition", typeof(TimeSpan), typeof(VideoTransportControl),
				 new UIPropertyMetadata(TimeSpan.Zero, OnTimelinePositionChanged, OnCoerceTimelinePosition));

		private static void OnTimelinePositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnTimelinePositionChanged((TimeSpan)e.OldValue, (TimeSpan)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="TimelinePosition"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="TimelinePosition"/></param>
		/// <param name="newValue">The new value of <see cref="TimelinePosition"/></param>
		protected virtual void OnTimelinePositionChanged(TimeSpan oldValue, TimeSpan newValue)
		{
			TimelinePosition = newValue;
			UpdateTimelinePosition();
		}

		private static object OnCoerceTimelinePosition(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				VideoTransportControl.OnCoerceTimelinePosition((TimeSpan)baseValue) :
				baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="TimelinePosition"/> when a new value is applied.
		/// </summary>
		/// <param name="baseValue">The value that was set on <see cref="TimelinePosition"/></param>
		/// <returns>The adjusted value of <see cref="TimelinePosition"/></returns>
		protected virtual TimeSpan OnCoerceTimelinePosition(TimeSpan baseValue)
		{
			baseValue = new TimeSpan(Math.Max(baseValue.Ticks, TimeSpan.Zero.Ticks));
			return baseValue;
		}

		/// <summary>
		/// Get or sets the position of the progress indicator bar.
		/// </summary>
		[Category("Common")]
		public TimeSpan TimelineDuration
		{
			get { return (TimeSpan)GetValue(TimelineDurationProperty); }
			set { SetValue(TimelineDurationProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="TimelineDuration" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty TimelineDurationProperty =
			 DependencyProperty.Register("TimelineDuration", typeof(TimeSpan), typeof(VideoTransportControl),
				 new UIPropertyMetadata(TimeSpan.Zero, OnTimelineDurationChanged, OnCoerceTimelineDuration));

		private static void OnTimelineDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnTimelineDurationChanged((TimeSpan)e.OldValue, (TimeSpan)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="TimelineDuration"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="TimelineDuration"/></param>
		/// <param name="newValue">The new value of <see cref="TimelineDuration"/></param>
		protected virtual void OnTimelineDurationChanged(TimeSpan oldValue, TimeSpan newValue)
		{
			TimelineDuration = newValue;
			UpdateTimelinePosition();
		}

		private static object OnCoerceTimelineDuration(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				VideoTransportControl.OnCoerceTimelineDuration((TimeSpan)baseValue) :
				baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="TimelineDuration"/> when a new value is applied.
		/// </summary>
		/// <param name="baseValue">The value that was set on <see cref="TimelineDuration"/></param>
		/// <returns>The adjusted value of <see cref="TimelineDuration"/></returns>
		protected virtual TimeSpan OnCoerceTimelineDuration(TimeSpan baseValue)
		{
			baseValue = new TimeSpan(Math.Max(baseValue.Ticks, TimeSpan.Zero.Ticks));
			return baseValue;
		}

		/// <summary>
		/// Get or sets the thickness of the progress indicator bar.
		/// </summary>
		[Category("Common")]
		public double TimelineThickness
		{
			get { return (double)GetValue(TimelineThicknessProperty); }
			set { SetValue(TimelineThicknessProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="TimelineThickness" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty TimelineThicknessProperty =
			 DependencyProperty.Register("TimelineThickness", typeof(double), typeof(VideoTransportControl),
				 new UIPropertyMetadata(5.0d, OnTimelineThicknessChanged, OnCoerceTimelineThickness));

		private static void OnTimelineThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnTimelineThicknessChanged((double)e.OldValue, (double)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="TimelineThickness"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="TimelineThickness"/></param>
		/// <param name="newValue">The new value of <see cref="TimelineThickness"/></param>
		protected virtual void OnTimelineThicknessChanged(double oldValue, double newValue)
		{
			//progressLine.StrokeThickness = TimelineThickness;
			//CreateProgressIndicator();
		}

		private static object OnCoerceTimelineThickness(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				VideoTransportControl.OnCoerceTimelineThickness((double)baseValue) :
				baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="TimelineThickness"/> when a new value is applied.
		/// </summary>
		/// <param name="baseValue">The value that was set on <see cref="TimelineThickness"/></param>
		/// <returns>The adjusted value of <see cref="TimelineThickness"/></returns>
		protected virtual double OnCoerceTimelineThickness(double baseValue)
		{
			baseValue = Math.Max(baseValue, 0.0d);
			return baseValue;
		}

		/// <summary>
		/// Get or sets the factor the time line expands when hovered.
		/// </summary>
		[Category("Common")]
		public double TimelineExpansionFactor
		{
			get { return (double)GetValue(TimelineExpansionFactorProperty); }
			set { SetValue(TimelineExpansionFactorProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="TimelineExpansionFactor" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty TimelineExpansionFactorProperty =
			DependencyProperty.Register("TimelineExpansionFactor", typeof(double), typeof(VideoTransportControl),
				new UIPropertyMetadata(2.0d, OnTimelineExpansionFactorChanged, OnCoerceTimelineExpansionFactor));

		private static void OnTimelineExpansionFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnTimelineExpansionFactorChanged((double)e.OldValue, (double)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="TimelineExpansionFactor"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="TimelineExpansionFactor"/></param>
		/// <param name="newValue">The new value of <see cref="TimelineExpansionFactor"/></param>
		protected virtual void OnTimelineExpansionFactorChanged(double oldValue, double newValue)
		{
			//progressLine.StrokeThickness = TimelineExpansionFactor;
			//CreateProgressIndicator();
		}

		private static object OnCoerceTimelineExpansionFactor(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				 VideoTransportControl.OnCoerceTimelineExpansionFactor((double)baseValue) :
				 baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="TimelineExpansionFactor"/> when a new value is applied.
		/// </summary>
		/// <param name="baseValue">The value that was set on <see cref="TimelineExpansionFactor"/></param>
		/// <returns>The adjusted value of <see cref="TimelineExpansionFactor"/></returns>
		protected virtual double OnCoerceTimelineExpansionFactor(double baseValue)
		{
			baseValue = Math.Max(baseValue, 0.0d);
			return baseValue;
		}

		/// <summary>
		/// The color of the progress bar.
		/// </summary>
		[Category("Brushes")]
		public Brush TimelineProgressBrush
		{
			get { return (Brush)GetValue(TimelineProgressBrushProperty); }
			set { SetValue(TimelineProgressBrushProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="TimelineProgressBrush" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty TimelineProgressBrushProperty =
			 DependencyProperty.Register("TimelineProgressBrush", typeof(Brush), typeof(VideoTransportControl),
					new UIPropertyMetadata(new SolidColorBrush(Colors.Red), OnTimelineProgressBrushChanged, OnCoerceTimelineProgressBrush));

		private static void OnTimelineProgressBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnTimelineProgressBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="TimelineProgressBrush"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="TimelineProgressBrush"/></param>
		/// <param name="newValue">The new value of <see cref="TimelineProgressBrush"/></param>
		protected virtual void OnTimelineProgressBrushChanged(Brush oldValue, Brush newValue)
		{
			//progressIndicator.Fill = ProgressBarBrush;
			//progressLine.Stroke = ProgressBarBrush;
			//CreateProgressIndicator();
		}

		private static object OnCoerceTimelineProgressBrush(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				VideoTransportControl.OnCoerceTimelineProgressBrush((Brush)baseValue) :
				baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="TimelineProgressBrush"/> when a new value is applied.
		/// </summary>
		/// <param name="value">The value that was set on <see cref="TimelineProgressBrush"/></param>
		/// <returns>The adjusted value of <see cref="TimelineProgressBrush"/></returns>
		protected virtual Brush OnCoerceTimelineProgressBrush(Brush value)
		{
			return value;
		}

		/// <summary>
		/// The color of the progress bar.
		/// </summary>
		[Category("Brushes")]
		public Brush TimelineSelectionBrush
		{
			get { return (Brush)GetValue(TimelineSelectionBrushProperty); }
			set { SetValue(TimelineSelectionBrushProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="TimelineSelectionBrush" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty TimelineSelectionBrushProperty =
			 DependencyProperty.Register("TimelineSelectionBrush", typeof(Brush), typeof(VideoTransportControl),
				  new UIPropertyMetadata(new SolidColorBrush(Colors.DarkGray) { Opacity = 0.5 }, 
					  OnTimelineSelectionBrushChanged, OnCoerceTimelineSelectionBrush));

		private static void OnTimelineSelectionBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnTimelineSelectionBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="TimelineSelectionBrush"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="TimelineSelectionBrush"/></param>
		/// <param name="newValue">The new value of <see cref="TimelineSelectionBrush"/></param>
		protected virtual void OnTimelineSelectionBrushChanged(Brush oldValue, Brush newValue)
		{
			//progressIndicator.Fill = ProgressBarBrush;
			//progressLine.Stroke = ProgressBarBrush;
			//CreateProgressIndicator();
		}

		private static object OnCoerceTimelineSelectionBrush(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				 VideoTransportControl.OnCoerceTimelineSelectionBrush((Brush)baseValue) :
				 baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="TimelineSelectionBrush"/> when a new value is applied.
		/// </summary>
		/// <param name="value">The value that was set on <see cref="TimelineSelectionBrush"/></param>
		/// <returns>The adjusted value of <see cref="TimelineSelectionBrush"/></returns>
		protected virtual Brush OnCoerceTimelineSelectionBrush(Brush value)
		{
			return value;
		}
		#endregion // Dependency Properties Timeline.

		#region Dependency Properties Timeline Thumb.
		/// <summary>
		/// Get or sets the brush used for the timeline thumb.
		/// </summary>
		[Category("Common")]
		public Brush TimelineThumbBrush
		{
			get { return (Brush)GetValue(TimelineThumbBrushProperty); }
			set { SetValue(TimelineThumbBrushProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="TimelineThumbBrush" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty TimelineThumbBrushProperty =
			 DependencyProperty.Register("TimelineThumbBrush", typeof(Brush), typeof(VideoTransportControl),
				 new UIPropertyMetadata(new SolidColorBrush(Colors.Red), OnTimelineThumbBrushChanged, OnCoerceTimelineThumbBrush));

		private static void OnTimelineThumbBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnTimelineThumbBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="TimelineThumbBrush"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="TimelineThumbBrush"/></param>
		/// <param name="newValue">The new value of <see cref="TimelineThumbBrush"/></param>
		protected virtual void OnTimelineThumbBrushChanged(Brush oldValue, Brush newValue)
		{
			//progressLine.StrokeThickness = TimelineThumbBrush;
			//CreateProgressIndicator();
		}

		private static object OnCoerceTimelineThumbBrush(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				VideoTransportControl.OnCoerceTimelineThumbBrush((Brush)baseValue) :
				baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="TimelineThumbBrush"/> when a new value is applied.
		/// </summary>
		/// <param name="baseValue">The value that was set on <see cref="TimelineThumbBrush"/></param>
		/// <returns>The adjusted value of <see cref="TimelineThumbBrush"/></returns>
		protected virtual Brush OnCoerceTimelineThumbBrush(Brush baseValue)
		{
			return baseValue;
		}

		/// <summary>
		/// Get or sets the brush used when the timeline thumb is hovered.
		/// </summary>
		[Category("Common")]
		public Brush TimelineThumbHoveredBrush
		{
			get { return (Brush)GetValue(TimelineThumbHoveredBrushProperty); }
			set { SetValue(TimelineThumbHoveredBrushProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="TimelineThumbHoveredBrush" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty TimelineThumbHoveredBrushProperty =
			 DependencyProperty.Register("TimelineThumbHoveredBrush", typeof(Brush), typeof(VideoTransportControl),
				  new UIPropertyMetadata(new SolidColorBrush(Colors.Blue), OnTimelineThumbHoveredBrushChanged, OnCoerceTimelineThumbHoveredBrush));

		private static void OnTimelineThumbHoveredBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnTimelineThumbHoveredBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="TimelineThumbHoveredBrush"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="TimelineThumbHoveredBrush"/></param>
		/// <param name="newValue">The new value of <see cref="TimelineThumbHoveredBrush"/></param>
		protected virtual void OnTimelineThumbHoveredBrushChanged(Brush oldValue, Brush newValue)
		{
			//progressLine.StrokeThickness = TimelineThumbHoveredBrush;
			//CreateProgressIndicator();
		}

		private static object OnCoerceTimelineThumbHoveredBrush(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				 VideoTransportControl.OnCoerceTimelineThumbHoveredBrush((Brush)baseValue) :
				 baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="TimelineThumbHoveredBrush"/> when a new value is applied.
		/// </summary>
		/// <param name="baseValue">The value that was set on <see cref="TimelineThumbHoveredBrush"/></param>
		/// <returns>The adjusted value of <see cref="TimelineThumbHoveredBrush"/></returns>
		protected virtual Brush OnCoerceTimelineThumbHoveredBrush(Brush baseValue)
		{
			return baseValue;
		}

		/// <summary>
		/// Get or sets the stroke brush of the timeline thumb.
		/// </summary>
		[Category("Common")]
		public Brush TimelineThumbStrokeBrush
		{
			get { return (Brush)GetValue(TimelineThumbStrokeBrushProperty); }
			set { SetValue(TimelineThumbStrokeBrushProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="TimelineThumbStrokeBrush" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty TimelineThumbStrokeBrushProperty =
			 DependencyProperty.Register("TimelineThumbStrokeBrush", typeof(Brush), typeof(VideoTransportControl),
				  new UIPropertyMetadata(new SolidColorBrush(Colors.DarkGray), OnTimelineThumbStrokeBrushChanged, OnCoerceTimelineThumbStrokeBrush));

		private static void OnTimelineThumbStrokeBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnTimelineThumbStrokeBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="TimelineThumbStrokeBrush"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="TimelineThumbStrokeBrush"/></param>
		/// <param name="newValue">The new value of <see cref="TimelineThumbStrokeBrush"/></param>
		protected virtual void OnTimelineThumbStrokeBrushChanged(Brush oldValue, Brush newValue)
		{
			//progressLine.StrokeThickness = TimelineThumbStrokeBrush;
			//CreateProgressIndicator();
		}

		private static object OnCoerceTimelineThumbStrokeBrush(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				 VideoTransportControl.OnCoerceTimelineThumbStrokeBrush((Brush)baseValue) :
				 baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="TimelineThumbStrokeBrush"/> when a new value is applied.
		/// </summary>
		/// <param name="baseValue">The value that was set on <see cref="TimelineThumbStrokeBrush"/></param>
		/// <returns>The adjusted value of <see cref="TimelineThumbStrokeBrush"/></returns>
		protected virtual Brush OnCoerceTimelineThumbStrokeBrush(Brush baseValue)
		{
			return baseValue;
		}

		/// <summary>
		/// Get or sets the timeline thumb stroke brush when the control is hovered.
		/// </summary>
		[Category("Common")]
		public Brush TimelineThumbHoveredStrokeBrush
		{
			get { return (Brush)GetValue(TimelineThumbHoveredStrokeBrushProperty); }
			set { SetValue(TimelineThumbHoveredStrokeBrushProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="TimelineThumbHoveredStrokeBrush" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty TimelineThumbHoveredStrokeBrushProperty =
			 DependencyProperty.Register("TimelineThumbHoveredStrokeBrush", typeof(Brush), typeof(VideoTransportControl),
				  new UIPropertyMetadata(new SolidColorBrush(Colors.DarkOrange), OnTimelineThumbHoveredStrokeBrushChanged, OnCoerceTimelineThumbHoveredStrokeBrush));

		private static void OnTimelineThumbHoveredStrokeBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnTimelineThumbHoveredStrokeBrushChanged((Brush)e.OldValue, (Brush)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="TimelineThumbHoveredStrokeBrush"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="TimelineThumbHoveredStrokeBrush"/></param>
		/// <param name="newValue">The new value of <see cref="TimelineThumbHoveredStrokeBrush"/></param>
		protected virtual void OnTimelineThumbHoveredStrokeBrushChanged(Brush oldValue, Brush newValue)
		{
			//progressLine.StrokeThickness = TimelineThumbHoveredStrokeBrush;
			//CreateProgressIndicator();
		}

		private static object OnCoerceTimelineThumbHoveredStrokeBrush(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				 VideoTransportControl.OnCoerceTimelineThumbHoveredStrokeBrush((Brush)baseValue) :
				 baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="TimelineThumbHoveredStrokeBrush"/> when a new value is applied.
		/// </summary>
		/// <param name="baseValue">The value that was set on <see cref="TimelineThumbHoveredStrokeBrush"/></param>
		/// <returns>The adjusted value of <see cref="TimelineThumbHoveredStrokeBrush"/></returns>
		protected virtual Brush OnCoerceTimelineThumbHoveredStrokeBrush(Brush baseValue)
		{
			return baseValue;
		}
		#endregion // Dependency Properties Timeline Thumb.

		#region Dependency Properties Preview Window.

		public enum PreviewType { None = 0, TimelinePosition, FramePreview };

		/// <summary>
		/// Gets or sets a value that tells the timeline the preview mode to use.
		/// </summary>
		[Category("Common")]
		public PreviewType PreviewMode
		{
			get { return (PreviewType)GetValue(PreviewModeProperty); }
			set { SetValue(PreviewModeProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="PreviewMode"/> dependency property. 
		/// </summary>
		public static readonly DependencyProperty PreviewModeProperty =
			DependencyProperty.Register("PreviewMode", typeof(PreviewType), typeof(VideoTransportControl),
				new UIPropertyMetadata(PreviewType.TimelinePosition, OnPreviewModeChanged, OnCoercePreviewMode));

		private static void OnPreviewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnPreviewModeChanged((PreviewType)e.OldValue, (PreviewType)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="PreviewMode"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="PreviewMode"/></param>
		/// <param name="newValue">The new value of <see cref="PreviewMode"/></param>
		protected virtual void OnPreviewModeChanged(PreviewType oldValue, PreviewType newValue)
		{
			_previewType = newValue;
		}

		private static object OnCoercePreviewMode(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				VideoTransportControl.OnCoercePreviewMode((PreviewType)baseValue) :
				baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="PreviewMode"/> when a new value is applied.
		/// </summary>
		/// <param name="value">The value that was set on <see cref="PreviewMode"/></param>
		/// <returns>The adjusted value of <see cref="PreviewMode"/></returns>
		protected virtual PreviewType OnCoercePreviewMode(PreviewType value)
		{
			return value;
		}
		#endregion // Dependency Properties Preview Window.

		#region Dependency Properties Control Grid.
		/// <summary>
		/// Get or sets the thickness of the progress indicator bar.
		/// </summary>
		[Category("Common")]
		public double PlayButtonSize
		{
			get { return (double)GetValue(PlayButtonSizeProperty); }
			set { SetValue(PlayButtonSizeProperty, value); }
		}

		/// <summary>
		/// Identifies the <see cref="PlayButtonSize" /> dependency property. 
		/// </summary>
		public static readonly DependencyProperty PlayButtonSizeProperty =
			 DependencyProperty.Register("PlayButtonSize", typeof(double), typeof(VideoTransportControl),
				 new UIPropertyMetadata(24.0d, OnPlayButtonSizeChanged, OnCoercePlayButtonSize));

		private static void OnPlayButtonSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			if (VideoTransportControl != null)
				VideoTransportControl.OnPlayButtonSizeChanged((double)e.OldValue, (double)e.NewValue);
		}

		/// <summary>
		/// Called after the <see cref="PlayButtonSize"/> value has changed.
		/// </summary>
		/// <param name="oldValue">The previous value of <see cref="PlayButtonSize"/></param>
		/// <param name="newValue">The new value of <see cref="PlayButtonSize"/></param>
		protected virtual void OnPlayButtonSizeChanged(double oldValue, double newValue)
		{
			//progressLine.StrokeThickness = PlayButtonSize;
			//CreateProgressIndicator();
		}

		private static object OnCoercePlayButtonSize(DependencyObject d, object baseValue)
		{
			VideoTransportControl VideoTransportControl = d as VideoTransportControl;
			return VideoTransportControl != null ?
				VideoTransportControl.OnCoercePlayButtonSize((double)baseValue) :
				baseValue;
		}

		/// <summary>
		/// Coerces the value of <see cref="PlayButtonSize"/> when a new value is applied.
		/// </summary>
		/// <param name="baseValue">The value that was set on <see cref="PlayButtonSize"/></param>
		/// <returns>The adjusted value of <see cref="PlayButtonSize"/></returns>
		protected virtual double OnCoercePlayButtonSize(double baseValue)
		{
			baseValue = Math.Max(baseValue, 0.0d);
			return baseValue;
		}
		#endregion // Dependency Properties Control Grid.

	}
}