using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;

namespace GitBlame.ViewModels
{
	public sealed class VisualStudioNotification : NotificationBase
	{
		public VisualStudioNotification(IEnumerable<VisualStudioIntegrationViewModel> versions, Action<VisualStudioNotification> integrate, Action<VisualStudioNotification> doNotIntegrate)
		{
			Versions = new ReadOnlyObservableCollection<VisualStudioIntegrationViewModel>(new ObservableCollection<VisualStudioIntegrationViewModel>(versions));
			IntegrateCommand = ReactiveCommand.Create(() => integrate(this));
			DoNotIntegrateCommand = ReactiveCommand.Create(() => doNotIntegrate(this));
		}

		public IReactiveCommand IntegrateCommand { get; }

		public IReactiveCommand DoNotIntegrateCommand { get; }

		public ReadOnlyObservableCollection<VisualStudioIntegrationViewModel> Versions { get; }
	}
}
