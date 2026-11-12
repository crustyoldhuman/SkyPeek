/*
 * SkyPeek
 * Copyright (C) 2026 Kevin Stanislawski
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.IO;
using System.Text.Json;

namespace SkyPeek
{
	public class AppConfig
	{
		public static AppConfig Current { get; set; } = new();

		[System.Text.Json.Serialization.JsonIgnore]
		public readonly object ConfigLock = new();

		public System.Collections.Generic.HashSet<string> PlayedAlerts { get; set; } = [];
		public string City { get; set; } = "Chicago";
		public string State { get; set; } = "Illinois";
		public string Country { get; set; } = "USA";
		public int RefreshInterval { get; set; } = 30;
		public bool StartOnBoot { get; set; } = true;
		public bool FlipWindArrow { get; set; } = false;
		public bool UseCelsius { get; set; } = false;
		public bool Use24HourClock { get; set; } = false;
		public bool UseKph { get; set; } = false;
		public bool UseCm { get; set; } = false;
		public bool UseWindowsLocation { get; set; } = true;
		public bool UseAccentColor { get; set; } = false;
		public bool UseTransparency { get; set; } = true;
		public bool DisableAnimations { get; set; } = false;
		public double SlideoutScale { get; set; } = 1.0;
		public double InfoWindowWidth { get; set; } = 420;
		public double InfoWindowHeight { get; set; } = 420;
		public double SettingsWindowWidth { get; set; } = 690;
		public double SettingsWindowHeight { get; set; } = 490;
		public double? SettingsLeft { get; set; }
		public double? SettingsTop { get; set; }
		public bool EnableWeatherAlerts { get; set; } = true;
		public bool EnableAlertTone { get; set; } = true;
		public double AlertVolume { get; set; } = 0.5;
		public string SelectedAlertTone { get; set; } = "alerttone.wav";
		public bool EnableTrayAlertColors { get; set; } = true;
		public bool EnableHotkey { get; set; } = false;
		public uint HotkeyModifiers { get; set; } = 0x0002 | 0x0001;
		public uint HotkeyKey { get; set; } = 0x57;
		public string PopupPosition { get; set; } = "Bottom";
		public bool PreferTenDayAgenda { get; set; } = false;
		public bool PreferHourlyAgendaView { get; set; } = false;
		public string DefaultView { get; set; } = "Current";
		public string ThemePreference { get; set; } = "System";
		public string LastLocManCountry { get; set; } = "USA";
		public string LastLocManState { get; set; } = "";
		public bool UseAnimatedIcons { get; set; } = true;
		public bool HasCheckedOSAnimationSettings { get; set; } = false;
		public string LastDismissedUpdateVersion { get; set; } = "";
		public string KnownUpdateVersion { get; set; } = "";
		public System.Collections.Generic.List<SavedLocation> SavedLocations { get; set; } = [];
		public System.Collections.Generic.HashSet<string> DismissedAlerts { get; set; } = [];
		public System.Collections.Generic.HashSet<string> SeenBackgroundAlertIds { get; set; } = [];
		public System.Collections.Concurrent.ConcurrentDictionary<string, CachedWeatherData> WeatherCache { get; set; } = new();
		public bool HasSeenOnboarding { get; set; } = false;
		public bool HasSeenTrayBalloon { get; set; } = false;
	}
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public static class SettingsManager
	{
		private static readonly string _settingsPath = ResolveSettingsPath();

		private static string ResolveSettingsPath()
		{
			try
			{
				// If running as MSIX, strictly use the containerized AppData so uninstalls wipe the settings
				if (Windows.ApplicationModel.Package.Current != null)
				{
					return Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "SkyPeekSettings.json");
				}
			}
			catch { }

			string exeDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
			string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkyPeek", "SkyPeekSettings.json");
			string portablePath = Path.Combine(exeDirectory, "SkyPeekSettings.json");

			// If portable settings already exist, use them to avoid temp extractions wiping configuration
			if (File.Exists(portablePath)) return portablePath;

			// If local app data settings exist, use them over everything
			if (File.Exists(appDataPath)) return appDataPath;

			// Check if we have write access to the exe directory (portable mode)
			try
			{
				string testFile = Path.Combine(exeDirectory, ".write_test");
				File.WriteAllText(testFile, string.Empty);
				File.Delete(testFile);
				return portablePath;
			}
			catch
			{
				return appDataPath;
			}
		}

		public static async void SetStartup(bool enable)
		{
			try
			{
				bool isPackaged = false;
				try
				{
					var package = Windows.ApplicationModel.Package.Current;
					isPackaged = package != null;
				}
				catch { }

				if (isPackaged)
				{
					var startupTask = await Windows.ApplicationModel.StartupTask.GetAsync("SkyPeekStartup");
					if (enable)
						await startupTask.RequestEnableAsync();
					else
						startupTask.Disable();
				}
				else
				{
					using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
					string? path = Environment.ProcessPath;
					if (path != null)
					{
						if (enable) key?.SetValue("SkyPeek", path);
						else key?.DeleteValue("SkyPeek", false);
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Startup error: {ex.Message}");
			}
		}
		private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

		public static string GetSettingsDirectory() => Path.GetDirectoryName(_settingsPath) ?? Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;

		public static void Load()
		{
			try
			{
				if (File.Exists(_settingsPath))
				{
					try
					{
						string json = File.ReadAllText(_settingsPath);
						AppConfig.Current = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
					}
					catch (System.Exception ex1)
					{
						try
						{
							if (File.Exists(_settingsPath + ".bak"))
							{
								string bakJson = File.ReadAllText(_settingsPath + ".bak");
								AppConfig.Current = JsonSerializer.Deserialize<AppConfig>(bakJson, _jsonOptions) ?? new AppConfig();
							}
							else
							{
								throw new System.IO.FileNotFoundException("No backup file exists to recover from.");
							}
						}
						catch (System.Exception ex2)
						{
							try
							{
								string logPath = Path.Combine(GetSettingsDirectory(), "error.log");
								File.AppendAllText(logPath, $"[{System.DateTime.Now:O}] Settings load failed. Primary: {ex1.Message} | Backup: {ex2.Message}{Environment.NewLine}");
							}
							catch { } // Swallow log failure to guarantee no UI interruption
						}
					}
				}

				if (!AppConfig.Current.HasCheckedOSAnimationSettings)
				{
					bool osAnimationsEnabled = System.Windows.SystemParameters.ClientAreaAnimation;
					AppConfig.Current.DisableAnimations = !osAnimationsEnabled;
					AppConfig.Current.UseAnimatedIcons = osAnimationsEnabled;
					AppConfig.Current.HasCheckedOSAnimationSettings = true;
					Save();
				}
			}
			catch { }
		}

		public static void Save()
		{
			string tempPath = _settingsPath + ".tmp";
			string json;

			lock (AppConfig.Current.ConfigLock)
			{
				AppConfig snapshot = new()
				{
					PlayedAlerts = [.. AppConfig.Current.PlayedAlerts],
					City = AppConfig.Current.City,
					State = AppConfig.Current.State,
					Country = AppConfig.Current.Country,
					RefreshInterval = AppConfig.Current.RefreshInterval,
					StartOnBoot = AppConfig.Current.StartOnBoot,
					FlipWindArrow = AppConfig.Current.FlipWindArrow,
					UseCelsius = AppConfig.Current.UseCelsius,
					Use24HourClock = AppConfig.Current.Use24HourClock,
					UseKph = AppConfig.Current.UseKph,
					UseCm = AppConfig.Current.UseCm,
					UseWindowsLocation = AppConfig.Current.UseWindowsLocation,
					UseAccentColor = AppConfig.Current.UseAccentColor,
					UseTransparency = AppConfig.Current.UseTransparency,
					DisableAnimations = AppConfig.Current.DisableAnimations,
					SlideoutScale = AppConfig.Current.SlideoutScale,
					InfoWindowWidth = AppConfig.Current.InfoWindowWidth,
					InfoWindowHeight = AppConfig.Current.InfoWindowHeight,
					SettingsWindowWidth = AppConfig.Current.SettingsWindowWidth,
					SettingsWindowHeight = AppConfig.Current.SettingsWindowHeight,
					SettingsLeft = AppConfig.Current.SettingsLeft,
					SettingsTop = AppConfig.Current.SettingsTop,
					EnableWeatherAlerts = AppConfig.Current.EnableWeatherAlerts,
					EnableAlertTone = AppConfig.Current.EnableAlertTone,
					AlertVolume = AppConfig.Current.AlertVolume,
					SelectedAlertTone = AppConfig.Current.SelectedAlertTone,
					EnableTrayAlertColors = AppConfig.Current.EnableTrayAlertColors,
					EnableHotkey = AppConfig.Current.EnableHotkey,
					HotkeyModifiers = AppConfig.Current.HotkeyModifiers,
					HotkeyKey = AppConfig.Current.HotkeyKey,
					PopupPosition = AppConfig.Current.PopupPosition,
					PreferTenDayAgenda = AppConfig.Current.PreferTenDayAgenda,
					PreferHourlyAgendaView = AppConfig.Current.PreferHourlyAgendaView,
					DefaultView = AppConfig.Current.DefaultView,
					ThemePreference = AppConfig.Current.ThemePreference,
					LastLocManCountry = AppConfig.Current.LastLocManCountry,
					LastLocManState = AppConfig.Current.LastLocManState,
					UseAnimatedIcons = AppConfig.Current.UseAnimatedIcons,
					HasCheckedOSAnimationSettings = AppConfig.Current.HasCheckedOSAnimationSettings,
					LastDismissedUpdateVersion = AppConfig.Current.LastDismissedUpdateVersion,
					KnownUpdateVersion = AppConfig.Current.KnownUpdateVersion,
					SavedLocations = [.. AppConfig.Current.SavedLocations],
					DismissedAlerts = [.. AppConfig.Current.DismissedAlerts],
					SeenBackgroundAlertIds = [.. AppConfig.Current.SeenBackgroundAlertIds],
					WeatherCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedWeatherData>(
						AppConfig.Current.WeatherCache.ToDictionary(
							kvp => kvp.Key,
							kvp =>
							{
								var copy = new CachedWeatherData();
								copy.CopyScalarPropertiesFrom(kvp.Value);
								copy.HourlyData = [.. kvp.Value.HourlyData];
								copy.DailyData = [.. kvp.Value.DailyData];
								return copy;
							}
						)
					),
					HasSeenOnboarding = AppConfig.Current.HasSeenOnboarding,
					HasSeenTrayBalloon = AppConfig.Current.HasSeenTrayBalloon
				};

				json = JsonSerializer.Serialize(snapshot, _jsonOptions);
			}

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
				File.WriteAllText(tempPath, json);

				if (File.Exists(_settingsPath))
				{
					File.Replace(tempPath, _settingsPath, _settingsPath + ".bak", ignoreMetadataErrors: true);
				}
				else
				{
					File.Move(tempPath, _settingsPath);
				}
			}
			catch (System.Exception ex)
			{
				try
				{
					string logPath = Path.Combine(GetSettingsDirectory(), "error.log");
					File.AppendAllText(logPath, $"[{System.DateTime.Now:O}] Settings save failed: {ex.Message}{Environment.NewLine}");
				}
				catch { } // Swallow log failure to guarantee no UI interruption
			}
		}

		public static void Export(string targetPath)
		{
			try
			{
				// Ensure current memory state is written to disk first
				Save();
				File.Copy(_settingsPath, targetPath, true);
			}
			catch { }
		}

		public static AppConfig? GetConfigFromFile(string sourcePath)
		{
			try
			{
				if (File.Exists(sourcePath))
				{
					string json = File.ReadAllText(sourcePath);
					return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
				}
			}
			catch { }
			return null;
		}
	}
}