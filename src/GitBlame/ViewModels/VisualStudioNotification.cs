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
			m_integrateCommand = new ReactiveCommand(Observable.Return(true));
			m_doNotIntegrateCommand = new ReactiveCommand(Observable.Return(true));
		}

		public ReactiveCommand IntegrateCommand
		{
			get { return m_integrateCommand; }
		}

		public ReactiveCommand DoNotIntegrateCommand
		{
			get { return m_doNotIntegrateCommand; }
		}

		public ReadOnlyObservableCollection<VisualStudioIntegrationViewModel> Versions
		{
			get { return m_observableVersions; }
		}

		readonly ReadOnlyObservableCollection<VisualStudioIntegrationViewModel> m_observableVersions;
		readonly ReactiveCommand m_integrateCommand;
		readonly ReactiveCommand m_doNotIntegrateCommand;
	}
}
