using ReactiveUI;

namespace GitBlame.ViewModels
{
	public sealed class AppModel : ReactiveObject
	{
		public AppModel()
		{
			MainWindow = new MainWindowModel();
		}

		public MainWindowModel MainWindow
		{
			get { return m_mainWindowModel; }
			private set { this.RaiseAndSetIfChanged(ref m_mainWindowModel, value); }
		}

		MainWindowModel m_mainWindowModel;
	}
}
