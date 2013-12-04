namespace GitBlame.ViewModels
{
	public class OpenFileNotification : NotificationBase
	{
		public OpenFileNotification()
		{
		}

		public OpenFileNotification(string filePath)
		{
			m_filePath = filePath;
		}

		public string FilePath
		{
			get { return m_filePath; }
		}

		readonly string m_filePath;
	}
}
