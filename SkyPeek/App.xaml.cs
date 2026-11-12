using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SkyPeek
{
	public partial class App : Application
	{
		public App()
		{
			this.DispatcherUnhandledException += (sender, e) =>
			{
				LogCrash(e.Exception, "UI Thread Crash");
				MessageBox.Show($"UI Thread Crash:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}", "SkyPeek Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
				e.Handled = true; // Prevent immediate silent exit
			};

			AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
			{
				Exception ex = e.ExceptionObject as Exception ?? new Exception("Unknown AppDomain crash");
				LogCrash(ex, "Background Thread Crash");
				// Showing a MessageBox via Dispatcher on a crashing background thread causes deadlocks and silent exits.
				// We display the MessageBox directly to the desktop to ensure it actually appears.
				MessageBox.Show($"Background Thread Crash:\n{ex.Message}\n\n{ex.StackTrace}", "SkyPeek Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
			};

			System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
			{
				LogCrash(e.Exception, "Unobserved Task Exception");
				e.SetObserved();
			};
		}
		private static readonly IHost _host = Host
			.CreateDefaultBuilder()
			.ConfigureServices((context, services) =>
			{
				services.AddSingleton<MainWindow>();
				services.AddTransient<Settings>();
			}).Build();

		public static T? GetService<T>() where T : class
		{
			return _host.Services.GetService(typeof(T)) as T;
		}

		protected override async void OnStartup(StartupEventArgs e)
		{
			bool createdNew;
			try
			{
				_mutex = new System.Threading.Mutex(true, "SkyPeek_SingleInstance_Mutex_99F2D7B4-8463-4C12-87F8-D49A8F6A0C13", out createdNew);
				_ownsMutex = createdNew;
			}
			catch (System.Threading.AbandonedMutexException ex)
			{
				// The previous instance crashed, but the OS successfully gave us ownership!
				_mutex = ex.Mutex!;
				createdNew = true;
				_ownsMutex = true;
			}

			if (!createdNew)
			{
				MessageBox.Show("SkyPeek is already running. If it's not appearing in your system tray, you may need to terminate the program from Task Manager and open it again. Sorry about that.", "SkyPeek", MessageBoxButton.OK, MessageBoxImage.Information);
				Application.Current.Shutdown();
				return;
			}

			base.OnStartup(e);

			try
			{
				await _host.StartAsync();
			}
			catch (Exception ex)
			{
				LogCrash(ex, "Host Startup Crash");
				MessageBox.Show($"Fatal error during background host startup:\n{ex.Message}", "SkyPeek Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
				Application.Current.Shutdown();
				return;
			}

			var mainWindow = GetService<MainWindow>();
			mainWindow?.Show();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			SettingsManager.Save();

			if (_ownsMutex)
			{
				_mutex?.ReleaseMutex();
			}
			_mutex?.Dispose();
			_host.StopAsync().GetAwaiter().GetResult();
			_host.Dispose();
			base.OnExit(e);
		}

		private static System.Threading.Mutex _mutex = null!;
		private static bool _ownsMutex = false;

		public static readonly System.Threading.Lock GlobalLogLock = new();

		public static string ResolveCrashLogPath()
		{
			try
			{
				uint length = 0;
				uint result = NativeMethods.GetCurrentPackageId(ref length, IntPtr.Zero);
				if (result == 122) // ERROR_INSUFFICIENT_BUFFER indicates packaged identity exists
				{
					return System.IO.Path.Combine(global::Windows.Storage.ApplicationData.Current.LocalFolder.Path, "SkyPeek_ErrorLog.txt");
				}
			}
			catch { }

			string exeDirectory = System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
			string appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyPeek", "SkyPeek_ErrorLog.txt");
			string portablePath = System.IO.Path.Combine(exeDirectory, "SkyPeek_ErrorLog.txt");

			if (System.IO.File.Exists(portablePath)) return portablePath;
			if (System.IO.File.Exists(appDataPath)) return appDataPath;

			try
			{
				string testFile = System.IO.Path.Combine(exeDirectory, ".write_test");
				System.IO.File.WriteAllText(testFile, string.Empty);
				System.IO.File.Delete(testFile);
				return portablePath;
			}
			catch
			{
				return appDataPath;
			}
		}

		private static void LogCrash(Exception ex, string source)
		{
			try
			{
				string logPath = ResolveCrashLogPath();
				string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n";

				Exception? inner = ex.InnerException;
				while (inner != null)
				{
					logEntry += $"Inner Exception: {inner.Message}\n{inner.StackTrace}\n";
					inner = inner.InnerException;
				}

				logEntry += new string('-', 80) + "\n";

				lock (GlobalLogLock)
				{
					if (!System.IO.File.Exists(logPath))
					{
						System.IO.File.WriteAllText(logPath, "SkyPeek Crash Log\n\n");
					}
					System.IO.File.AppendAllText(logPath, logEntry);
				}
			}
			catch (Exception loggingEx)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to write crash log: {loggingEx.Message}");
			}
		}
	}
}