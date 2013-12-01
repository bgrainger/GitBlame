using System;
using System.Security;
using Microsoft.Win32;
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

		public static string GetRegistrySetting(string name)
		{
			try
			{
				using (var key = Registry.CurrentUser.OpenSubKey(c_registryKeyName))
				{
					if (key != null)
						return key.GetValue(name) as string;
				}
			}
			catch (SecurityException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}

			return null;
		}

		public static bool SetRegistrySetting(string name, string value)
		{
			try
			{
				using (var key = Registry.CurrentUser.CreateSubKey(c_registryKeyName))
				{
					if (key != null)
					{
						key.SetValue(name, value);
						return true;
					}
				}
			}
			catch (SecurityException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}

			return false;
		}

		const string c_registryKeyName = @"Software\GitBlame";

		MainWindowModel m_mainWindowModel;
	}
}
