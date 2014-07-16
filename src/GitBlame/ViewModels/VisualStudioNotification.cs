using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;

namespace GitBlame.ViewModels
{
	public sealed class VisualStudioNotification : NotificationBase
	{
		public VisualStudioNotification(IEnumerable<VisualStudioIntegrationViewModel> versions)
		{
			m_observableVersions = new ReadOnlyObservableCollection<VisualStudioIntegrationViewModel>(new ObservableCollection<VisualStudioIntegrationViewModel>(versions));
			m_integrateCommand = ReactiveCommand.Create();
			m_doNotIntegrateCommand = ReactiveCommand.Create();
		}

		public IReactiveCommand<object> IntegrateCommand
		{
			get { return m_integrateCommand; }
		}

		public IReactiveCommand<object> DoNotIntegrateCommand
		{
			get { return m_doNotIntegrateCommand; }
		}

		public ReadOnlyObservableCollection<VisualStudioIntegrationViewModel> Versions
		{
			get { return m_observableVersions; }
		}

		readonly ReadOnlyObservableCollection<VisualStudioIntegrationViewModel> m_observableVersions;
		readonly ReactiveCommand<object> m_integrateCommand;
		readonly ReactiveCommand<object> m_doNotIntegrateCommand;
	}
}
