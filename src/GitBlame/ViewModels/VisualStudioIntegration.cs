using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using GitBlame.Utility;
using Microsoft.Win32;

namespace GitBlame.ViewModels
{
	public static class VisualStudioIntegration
	{
		public static IObservable<VisualStudioNotification> Check()
		{
			var subject = new Subject<VisualStudioNotification>();
			Task.Run(() =>
			{
				string hardLinkPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"GitBlame\GitBlame.exe");
				UnsafeNativeMethods.DeleteFile(hardLinkPath);

				string exePath = Assembly.GetExecutingAssembly().Location;
				if (UnsafeNativeMethods.CreateHardLink(hardLinkPath, exePath, IntPtr.Zero))
				{
					var currentIntegrationStatus = GetCurrentIntegrationStatus();
					var possibleIntegrations = GetPossibleIntegrationStatus(hardLinkPath);

					// remember versions the user has already declined to integrate with
					foreach (var model in possibleIntegrations)
					{
						VisualStudioIntegrationStatus currentStatus;
						if (model.IntegrationStatus == VisualStudioIntegrationStatus.Available &&
							currentIntegrationStatus.TryGetValue(model.Version, out currentStatus) &&
							currentStatus == VisualStudioIntegrationStatus.NotInstalled)
						{
							model.IntegrationStatus = VisualStudioIntegrationStatus.NotInstalled;
						}

						if (model.IntegrationStatus != VisualStudioIntegrationStatus.NotInstalled)
							model.IsChecked = true;
					}

					// show notification if there are any available versions to integrate with
					if (possibleIntegrations.Any(x => x.IntegrationStatus == VisualStudioIntegrationStatus.Available))
					{
						var visualStudioNotification = new VisualStudioNotification(possibleIntegrations);
						visualStudioNotification.IntegrateCommand.Subscribe(x => IntegrateWithVisualStudio(subject, hardLinkPath, visualStudioNotification, true));
						visualStudioNotification.DoNotIntegrateCommand.Subscribe(x => IntegrateWithVisualStudio(subject, null, visualStudioNotification, false));
						subject.OnNext(visualStudioNotification);
					}
					else
					{
						subject.OnCompleted();
					}
				}
			});
			return subject;
		}

		private static void IntegrateWithVisualStudio(IObserver<VisualStudioNotification> observer, string commandPath, VisualStudioNotification model, bool integrate)
		{
			string preference = string.Join(";", model.Versions.Select(x => GetEffectivePreference(integrate, x)));
			AppModel.SetRegistrySetting("VisualStudioIntegration", preference);

			if (integrate)
			{
				// TODO: Delete tools where !x.IsChecked && x.IntegrationStatus == VisualStudioIntegrationStatus.Installed
				foreach (var version in model.Versions.Where(x => x.IsChecked && x.IntegrationStatus == VisualStudioIntegrationStatus.Available))
				{
					try
					{
						using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\VisualStudio\{0}.0\External Tools".FormatInvariant(version.Version)))
						{
							if (key != null)
							{
								int tool = (int) key.GetValue("ToolNumKeys");
								key.SetValue("ToolArg{0}".FormatInvariant(tool), "$(ItemPath) $(CurLine)");
								key.SetValue("ToolCmd{0}".FormatInvariant(tool), commandPath);
								key.SetValue("ToolDir{0}".FormatInvariant(tool), "$(ItemDir)");
								key.SetValue("ToolOpt{0}".FormatInvariant(tool), 17);
								key.SetValue("ToolSourceKey{0}".FormatInvariant(tool), "");
								key.SetValue("ToolTitle{0}".FormatInvariant(tool), "Git&Blame");
								key.SetValue("ToolNumKeys", tool + 1);
							}
						}
					}
					catch (SecurityException)
					{
					}
					catch (UnauthorizedAccessException)
					{
					}
				}
			}

			observer.OnNext(null);
			observer.OnCompleted();
		}

		private static string GetEffectivePreference(bool integrate, VisualStudioIntegrationViewModel model)
		{
			// if "Yes" was clicked, use the checkbox setting; else use the already-installed settgin
			if (integrate)
				return (model.IsChecked ? "+" : "-") + model.Version;
			else
				return (model.IntegrationStatus == VisualStudioIntegrationStatus.Installed ? "+" : "-") + model.Version;
		}

		private static ICollection<VisualStudioIntegrationViewModel> GetPossibleIntegrationStatus(string commandPath)
		{
			var possibleIntegrationStatus = new List<VisualStudioIntegrationViewModel>();
			try
			{
				using (var visualStudio = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\VisualStudio"))
				{
					if (visualStudio != null)
					{
						foreach (string subKeyName in s_knownVisualStudioVersions)
						{
							using (var externalTools = visualStudio.OpenSubKey(subKeyName + @".0\External Tools"))
							{
								if (externalTools != null)
								{
									VisualStudioIntegrationViewModel model = new VisualStudioIntegrationViewModel
									{
										Version = subKeyName,
										IntegrationStatus = VisualStudioIntegrationStatus.Available
									};
									for (int tool = 0; tool < (int) externalTools.GetValue("ToolNumKeys"); tool++)
									{
										if (commandPath.Equals(externalTools.GetValue("ToolCmd" + tool)))
										{
											model.IntegrationStatus = VisualStudioIntegrationStatus.Installed;
											model.ToolIndex = tool;
										}
									}

									possibleIntegrationStatus.Add(model);
								}
							}
						}
					}
				}
			}
			catch (SecurityException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}

			return possibleIntegrationStatus;
		}

		private static IReadOnlyDictionary<string, VisualStudioIntegrationStatus> GetCurrentIntegrationStatus()
		{
			var integrationStatus = new Dictionary<string, VisualStudioIntegrationStatus>();
			foreach (var integration in (AppModel.GetRegistrySetting("VisualStudioIntegration") ?? "").Split(';').Where(x => x.Length > 1))
			{
				string version = integration.Substring(1);
				if ((integration[0] == '-' || integration[0] == '+') && s_knownVisualStudioVersions.Contains(version))
				{
					integrationStatus[version] = integration[0] == '-' ? VisualStudioIntegrationStatus.NotInstalled : VisualStudioIntegrationStatus.Installed;
				}
			}
			return integrationStatus;
		}


		static readonly string[] s_knownVisualStudioVersions = { "9", "10", "11" };
	}
}
