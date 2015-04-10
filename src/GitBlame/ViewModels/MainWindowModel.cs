﻿using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using Common.Logging;
using ReactiveUI;

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
					VisualStudioIntegration.Check().Cast<NotificationBase>().StartWith(default(NotificationBase)),
					(of, vs) => of ?? vs)
				.DistinctUntilChanged();
			m_notification = notifications.ToProperty(this, x => x.Notification);
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

		static readonly ILog Log = LogManager.GetLogger("MainWindow");

		readonly Stack<BlamePositionModel> m_positionHistory;
		readonly Stack<BlamePositionModel> m_positionFuture;
		readonly ObservableAsPropertyHelper<NotificationBase> m_notification;
		readonly ObservableAsPropertyHelper<string> m_windowTitle;
		BlamePositionModel m_position;
	}
}
