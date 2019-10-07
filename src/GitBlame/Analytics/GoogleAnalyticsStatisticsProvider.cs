using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using GitBlame.Utility;
using Microsoft.Win32;

namespace GitBlame.Analytics
{
	internal sealed class GoogleAnalyticsStatisticsProvider
	{
		public GoogleAnalyticsStatisticsProvider()
		{
		}

		public int ScreenWidth => (int) SystemParameters.PrimaryScreenWidth;

		public int ScreenHeight => (int) SystemParameters.PrimaryScreenHeight;

		public int ScreenColorDepth
		{
			get { return 32; }
		}

		public string OperatingSystemVersion
		{
			get
			{
				OperatingSystem os = Environment.OSVersion;
				Version version = os.Version;

				// perform simple detection of OS
				// TODO: more sophisticated detection; Windows 7 and Server 2008 have the same major/minor version number 
				string release;
				if (version.Major == 5 && version.Minor == 1)
					release = "XP";
				else if (version.Major == 5 && version.Minor == 2)
					release = "Server 2003";
				else if (version.Major == 6 && version.Minor == 0)
					release = "Vista";
				else if (version.Major == 6 && version.Minor == 1)
					release = "7";
				else if (version.Major == 6 && version.Minor == 2)
					release = version.Build < 9200 ? "8 (Build {0})".FormatInvariant(version.Build) : "8";
				else if (version.Major == 6 && version.Minor == 3)
					release = version.Build < 9600 ? "8.1 (Build {0})".FormatInvariant(version.Build) : "8.1";
				else if ((version.Major == 6 && version.Minor == 4) || (version.Major == 10 && version.Minor == 0))
					release = "10 (Build {0})".FormatInvariant(version.Build);
				else
					release = version.ToString();

				string servicePack = string.IsNullOrEmpty(os.ServicePack) ? "" : (" " + os.ServicePack.Replace("Service Pack ", "SP"));

				return "Windows " + release + servicePack;
			}
		}

		public bool Is64BitOperatingSystem
		{
			get
			{
				// trivially true if actually running as a 64-bit process
				if (IntPtr.Size == 8)
					return true;

				// call OS to find out if this is a 32-bit process running on 64-bit Windows
				bool isWow64Process;
				IsWow64Process(GetCurrentProcess(), out isWow64Process);
				return isWow64Process;
			}
		}

		public string SystemUserAgent
		{
			get
			{
				// try to get IE version from the exe first.
				Version ieVersion = null;
				try
				{
					using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\IEXPLORE.EXE"))
					{
						if (key != null)
						{
							string exeFileLocation = key.GetValue("Path") as string;
							if (!string.IsNullOrEmpty(exeFileLocation))
							{
								try
								{
									exeFileLocation = exeFileLocation.Replace("\\", @"\").Replace(";", @"\iexplore.exe");
									FileVersionInfo exeVersionInfo = FileVersionInfo.GetVersionInfo(exeFileLocation);
									Version fileIeVersion;
									if (Version.TryParse(exeVersionInfo.ProductVersion, out fileIeVersion))
										ieVersion = fileIeVersion;
								}
								catch (ArgumentException)
								{
								}
								catch (FileNotFoundException)
								{
								}
							}
						}
					}
				}
				catch (SecurityException)
				{
				}

				// if unable to get version from .exe try to get IE version from registry
				if (ieVersion == null)
				{
					// use IE5.5 as a sentinel meaning "couldn't determine version"
					ieVersion = new Version(5, 5);
					try
					{
						using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Internet Explorer", false))
						{
							if (key != null)
							{
								string registryVersion = key.GetValue("Version") as string;
								Version registryIeVersion;
								if (Version.TryParse(registryVersion, out registryIeVersion))
									ieVersion = registryIeVersion;
							}
						}
					}
					catch (SecurityException)
					{
					}
				}

				// build a standard IE User-Agent string with the IE and OS versions
				// See http://blogs.msdn.com/b/ie/archive/2009/01/09/the-internet-explorer-8-user-agent-string-updated-edition.aspx
				Version osVersion = Environment.OSVersion.Version;
				return string.Format("Mozilla/4.0 (compatible; MSIE {0}.{1}; Windows NT {2}.{3}; Trident/4.0; .NET CLR 3.5.30729)",
					ieVersion.Major, ieVersion.Minor, osVersion.Major, osVersion.Minor);
			}
		}

		public string ApplicationVersion
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
		}

		[DllImport("Kernel32.dll", ExactSpelling = true)]
		static extern IntPtr GetCurrentProcess();

		[DllImport("Kernel32.dll", ExactSpelling = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool IsWow64Process(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] out bool bWow64Process);
	}
}
