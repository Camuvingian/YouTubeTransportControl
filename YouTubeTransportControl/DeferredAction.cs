using System;
using System.Threading;
using System.Windows;

namespace YouTubeTransportControl
{
	/// <summary>
	/// Represents a timer which performs an action on the UI thread 
	/// when time elapses. Rescheduling is supported.
	/// </summary>
	public class DeferredAction : IDisposable
	{
		// Vars.
		private Timer timer;
		private bool disposed = false;

		/// <summary>
		/// Private ctor.
		/// </summary>
		/// <param name="action">The action to be performed.</param>
		private DeferredAction(Action action)
		{
			timer = new Timer(new TimerCallback(delegate
			{
				if (Application.Current != null)
					Application.Current.Dispatcher.Invoke(action);
			}));
		}

		/// <summary>
		/// Creates a new DeferredAction.
		/// </summary>
		/// <param name="action">
		/// The action that will be deferred. It is not performed until after <see cref="Defer"/> is called.
		/// </param>
		public static DeferredAction Create(Action action)
		{
			if (action == null)
				throw new ArgumentNullException("action");
			return new DeferredAction(action);
		}

		/// <summary>
		/// Defers performing the action until after time elapses. 
		/// Repeated calls will reschedule the action if it has not 
		/// already been performed.
		/// </summary>
		/// <param name="delay">
		/// The amount of time to wait before performing the action.
		/// </param>
		public void Defer(TimeSpan delay)
		{
			// Fire action when time elapses (with no subsequent calls).
			timer.Change(delay, TimeSpan.FromMilliseconds(-1));
		}

		#region IDisposable Members.
		/// <summary>
		/// Dispose managed resources.
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
					if (timer != null)
					{
						timer.Dispose();
						timer = null;
					}
				}

				// Shared cleanup logic.
				disposed = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Finalizer.
		/// </summary>
		~DeferredAction()
		{
			Dispose(false);
		}
		#endregion // IDisposable Members.
	}
}
