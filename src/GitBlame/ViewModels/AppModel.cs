using System;
using System.Security;
using Microsoft.Win32;
using NLog;
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
				Log.Info("Getting registry value for '{0}'", name);
				using var key = Registry.CurrentUser.OpenSubKey(c_registryKeyName);
				return key?.GetValue(name) as string;
			}
			catch (SecurityException ex)
			{
				Log.Error(ex, "SecurityException getting '{0}'", name);
			}
			catch (UnauthorizedAccessException ex)
			{
				Log.Error(ex, "SecurityException getting '{0}'", name);
			}

			return null;
		}

		public static bool SetRegistrySetting(string name, string value)
		{
			try
			{
				Log.Info("Setting registry value '{0}' to '{1}'", name, value);
				using var key = Registry.CurrentUser.CreateSubKey(c_registryKeyName);
				key?.SetValue(name, value);
				return true;
			}
			catch (SecurityException ex)
			{
				Log.Error(ex, "SecurityException setting '{0}'", name);
			}
			catch (UnauthorizedAccessException ex)
			{
				Log.Error(ex, "UnauthorizedAccessException setting '{0}'", name);
			}

			return false;
		}

		const string c_registryKeyName = @"Software\GitBlame";
		static readonly ILogger Log = LogManager.GetLogger("App");

		MainWindowModel m_mainWindowModel;
	}
}
