using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;
using ReactiveUI;
using Squirrel.Client;
using Squirrel.Core;

namespace GitBlame.ViewModels
{
	public sealed class MainWindowModel : ReactiveObject
	{
		public MainWindowModel()
		{
			m_windowTitle = this.WhenAny(x => x.FilePath, x => x.Value).Select(x => (x == null ? "" : Path.GetFileName(x) + " - ") + "GitBlame").ToProperty(this, x => x.WindowTitle);

			var openFileNotifications = this.WhenAny(x => x.FilePath, x => x.Value).Select(x => x == null ? new OpenFileNotification() : null);
			m_updateAvailableNotifications = new Subject<UpdateAvailableNotification>();
			var notifications = openFileNotifications.StartWith(default(OpenFileNotification)).CombineLatest(m_updateAvailableNotifications.StartWith(default(UpdateAvailableNotification)),
				(of, ua) => (NotificationBase) of ?? ua);
			m_notification = notifications.ToProperty(this, x => x.Notification);
			m_notificationVisibility = notifications.Select(x => x != null ? Visibility.Visible : Visibility.Collapsed).ToProperty(this, x => x.NotificationVisibility);

			CheckForUpdates();
		}

		public string FilePath
		{
			get { return m_filePath; }
			set { this.RaiseAndSetIfChanged(ref m_filePath, value); }
		}

		public int? LineNumber
		{
			get { return m_lineNumber; }
			set { this.RaiseAndSetIfChanged(ref m_lineNumber, value); }
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

		private async void CheckForUpdates()
		{
			using (var updateManager = new UpdateManager(@"http://bradleygrainger.com/GitBlame/download", "GitBlame", FrameworkVersion.Net45))
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
							m_updateAvailableNotifications.OnNext(new UpdateAvailableNotification(results[0]));
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
		}

		string m_filePath;
		int? m_lineNumber;
		readonly ObservableAsPropertyHelper<NotificationBase> m_notification;
		readonly ObservableAsPropertyHelper<string> m_windowTitle;
		readonly ObservableAsPropertyHelper<Visibility> m_notificationVisibility;
		readonly Subject<UpdateAvailableNotification> m_updateAvailableNotifications;
	}
}
