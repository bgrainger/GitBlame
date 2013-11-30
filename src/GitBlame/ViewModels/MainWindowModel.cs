using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Security;
using System.Windows;
using Microsoft.Win32;
using ReactiveUI;
using Squirrel.Client;
using Squirrel.Core;

namespace GitBlame.ViewModels
{
	public sealed class MainWindowModel : ReactiveObject
	{
		public MainWindowModel()
		{
			m_positionHistory = new Stack<BlamePositionModel>();
			m_positionFuture = new Stack<BlamePositionModel>();

			m_windowTitle = this.WhenAny(x => x.Position, x => x.Value).Select(x => (x == null ? "" : Path.GetFileName(x.FileName) + " - ") + "GitBlame").ToProperty(this, x => x.WindowTitle);

			var openFileNotifications = this.WhenAny(x => x.Position, x => x.Value).Select(x => x == null ? new OpenFileNotification() : null);
			var updateAvailableNotifications = CheckForUpdates();
			var notifications = openFileNotifications.StartWith(default(OpenFileNotification)).CombineLatest(updateAvailableNotifications.StartWith(default(UpdateAvailableNotification)),
				(of, ua) => (NotificationBase) of ?? ua);
			m_notification = notifications.ToProperty(this, x => x.Notification);
			m_notificationVisibility = notifications.Select(x => x != null ? Visibility.Visible : Visibility.Collapsed).ToProperty(this, x => x.NotificationVisibility);

			CheckForUpdates();
		}

		public BlamePositionModel Position
		{
			get { return m_position; }
			private set { this.RaiseAndSetIfChanged(ref m_position, value); }
		}

		public void NavigateTo(BlamePositionModel position)
		{
			if (Position != null)
				m_positionHistory.Push(Position);
			m_positionFuture.Clear();
			Position = position;
		}

		public void NavigateBack()
		{
			if (m_positionHistory.Count != 0)
			{
				m_positionFuture.Push(Position);
				Position = m_positionHistory.Pop();
			}
		}

		public void NavigateForward()
		{
			if (m_positionFuture.Count != 0)
			{
				m_positionHistory.Push(Position);
				Position = m_positionFuture.Pop();
			}
		}

		public NotificationBase Notification
		{
			get { return m_notification.Value; }
		}

		public Visibility NotificationVisibility
		{
			get { return m_notificationVisibility.Value; }
		}

		public string WindowTitle
		{
			get { return m_windowTitle.Value; }
		}

		private IObservable<UpdateAvailableNotification> CheckForUpdates()
		{
			return Observable.Create<UpdateAvailableNotification>(async obs =>
			{
				string updateUrl = null;
				try
				{
					using (var key = Registry.CurrentUser.OpenSubKey(@"Software\GitBlame"))
					{
						if (key != null)
							updateUrl = key.GetValue("UpdateUrl") as string;
					}
				}
				catch (SecurityException)
				{
				}
				catch (UnauthorizedAccessException)
				{
				}

				using (var updateManager = new UpdateManager(updateUrl ?? @"http://bradleygrainger.com/GitBlame/download", "GitBlame", FrameworkVersion.Net45))
				{
					try
					{
						UpdateInfo updateInfo = await updateManager.CheckForUpdate();
						var releases = updateInfo == null ? new List<ReleaseEntry>() : updateInfo.ReleasesToApply.ToList();
						if (releases.Count != 0)
						{
							await updateManager.DownloadReleases(releases);
							var results = await updateManager.ApplyReleases(updateInfo);

							if (results.Any())
								obs.OnNext(new UpdateAvailableNotification(results[0]));
						}
					}
					catch (InvalidOperationException)
					{
						// Squirrel throws an InvalidOperationException (wrapping the underlying exception) if anything goes wrong
					}
					catch (TimeoutException)
					{
						// Failed to check for updates; try again the next time the app is run
					}
				}
				obs.OnCompleted();
			});
		}

		readonly Stack<BlamePositionModel> m_positionHistory;
		readonly Stack<BlamePositionModel> m_positionFuture;
		readonly ObservableAsPropertyHelper<NotificationBase> m_notification;
		readonly ObservableAsPropertyHelper<string> m_windowTitle;
		readonly ObservableAsPropertyHelper<Visibility> m_notificationVisibility;
		BlamePositionModel m_position;
	}
}
