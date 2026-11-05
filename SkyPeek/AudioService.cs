using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SkyPeek
{
	public static class AudioService
	{
		private static readonly SemaphoreSlim _alertAudioLock = new(1, 1);
		private static readonly HashSet<MediaPlayer> _activeMediaPlayers = [];

		public static Task PlayAlertToneAsync(string selectedAlertTone, double alertVolume, int playCount = 1, Func<bool>? shouldContinuePlaying = null)
		{
			return Task.Run(async () =>
			{
				await _alertAudioLock.WaitAsync().ConfigureAwait(false);
				try
				{
					string toneToResolve = string.IsNullOrWhiteSpace(selectedAlertTone) ? "alerttone.wav" : selectedAlertTone;
					string path = toneToResolve;

					if (!Path.IsPathRooted(toneToResolve))
					{
						string localPhysicalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", toneToResolve);
						if (File.Exists(localPhysicalPath))
						{
							path = localPhysicalPath;
						}
						else
						{
							string tempPath = Path.Combine(Path.GetTempPath(), toneToResolve);
							if (!File.Exists(tempPath))
							{
								await Application.Current.Dispatcher.InvokeAsync(() =>
								{
									var resourceInfo = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/{toneToResolve}"));
									if (resourceInfo != null)
									{
										using var fileStream = File.Create(tempPath);
										resourceInfo.Stream.CopyTo(fileStream);
									}
								});
							}
							path = tempPath;
						}
					}

					if (!File.Exists(path))
					{
						string fallbackPhysical = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "alerttone.wav");
						if (File.Exists(fallbackPhysical))
						{
							path = fallbackPhysical;
						}
						else
						{
							string fallbackTemp = Path.Combine(Path.GetTempPath(), "alerttone.wav");
							if (!File.Exists(fallbackTemp))
							{
								await Application.Current.Dispatcher.InvokeAsync(() =>
								{
									var resourceInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/alerttone.wav"));
									if (resourceInfo != null)
									{
										using var fileStream = File.Create(fallbackTemp);
										resourceInfo.Stream.CopyTo(fileStream);
									}
								});
							}
							path = fallbackTemp;
						}
					}

					if (!File.Exists(path)) return;

					for (int i = 0; i < playCount; i++)
					{
						if (shouldContinuePlaying != null && !shouldContinuePlaying()) break;
						var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
						MediaPlayer? player = null;

						await Application.Current.Dispatcher.InvokeAsync(() =>
						{
							player = new MediaPlayer();
							player.MediaOpened += (s, e) => player.Play();
							player.MediaEnded += (s, e) =>
							{
								player.Close();
								lock (_activeMediaPlayers) { _activeMediaPlayers.Remove(player); }
								tcs.TrySetResult(true);
							};
							player.MediaFailed += (s, e) =>
							{
								player.Close();
								lock (_activeMediaPlayers) { _activeMediaPlayers.Remove(player); }
								tcs.TrySetResult(false);
							};
							player.Volume = Math.Clamp(alertVolume, 0.0, 1.0) * 0.95;
							lock (_activeMediaPlayers) { _activeMediaPlayers.Add(player); }
							player.Open(new Uri(path, UriKind.Absolute));
						});

						try
						{
							await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
						}
						catch (TimeoutException)
						{
							if (player != null)
							{
								_ = Application.Current.Dispatcher.InvokeAsync(() => player.Close());
								lock (_activeMediaPlayers)
								{
									_activeMediaPlayers.Remove(player);
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"AudioService playback exception: {ex.Message}");
				}
				finally
				{
					_alertAudioLock.Release();
				}
			});
		}
	}
}