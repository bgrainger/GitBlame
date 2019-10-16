using ReactiveUI;

namespace GitBlame.ViewModels
{
	public sealed class AppModel : ReactiveObject
	{
		public AppModel()
		{
			m_mainWindowModel = new MainWindowModel();
		}

		public MainWindowModel MainWindow
		{
			get => m_mainWindowModel;
			private set => this.RaiseAndSetIfChanged(ref m_mainWindowModel, value);
		}

		MainWindowModel m_mainWindowModel;
	}
}
