using System;
using System.Runtime.InteropServices;

namespace SkyPeek
{
	[StructLayout(LayoutKind.Sequential)]
	public struct POINT
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct NOTIFYICONIDENTIFIER
	{
		public uint cbSize;
		public IntPtr hWnd;
		public uint uID;
		public Guid guidItem;
	}

	internal static partial class NativeMethods
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = ".NET source generator fails to marshal custom out/ref structs")]
		[DllImport("shell32.dll", SetLastError = true)]
		internal static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

		[LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
		internal static partial int GetWindowLong(IntPtr hwnd, int index);

		[LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
		internal static partial int SetWindowLong(IntPtr hwnd, int index, int newStyle);

		[LibraryImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

		[LibraryImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);

		[LibraryImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static partial bool SetForegroundWindow(IntPtr hWnd);

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = ".NET source generator fails to marshal custom out structs")]
		[DllImport("user32.dll", EntryPoint = "GetCursorPos")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool GetCursorPosition(out POINT lpPoint);

		internal static POINT GetCursorPos()
		{
			GetCursorPosition(out POINT p);
			return p;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = ".NET source generator fails to generate implementation")]
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time", Justification = ".NET source generator fails to marshal ref uint")]
		[DllImport("kernel32.dll", ExactSpelling = true)]
		internal static extern uint GetCurrentPackageId(ref uint packagePathLength, IntPtr packagePath);

		[LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
		internal static partial uint RegisterWindowMessage(string lpString);
		internal const int NIN_SELECT = 0x400; // Left-click/Enter
		internal const int NIN_KEYSELECT = 0x401; // Keyboard Enter/Space
	}
}