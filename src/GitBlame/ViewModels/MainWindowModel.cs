using System;
using System.Reactive.Linq;
using System.Windows;
using ReactiveUI;

namespace GitBlame.ViewModels
{
	public sealed class MainWindowModel : ReactiveObject
	{
		public MainWindowModel()
		{
			m_notificationVisibility = this.WhenAny(x => x.Notification, x => x.Value != null).Select(x => x ? Visibility.Visible: Visibility.Collapsed).ToProperty(this, x => x.NotificationVisibility);
			this.WhenAny(x => x.FilePath, x => x.Value).Select(x => x == null ? new OpenFileNotification() : null).Subscribe(x => Notification = x);
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

		string m_filePath;
		int? m_lineNumber;
		NotificationBase m_notification;
		readonly ObservableAsPropertyHelper<Visibility> m_notificationVisibility;
	}
}
