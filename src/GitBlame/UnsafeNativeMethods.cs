using System;
using System.Runtime.InteropServices;

namespace GitBlame
{
	internal static class UnsafeNativeMethods
	{
		[DllImport("Kernel32.dll", CharSet=CharSet.Unicode, EntryPoint="CreateHardLinkW")]
		public static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

		[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "DeleteFileW")]
		public static extern bool DeleteFile(string lpFileName);
	}
}
