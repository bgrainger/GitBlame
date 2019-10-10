using System.Collections.Generic;
using ReactiveUI;

namespace GitBlame.ViewModels
{
	public sealed class VisualStudioIntegrationViewModel : ReactiveObject
	{
		public VisualStudioIntegrationViewModel(string version, VisualStudioIntegrationStatus integrationStatus)
		{
			Version = version;
			IntegrationStatus = integrationStatus;
		}

		public string Version { get; }

		public string Title => "Visual Studio " + s_title[Version];

		public bool IsChecked
		{
			get => m_isChecked;
			set => this.RaiseAndSetIfChanged(ref m_isChecked, value);
		}

		public VisualStudioIntegrationStatus IntegrationStatus
		{
			get => m_integrationStatus;
			set => this.RaiseAndSetIfChanged(ref m_integrationStatus, value);
		}

		public int? ToolIndex
		{
			get => m_toolIndex;
			set => this.RaiseAndSetIfChanged(ref m_toolIndex, value);
		}

		static readonly Dictionary<string, string> s_title = new Dictionary<string, string>
			{
				{ "9", "2008" },
				{ "10", "2010" },
				{ "11", "2012" },
				{ "12", "2013" }
			};

		bool m_isChecked;
		int? m_toolIndex;
		VisualStudioIntegrationStatus m_integrationStatus;
	}
}
