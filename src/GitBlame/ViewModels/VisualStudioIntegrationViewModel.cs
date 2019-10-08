using System.Collections.Generic;
using ReactiveUI;

namespace GitBlame.ViewModels
{
	public sealed class VisualStudioIntegrationViewModel : ReactiveObject
	{
		public string Version
		{
			get => m_version;
			set => this.RaiseAndSetIfChanged(ref m_version, value);
		}

		public string Title => "Visual Studio " + s_title[m_version];

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

		string m_version;
		bool m_isChecked;
		VisualStudioIntegrationStatus m_integrationStatus;
		int? m_toolIndex;
	}
}
