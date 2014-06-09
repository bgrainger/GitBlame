using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Common.Logging;
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

			var openFileNotifications = this.WhenAny(x => x.Position, x => x.Value)
				.Select(x => x == null ? new OpenFileNotification() : x.RepoPath == null ? new OpenFileNotification(x.FilePath) : null);
			var notifications = openFileNotifications.Cast<NotificationBase>().StartWith(default(NotificationBase)).CombineLatest(
					CheckForUpdates().Cast<NotificationBase>().StartWith(default(NotificationBase)),
					VisualStudioIntegration.Check().Cast<NotificationBase>().StartWith(default(NotificationBase)),
					(of, ua, vs) => of ?? ua ?? vs)
				.DistinctUntilChanged();
			m_notification = notifications.ToProperty(this, x => x.Notification);

			CheckForUpdates();
		}

		public BlamePositionModel Position
		{
			get { return m_position; }
			private set
			{
				if (value == null)
					Log.Info("Position := (null)");
				else
					Log.InfoFormat("Position := Repo={0}, File={1}, CommitId={2}, LineNumber={3}", value.RepoPath, value.FileName, value.CommitId, value.LineNumber);
				
				this.RaiseAndSetIfChanged(ref m_position, value);
			}
		}

		public void NavigateTo(BlamePositionModel position)
		{
			Log.DebugFormat("NavigateTo({0})", position == null ? "null" : "position");

			if (Position != null)
				m_positionHistory.Push(Position);
			m_positionFuture.Clear();
			Position = position;
		}

		public void NavigateBack()
		{
			if (m_positionHistory.Count != 0)
			{
				Log.Debug("NavigateBack");
				m_positionFuture.Push(Position);
				Position = m_positionHistory.Pop();
			}
		}

		public void NavigateForward()
		{
			if (m_positionFuture.Count != 0)
			{
				Log.Debug("NavigateForward");
				m_positionHistory.Push(Position);
				Position = m_positionFuture.Pop();
			}
		}

		public NotificationBase Notification
		{
			get { return m_notification.Value; }
		}

		public string WindowTitle
		{
			get { return m_windowTitle.Value; }
		}

		private IObservable<UpdateAvailableNotification> CheckForUpdates()
		{
			return Observable.Create<UpdateAvailableNotification>(async obs =>
			{
				string updateUrl = AppModel.GetRegistrySetting("UpdateUrl");
				Log.DebugFormat("UpdateUrl = {0}", updateUrl);
				using (var updateManager = new UpdateManager(updateUrl ?? @"http://bradleygrainger.com/GitBlame/download", "GitBlame", FrameworkVersion.Net45))
				{
					try
					{
						UpdateInfo updateInfo = await updateManager.CheckForUpdate();
						var releases = updateInfo == null ? new List<ReleaseEntry>() : updateInfo.ReleasesToApply.ToList();
						if (updateInfo == null)
							Log.Info("CheckForUpdate returned (null)");
						else
							Log.InfoFormat("CheckForUpdate: Current=({0}), Future=({1}), {2} ReleasesToApply", ToLog(updateInfo.CurrentlyInstalledVersion), ToLog(updateInfo.FutureReleaseEntry), releases.Count);

						if (releases.Count != 0)
						{
							await updateManager.DownloadReleases(releases);
							Log.Info("Downloaded releases");
							var results = await updateManager.ApplyReleases(updateInfo);
							Log.InfoFormat("ApplyReleases: {0}", string.Join(", ", results));

							if (results.Any())
							{
								string newPath = results[0];
								VisualStudioIntegration.ReintegrateWithVisualStudio(newPath);
								obs.OnNext(new UpdateAvailableNotification(newPath));
							}
						}
					}
					catch (InvalidOperationException ex)
					{
						// Squirrel throws an InvalidOperationException (wrapping the underlying exception) if anything goes wrong
						Log.ErrorFormat("CheckForUpdates failed: {0}", ex, ex.Message);
						if (ex.InnerException != null)
							Log.ErrorFormat("CheckForUpdates inner exception: {0}", ex.InnerException, ex.InnerException.Message);
					}
					catch (TimeoutException ex)
					{
						// Failed to check for updates; try again the next time the app is run
						Log.ErrorFormat("CheckForUpdates timed out: {0}", ex, ex.Message);
					}
				}
				obs.OnCompleted();
			});
		}

		private static string ToLog(ReleaseEntry entry)
		{
			return entry == null ? "null" : entry.EntryAsString;
		}

		static readonly ILog Log = LogManager.GetLogger("MainWindow");

		readonly Stack<BlamePositionModel> m_positionHistory;
		readonly Stack<BlamePositionModel> m_positionFuture;
		readonly ObservableAsPropertyHelper<NotificationBase> m_notification;
		readonly ObservableAsPropertyHelper<string> m_windowTitle;
		BlamePositionModel m_position;
	}
}
