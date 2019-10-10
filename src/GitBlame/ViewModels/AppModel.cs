using System;
using System.Security;
using Common.Logging;
using Microsoft.Win32;
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

		public static string? GetRegistrySetting(string name)
		{
			try
			{
				Log.InfoFormat("Getting registry value for '{0}'", name);
				using var key = Registry.CurrentUser.OpenSubKey(c_registryKeyName);
				return key?.GetValue(name) as string;
			}
			catch (SecurityException ex)
			{
				Log.ErrorFormat("SecurityException getting '{0}'", ex, name);
			}
			catch (UnauthorizedAccessException ex)
			{
				Log.ErrorFormat("SecurityException getting '{0}'", ex, name);
			}

			return null;
		}

		public static bool SetRegistrySetting(string name, string value)
		{
			try
			{
				Log.InfoFormat("Setting registry value '{0}' to '{1}'", name, value);
				using var key = Registry.CurrentUser.CreateSubKey(c_registryKeyName);
				key?.SetValue(name, value);
				return true;
			}
			catch (SecurityException ex)
			{
				Log.ErrorFormat("SecurityException setting '{0}'", ex, name);
			}
			catch (UnauthorizedAccessException ex)
			{
				Log.ErrorFormat("UnauthorizedAccessException setting '{0}'", ex, name);
			}

			return false;
		}

		const string c_registryKeyName = @"Software\GitBlame";
		static readonly ILog Log = LogManager.GetLogger("App");

		MainWindowModel m_mainWindowModel;
	}
}
