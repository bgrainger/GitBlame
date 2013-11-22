using ReactiveUI;

namespace GitBlame
{
	public sealed class MainWindowModel : ReactiveObject
	{
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

		string m_filePath;
		int? m_lineNumber;
	}
}
