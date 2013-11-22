using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
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
			m_notificationVisibility = this.WhenAny(x => x.Notification, x => x.Value != null).Select(x => x ? Visibility.Visible: Visibility.Collapsed).ToProperty(this, x => x.NotificationVisibility);
			this.WhenAny(x => x.FilePath, x => x.Value).Select(x => x == null ? new OpenFileNotification() : null).Subscribe(x => Notification = x);

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
			get { return m_notification; }
			set { this.RaiseAndSetIfChanged(ref m_notification, value); }
		}

		public Visibility NotificationVisibility
		{
			get { return m_notificationVisibility.Value; }
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
							Notification = new UpdateAvailableNotification(results[0]);
					}
				}
				catch (InvalidOperationException)
				{
					// Squirrel throws an InvalidOperationException (wrapping the underlying exception) if anything goes wrong
				}
			}
		}

		string m_filePath;
		int? m_lineNumber;
		NotificationBase m_notification;
		readonly ObservableAsPropertyHelper<Visibility> m_notificationVisibility;
	}
}
