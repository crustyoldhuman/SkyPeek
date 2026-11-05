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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SkyPeek
{
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public static partial class WeatherService
	{
		private static readonly HttpClient _httpClient = new(new System.Net.Http.SocketsHttpHandler
		{
			PooledConnectionLifetime = System.TimeSpan.FromMinutes(5)
		})
		{
			Timeout = System.Threading.Timeout.InfiniteTimeSpan
		};

		private static async Task<HttpResponseMessage> GetWithRetryAsync(string url, int maxRetries = 3)
		{
			for (int i = 0; i < maxRetries; i++)
			{
				try
				{
					using HttpRequestMessage request = new(HttpMethod.Get, url);
					request.Headers.TryAddWithoutValidation("User-Agent", "SkyPeekApp/1.0 (contact@skypeek.app)");
					request.Headers.ConnectionClose = true;

					var response = await _httpClient.SendAsync(request);
					if (!response.IsSuccessStatusCode)
					{
						response.Dispose();
						throw new HttpRequestException($"HTTP status code: {response.StatusCode}");
					}
					return response;
				}
				catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is System.Net.Sockets.SocketException)
				{
					if (i == maxRetries - 1) continue;
					await Task.Delay(2000 * (i + 1));
				}
			}

			using HttpRequestMessage finalRequest = new(HttpMethod.Get, url);
			finalRequest.Headers.TryAddWithoutValidation("User-Agent", "SkyPeekApp/1.0 (contact@skypeek.app)");
			finalRequest.Headers.ConnectionClose = true;
			return await _httpClient.SendAsync(finalRequest);
		}

		private static string SaintPattern => @"\b(st\.?)(?=\s|$)";
		private static readonly Regex _saintRegex = new(SaintPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static string SaintePattern => @"\b(ste\.?)(?=\s|$)";
		private static readonly Regex _sainteRegex = new(SaintePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static string FortPattern => @"\b(ft\.?)(?=\s|$)";
		private static readonly Regex _fortRegex = new(FortPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static string MountPattern => @"\b(mt\.?)(?=\s|$)";
		private static readonly Regex _mountRegex = new(MountPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static string BoldPattern => @"\*\*(.*?)\*\*";
		private static readonly Regex _boldRegex = new(BoldPattern, RegexOptions.Compiled);

		public static string NormalizeLocationName(string input)
		{
			if (string.IsNullOrWhiteSpace(input)) return input;
			string result = input.Trim().Replace("’", "'").Replace("‘", "'");
			result = _saintRegex.Replace(result, "Saint");
			result = _sainteRegex.Replace(result, "Sainte");
			result = _fortRegex.Replace(result, "Fort");
			result = _mountRegex.Replace(result, "Mount");
			return result;
		}

		public static async Task<System.Collections.Generic.List<GeoSearchResult>> SearchLocationsAsync(string query, int count = 5)
		{
			try
			{
				string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count={count}&language=en&format=json";
				using HttpRequestMessage request = new(HttpMethod.Get, url);
				request.Headers.TryAddWithoutValidation("User-Agent", "SkyPeekApp/1.0 (contact@skypeek.app)");
				using HttpResponseMessage response = await _httpClient.SendAsync(request);
				if (!response.IsSuccessStatusCode) return [];

				string json = await response.Content.ReadAsStringAsync();
				using JsonDocument doc = JsonDocument.Parse(json);
				if (!doc.RootElement.TryGetProperty("results", out JsonElement resultsEl) || resultsEl.ValueKind != JsonValueKind.Array)
				{
					return [];
				}

				System.Collections.Generic.List<GeoSearchResult> searchResults = [];
				foreach (JsonElement item in resultsEl.EnumerateArray())
				{
					searchResults.Add(new GeoSearchResult
					{
						Name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
						Admin1 = item.TryGetProperty("admin1", out var a1) ? a1.GetString() ?? "" : "",
						Country = item.TryGetProperty("country", out var c) ? c.GetString() ?? "" : ""
					});
				}
				return searchResults;
			}
			catch
			{
				return [];
			}
		}

		private static string GetUvRating(double uvIndex)
		{
			if (uvIndex < 3) return "Low";
			if (uvIndex < 6) return "Moderate";
			if (uvIndex < 8) return "High";
			if (uvIndex < 11) return "V. High";
			return "Extreme";
		}

		private static (string Value, string Label) GetUnifiedAqi(int? usAqi, int? euAqi)
		{
			if (usAqi.HasValue)
			{
				int val = usAqi.Value;
				if (val <= 50) return (val.ToString(), "Good");
				if (val <= 100) return (val.ToString(), "Fair");
				if (val <= 200) return (val.ToString(), "Poor");
				return (val.ToString(), "Hazard");
			}
			if (euAqi.HasValue)
			{
				int val = euAqi.Value;
				if (val <= 20) return (val.ToString(), "Good");
				if (val <= 60) return (val.ToString(), "Fair");
				if (val <= 100) return (val.ToString(), "Poor");
				return (val.ToString(), "Hazard");
			}
			return ("--", "--");
		}

		public static string GetIconPath(int weatherCode, bool isDaytime)
		{
			return weatherCode switch
			{
				0 => isDaytime ? "pack://application:,,,/IconAssets/0ClearSkyDaytime.json" : "pack://application:,,,/IconAssets/0ClearSkyNighttime.json",
				1 => isDaytime ? "pack://application:,,,/IconAssets/1MainlyClearDaytime.json" : "pack://application:,,,/IconAssets/1MainlyClearNighttime.json",
				2 => isDaytime ? "pack://application:,,,/IconAssets/2PartlyCloudyDaytime.json" : "pack://application:,,,/IconAssets/2PartlyCloudyNighttime.json",
				3 => "pack://application:,,,/IconAssets/3Overcast.json",
				45 => "pack://application:,,,/IconAssets/45Fog.json",
				48 => "pack://application:,,,/IconAssets/48FogRime.json",
				51 => "pack://application:,,,/IconAssets/51DrizzleLight.json",
				53 => "pack://application:,,,/IconAssets/53DrizzleModerate.json",
				55 => "pack://application:,,,/IconAssets/55DrizzleDense.json",
				56 => "pack://application:,,,/IconAssets/56FreezingDrizzleSlight.json",
				57 => "pack://application:,,,/IconAssets/57FreezingDrizzleDense.json",
				61 => "pack://application:,,,/IconAssets/61RainSlight.json",
				63 => "pack://application:,,,/IconAssets/63RainModerate.json",
				65 => "pack://application:,,,/IconAssets/65RainHeavy.json",
				66 => "pack://application:,,,/IconAssets/66FreezingRainSlight.json",
				67 => "pack://application:,,,/IconAssets/67FreezingRainHeavy.json",
				71 => "pack://application:,,,/IconAssets/71SnowfallSlight.json",
				73 => "pack://application:,,,/IconAssets/73SnowfallModerate.json",
				75 => "pack://application:,,,/IconAssets/75SnowfallHeavy.json",
				77 => "pack://application:,,,/IconAssets/77SnowGrains.json",
				80 => isDaytime ? "pack://application:,,,/IconAssets/80RainShowersSlightDaytime.json" : "pack://application:,,,/IconAssets/80RainShowersSlightNighttime.json",
				81 => isDaytime ? "pack://application:,,,/IconAssets/81RainShowersModerateDaytime.json" : "pack://application:,,,/IconAssets/81RainShowersModerateNighttime.json",
				82 => isDaytime ? "pack://application:,,,/IconAssets/82RainShowersViolentDaytime.json" : "pack://application:,,,/IconAssets/82RainShowersViolentNighttime.json",
				85 => isDaytime ? "pack://application:,,,/IconAssets/85SnowShowersSlightDaytime.json" : "pack://application:,,,/IconAssets/85SnowShowersSlightNighttime.json",
				86 => isDaytime ? "pack://application:,,,/IconAssets/86SnowShowersHeavyDaytime.json" : "pack://application:,,,/IconAssets/86SnowShowersHeavyNighttime.json",
				95 => "pack://application:,,,/IconAssets/95Thunderstorm.json",
				96 => "pack://application:,,,/IconAssets/96ThunderstormWithHailSlight.json",
				99 => "pack://application:,,,/IconAssets/99ThunderstormWithHailHeavy.json",
				_ => "pack://application:,,,/IconAssets/3Overcast.json"
			};
		}

		public static async Task<(string City, string State, string Country)?> GetIpLocationAsync()
		{
			try
			{
				string url = "https://api.bigdatacloud.net/data/reverse-geocode-client";
				using HttpResponseMessage response = await GetWithRetryAsync(url);
				if (!response.IsSuccessStatusCode) return null;

				string json = await response.Content.ReadAsStringAsync();
				using JsonDocument doc = JsonDocument.Parse(json);

				string locCity = doc.RootElement.TryGetProperty("city", out var c) ? c.GetString() ?? "" : "";
				if (string.IsNullOrWhiteSpace(locCity))
					locCity = doc.RootElement.TryGetProperty("locality", out var loc) ? loc.GetString() ?? "" : "";

				string locState = doc.RootElement.TryGetProperty("principalSubdivision", out var s) ? s.GetString() ?? "" : "";
				string locCountry = doc.RootElement.TryGetProperty("countryName", out var cou) ? cou.GetString() ?? "" : "";

				if (locCountry == "United States of America" || locCountry == "United States") locCountry = "USA";
				else if (locCountry == "United Kingdom") locCountry = "UK";

				if (string.IsNullOrWhiteSpace(locCity) && string.IsNullOrWhiteSpace(locState)) return null;

				return (locCity, locState, locCountry);
			}
			catch
			{
				return null;
			}
		}

		public static async Task<(string City, string State, string Country)?> GetReverseGeocodeAsync(string lat, string lon)
		{
			try
			{
				string geoUrl = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={lat}&lon={lon}&zoom=10&addressdetails=1";
				using HttpResponseMessage geoResponse = await GetWithRetryAsync(geoUrl);
				if (!geoResponse.IsSuccessStatusCode)
				{
					return null;
				}

				string geoJson = await geoResponse.Content.ReadAsStringAsync();
				using System.Text.Json.JsonDocument geoDoc = System.Text.Json.JsonDocument.Parse(geoJson);

				if (!geoDoc.RootElement.TryGetProperty("address", out var address)) return null;

				string city = address.TryGetProperty("city", out var cProp) ? cProp.GetString() ?? "" : "";
				if (string.IsNullOrWhiteSpace(city)) city = address.TryGetProperty("town", out var tProp) ? tProp.GetString() ?? "" : "";
				if (string.IsNullOrWhiteSpace(city)) city = address.TryGetProperty("village", out var vProp) ? vProp.GetString() ?? "" : "";

				string state = address.TryGetProperty("state", out var sProp) ? sProp.GetString() ?? "" : "";

				string countryCode = address.TryGetProperty("country_code", out var ccProp) ? ccProp.GetString() ?? "" : "";
				string country = countryCode.ToUpperInvariant() switch
				{
					"US" => "USA",
					"GB" => "UK",
					"AU" => "Australia",
					"CA" => "Canada",
					"NZ" => "New Zealand",
					_ => address.TryGetProperty("country", out var cnProp) ? cnProp.GetString() ?? "" : ""
				};

				if (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(state)) return null;

				return (city, state, country);
			}
			catch (System.Exception)
			{
				return null;
			}
		}

		public static async Task<bool> PingApiAsync()
		{
			try
			{
				using HttpRequestMessage request = new(HttpMethod.Get, "https://api.open-meteo.com/v1/forecast?latitude=0&longitude=0&current=temperature_2m");
				request.Headers.TryAddWithoutValidation("User-Agent", "SkyPeekApp/1.0 (contact@skypeek.app)");
				using HttpResponseMessage response = await _httpClient.SendAsync(request);
				return response.IsSuccessStatusCode;
			}
			catch
			{
				return false;
			}
		}

		private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (double Latitude, double Longitude)> _coordCache = new();

		public static async Task<(double Latitude, double Longitude)?> GetCoordinatesAsync(string city, string state, string country)
		{
			string cacheKey = $"{city}|{state}|{country}".ToLowerInvariant();
			if (_coordCache.TryGetValue(cacheKey, out var cached)) return cached;

			string geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city.Trim())}&count=100&language=en&format=json";
			using HttpResponseMessage geoResponse = await GetWithRetryAsync(geoUrl);

			string geoJson = await geoResponse.Content.ReadAsStringAsync();
			using JsonDocument geoDoc = JsonDocument.Parse(geoJson);

			if (!geoDoc.RootElement.TryGetProperty("results", out JsonElement results) || results.GetArrayLength() == 0) return null;

			string cUpper = country.ToUpperInvariant();
			string expectedCountryCode = cUpper switch
			{
				"USA" or "UNITED STATES" or "US" => "US",
				"CANADA" or "CA" => "CA",
				"UK" or "UNITED KINGDOM" or "GB" => "GB",
				"AUSTRALIA" or "AU" => "AU",
				"NEW ZEALAND" or "NZ" => "NZ",
				_ => ""
			};

			foreach (JsonElement element in results.EnumerateArray())
			{
				bool stateMatches = true;
				bool countryMatches = true;

				if (!string.IsNullOrWhiteSpace(state))
				{
					if (element.TryGetProperty("admin1", out JsonElement admin1Obj) && admin1Obj.ValueKind == JsonValueKind.String)
					{
						string? admin1 = admin1Obj.GetString();
						stateMatches = admin1?.Equals(state, StringComparison.OrdinalIgnoreCase) == true;
					}
					else stateMatches = false;
				}

				if (!string.IsNullOrWhiteSpace(expectedCountryCode) && element.TryGetProperty("country_code", out JsonElement countryObj))
				{
					string? countryCode = countryObj.GetString();
					countryMatches = countryCode?.Equals(expectedCountryCode, StringComparison.OrdinalIgnoreCase) == true;
				}

				if (stateMatches && countryMatches)
				{
					var res = (GetSafeDouble(element.GetProperty("latitude")), GetSafeDouble(element.GetProperty("longitude")));
					_coordCache[cacheKey] = res;
					return res;
				}
			}
			var fallbackRes = (GetSafeDouble(results[0].GetProperty("latitude")), GetSafeDouble(results[0].GetProperty("longitude")));
			_coordCache[cacheKey] = fallbackRes;
			return fallbackRes;
		}

		public static async Task<List<CachedWeatherData>> GetWeatherBulkAsync(List<double> lats, List<double> lons, List<string> displayCities, bool useCelsius, bool use24HourClock, bool flipWindArrow, bool useKph, bool useCm)
		{
			if (lats.Count == 0) return [];

			return await Task.Run(async () =>
			{
				string tempUnit = useCelsius ? "celsius" : "fahrenheit";
				string speedUnit = useKph ? "kmh" : "mph";

			string latStr = string.Join(",", lats.Select(l => l.ToString(System.Globalization.CultureInfo.InvariantCulture)));
			string lonStr = string.Join(",", lons.Select(l => l.ToString(System.Globalization.CultureInfo.InvariantCulture)));

			string url = $"https://api.open-meteo.com/v1/forecast?latitude={latStr}&longitude={lonStr}&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,wind_direction_10m,is_day,precipitation,cloud_cover&hourly=temperature_2m,apparent_temperature,precipitation,precipitation_probability,relative_humidity_2m,weather_code,wind_speed_10m,wind_direction_10m,is_day,uv_index&daily=weather_code,temperature_2m_max,temperature_2m_min,apparent_temperature_max,apparent_temperature_min,precipitation_probability_max,precipitation_sum,uv_index_max,wind_speed_10m_max,wind_direction_10m_dominant,sunrise,sunset&forecast_days=12&temperature_unit={tempUnit}&wind_speed_unit={speedUnit}&precipitation_unit=inch&timezone=auto";
			string aqiUrl = $"https://air-quality-api.open-meteo.com/v1/air-quality?latitude={latStr}&longitude={lonStr}&current=us_aqi,european_aqi&hourly=us_aqi,european_aqi,pm10,pm2_5,ozone,nitrogen_dioxide,alder_pollen,birch_pollen,grass_pollen,mugwort_pollen,olive_pollen,ragweed_pollen,us_aqi_pm2_5,us_aqi_pm10,us_aqi_nitrogen_dioxide,us_aqi_ozone&timezone=auto";

			using HttpResponseMessage response = await GetWithRetryAsync(url);
			string jsonResponse = await response.Content.ReadAsStringAsync();
			using JsonDocument doc = JsonDocument.Parse(jsonResponse);

			JsonDocument? aqiDoc = null;
			try
			{
				using HttpResponseMessage aqiResponse = await GetWithRetryAsync(aqiUrl);
				if (aqiResponse.IsSuccessStatusCode)
				{
					string aqiJsonResponse = await aqiResponse.Content.ReadAsStringAsync();
					aqiDoc = JsonDocument.Parse(aqiJsonResponse);
				}
			}
			catch { /* Ignore AQI failures to preserve core weather fetch */ }

				List<CachedWeatherData> results = [];

				try
				{
					if (doc.RootElement.ValueKind == JsonValueKind.Array)
					{
						int i = 0;
						foreach (JsonElement node in doc.RootElement.EnumerateArray())
						{
							if (node.TryGetProperty("error", out _)) { results.Add(null!); i++; continue; }
							JsonElement? aqiNode = null;
							if (aqiDoc != null && aqiDoc.RootElement.ValueKind == JsonValueKind.Array && i < aqiDoc.RootElement.GetArrayLength())
							{
								aqiNode = aqiDoc.RootElement[i];
							}
							results.Add(ParseWeatherData(node, lats[i], lons[i], displayCities[i], use24HourClock, flipWindArrow, useKph, useCm, aqiNode));
							i++;
						}
					}
					else
					{
						if (!doc.RootElement.TryGetProperty("error", out _))
						{
							JsonElement? aqiNode = (aqiDoc != null && !aqiDoc.RootElement.TryGetProperty("error", out _)) ? aqiDoc.RootElement : null;
							results.Add(ParseWeatherData(doc.RootElement, lats[0], lons[0], displayCities[0], use24HourClock, flipWindArrow, useKph, useCm, aqiNode));
						}
					}
				}
				finally
				{
					aqiDoc?.Dispose();
				}

				return results;
			});
		}

		public static CachedWeatherData ParseWeatherData(JsonElement root, double lat, double lon, string displayCity, bool use24HourClock, bool flipWindArrow, bool useKph, bool useCm, JsonElement? aqiRoot = null)
		{
			JsonElement current = root.GetProperty("current");
			JsonElement daily = root.GetProperty("daily");

			DateTime apiNow = DateTime.Parse(current.GetProperty("time").GetString()!);

			CachedWeatherData data = new() { Latitude = lat, Longitude = lon, LastUpdated = DateTime.Now };

			int weatherCode = GetSafeInt(current.GetProperty("weather_code"));
			double currentPrecipCheck = GetSafeDouble(current.GetProperty("precipitation"));
			int currentCloudCover = GetSafeInt(current.GetProperty("cloud_cover"));

			// Open-Meteo CAPE hallucination guardrail
			if (weatherCode >= 95 && currentPrecipCheck == 0)
			{
				if (currentCloudCover < 20)
				{
					weatherCode = currentCloudCover < 10 ? 0 : 1;
				}
				else if (weatherCode > 95)
				{
					weatherCode = 95;
				}
			}

			data.WeatherCode = weatherCode;
			data.Status = GetWeatherStatus(weatherCode);
			data.Temp = Math.Round(GetSafeDouble(current.GetProperty("temperature_2m"))).ToString();
			data.FeelsLike = Math.Round(GetSafeDouble(current.GetProperty("apparent_temperature"))).ToString();
			data.High = Math.Round(daily.GetProperty("temperature_2m_max").GetArrayLength() > 0 ? GetSafeDouble(daily.GetProperty("temperature_2m_max")[0]) : 0).ToString();
			data.Low = Math.Round(daily.GetProperty("temperature_2m_min").GetArrayLength() > 0 ? GetSafeDouble(daily.GetProperty("temperature_2m_min")[0]) : 0).ToString();
			double currentPrecip = daily.GetProperty("precipitation_sum").GetArrayLength() > 0 ? GetSafeDouble(daily.GetProperty("precipitation_sum")[0]) : 0;
			data.Precip = (useCm ? currentPrecip * 2.54 : currentPrecip).ToString("0.00");
			data.PrecipChance = daily.GetProperty("precipitation_probability_max").GetArrayLength() > 0 ? GetSafeInt(daily.GetProperty("precipitation_probability_max")[0]).ToString() : "0";
			data.Humidity = GetSafeInt(current.GetProperty("relative_humidity_2m")).ToString();

			double windSpeed = GetSafeDouble(current.GetProperty("wind_speed_10m"));
			int windDir = GetSafeInt(current.GetProperty("wind_direction_10m"));
			data.WindSpeed = Math.Round(windSpeed).ToString();
			data.WindDir = GetWindDirection(windDir);
			data.WindDegrees = windDir;
			data.WindSpeedUnit = useKph ? "kph" : "mph";

			data.IsDay = GetSafeInt(current.GetProperty("is_day")) == 1;
			data.IconPath = GetIconPath(data.WeatherCode, data.IsDay);

			if (aqiRoot.HasValue && aqiRoot.Value.TryGetProperty("current", out JsonElement aqiCurrent))
			{
				int? usAqi = aqiCurrent.TryGetProperty("us_aqi", out JsonElement usEl) && usEl.ValueKind != JsonValueKind.Null ? usEl.GetInt32() : null;
				int? euAqi = aqiCurrent.TryGetProperty("european_aqi", out JsonElement euEl) && euEl.ValueKind != JsonValueKind.Null ? euEl.GetInt32() : null;

				if (usAqi.HasValue || euAqi.HasValue)
				{
					(data.Aqi, data.AqiRating) = GetUnifiedAqi(usAqi, euAqi);
				}
			}

			if (aqiRoot.HasValue && aqiRoot.Value.TryGetProperty("hourly", out JsonElement aqHourly))
			{
				double peakPm25 = 0, peakPm10 = 0, peakO3 = 0, peakNo2 = 0, peakPollen = 0;
				int maxDriverAqi = -1;
				string primaryDriver = "None";
				int maxAqiPm25 = 0, maxAqiPm10 = 0, maxAqiO3 = 0, maxAqiNo2 = 0;

				double GetAqiVal(string prop, int idx) => aqHourly.TryGetProperty(prop, out JsonElement el) && el.ValueKind == JsonValueKind.Array && idx < el.GetArrayLength() ? GetSafeDouble(el[idx]) : 0;

				int limit = aqHourly.TryGetProperty("time", out JsonElement timeEl) ? Math.Min(24, timeEl.GetArrayLength()) : 24;

				for (int h = 0; h < limit; h++)
				{
					double pm25 = GetAqiVal("pm2_5", h);
					double pm10 = GetAqiVal("pm10", h);
					double o3 = GetAqiVal("ozone", h);
					double no2 = GetAqiVal("nitrogen_dioxide", h);
					double pollen = GetAqiVal("alder_pollen", h) + GetAqiVal("birch_pollen", h) + GetAqiVal("grass_pollen", h) + GetAqiVal("mugwort_pollen", h) + GetAqiVal("olive_pollen", h) + GetAqiVal("ragweed_pollen", h);

					if (pm25 > peakPm25) peakPm25 = pm25;
					if (pm10 > peakPm10) peakPm10 = pm10;
					if (o3 > peakO3) peakO3 = o3;
					if (no2 > peakNo2) peakNo2 = no2;
					if (pollen > peakPollen) peakPollen = pollen;

					int aqiPm25 = (int)GetAqiVal("us_aqi_pm2_5", h);
					int aqiPm10 = (int)GetAqiVal("us_aqi_pm10", h);
					int aqiO3 = (int)GetAqiVal("us_aqi_ozone", h);
					int aqiNo2 = (int)GetAqiVal("us_aqi_nitrogen_dioxide", h);

					if (aqiPm25 > maxDriverAqi) { maxDriverAqi = aqiPm25; primaryDriver = "Fine Particulates (PM2.5)"; }
					if (aqiPm10 > maxDriverAqi) { maxDriverAqi = aqiPm10; primaryDriver = "Coarse Particulates (PM10)"; }
					if (aqiO3 > maxDriverAqi) { maxDriverAqi = aqiO3; primaryDriver = "Ozone (O3)"; }
					if (aqiNo2 > maxDriverAqi) { maxDriverAqi = aqiNo2; primaryDriver = "Nitrogen Dioxide (NO2)"; }

					if (aqiPm25 > maxAqiPm25) maxAqiPm25 = aqiPm25;
					if (aqiPm10 > maxAqiPm10) maxAqiPm10 = aqiPm10;
					if (aqiO3 > maxAqiO3) maxAqiO3 = aqiO3;
					if (aqiNo2 > maxAqiNo2) maxAqiNo2 = aqiNo2;
				}

				data.PeakPm25 = Math.Round(peakPm25, 1).ToString("0.0");
				data.PeakPm10 = Math.Round(peakPm10, 1).ToString("0.0");
				data.PeakO3 = Math.Round(peakO3, 1).ToString("0.0");
				data.PeakNo2 = Math.Round(peakNo2, 1).ToString("0.0");
				data.PeakPollen = Math.Round(peakPollen, 1).ToString("0.0");
				data.PrimaryPollutant = maxDriverAqi > 0 ? primaryDriver : "--";

				data.RatingPm25 = GetUnifiedAqi(maxAqiPm25, null).Label;
				data.RatingPm10 = GetUnifiedAqi(maxAqiPm10, null).Label;
				data.RatingO3 = GetUnifiedAqi(maxAqiO3, null).Label;
				data.RatingNo2 = GetUnifiedAqi(maxAqiNo2, null).Label;
				data.RatingPollen = peakPollen < 50 ? "Good" : (peakPollen < 150 ? "Fair" : (peakPollen < 300 ? "Poor" : "Hazard"));
			}

			string timeFormatWithMeridiem = use24HourClock ? "H:mm" : "h:mm tt";
			string timeFormatWithoutSpace = use24HourClock ? "H:mm" : "h:mmtt";

			string? currentSunriseStr = daily.GetProperty("sunrise").GetArrayLength() > 0 ? daily.GetProperty("sunrise")[0].GetString() : null;
			string? currentSunsetStr = daily.GetProperty("sunset").GetArrayLength() > 0 ? daily.GetProperty("sunset")[0].GetString() : null;
			data.Sunrise = string.IsNullOrEmpty(currentSunriseStr) ? "--:--" : DateTime.Parse(currentSunriseStr).ToString(timeFormatWithMeridiem).ToLower();
			data.Sunset = string.IsNullOrEmpty(currentSunsetStr) ? "--:--" : DateTime.Parse(currentSunsetStr).ToString(timeFormatWithMeridiem).ToLower();

			_ = double.TryParse(data.Precip, out double precipVal);
			string precipText = precipVal > 0 ? $"You can expect some precipitation, with totals reaching around **{data.Precip} {(useCm ? "cm" : "inches")}**." : "Conditions are expected to be **dry** throughout the day.";

			JsonElement hourly = root.GetProperty("hourly");
			JsonElement times = hourly.GetProperty("time");
			JsonElement hTemps = hourly.GetProperty("temperature_2m");
			JsonElement hAppTemps = hourly.GetProperty("apparent_temperature");
			JsonElement hPrecips = hourly.GetProperty("precipitation");
			JsonElement hPrecipProbs = hourly.GetProperty("precipitation_probability");
			JsonElement hHums = hourly.GetProperty("relative_humidity_2m");
			JsonElement hUvs = hourly.GetProperty("uv_index");

			double peakUv = 0;
			DateTime peakUvTime = apiNow;
			for (int i = 0; i < times.GetArrayLength() && i < 24; i++)
			{
				DateTime dt = DateTime.Parse(times[i].GetString()!);
				if (dt.Date == apiNow.Date)
				{
					double uv = GetSafeDouble(hUvs[i]);
					if (uv > peakUv)
					{
						peakUv = uv;
						peakUvTime = dt;
					}
				}
			}

			string uvText = "";
			if (peakUv > 0)
			{
				string formattedUvTime = use24HourClock ? peakUvTime.ToString("H:mm") : peakUvTime.ToString("h tt").ToLower();
				string sunscreenMsg = peakUv >= 3.0 ? " Bring sunscreen!" : "";
				uvText = $" The max UV index will be **{peakUv:0.0}** at **{formattedUvTime}**, which is **{GetUvRating(peakUv).ToLower()}**.{sunscreenMsg}";
			}

			data.DailySummary = $"today will feature **{data.Status.ToLower()}** weather. Temps will reach a high of **{data.High}°** before cooling off to a low of **{data.Low}°** tonight. {precipText} Winds will be blowing from the **{data.WindDir}** at roughly **{data.WindSpeed} {data.WindSpeedUnit}**, with the relative humidity hovering around **{data.Humidity}%**.{uvText}";
			JsonElement hCodes = hourly.GetProperty("weather_code");
			JsonElement hWindSpeeds = hourly.GetProperty("wind_speed_10m");
			JsonElement hWindDirs = hourly.GetProperty("wind_direction_10m");
			JsonElement hIsDays = hourly.GetProperty("is_day");
			JsonElement dSunrises = daily.GetProperty("sunrise");
			JsonElement dSunsets = daily.GetProperty("sunset");

			JsonElement? hUsAqi = null;
			JsonElement? hEuAqi = null;
			if (aqiRoot.HasValue && aqiRoot.Value.TryGetProperty("hourly", out JsonElement aqiHourly))
			{
				if (aqiHourly.TryGetProperty("us_aqi", out JsonElement usEl)) hUsAqi = usEl;
				if (aqiHourly.TryGetProperty("european_aqi", out JsonElement euEl)) hEuAqi = euEl;
			}

			List<HourlyForecast> newHourlyData = [];
			DateTime now = apiNow;
			int added = 0;
			int length = times.GetArrayLength();
			int solarLength = dSunrises.GetArrayLength();

			for (int i = 0; i < length && added < 24; i++)
			{
				DateTime hrTime = DateTime.Parse(times[i].GetString()!);

				if (hrTime >= now.Date.AddHours(now.Hour))
				{
					string timeStr = hrTime.ToString(timeFormatWithoutSpace).ToLower();
					int hDir = GetSafeInt(hWindDirs[i]);
					string solarTime = "";
					string solarIcon = "";
					Visibility solarVis = Visibility.Collapsed;

					for (int j = 0; j < solarLength; j++)
					{
						_ = DateTime.TryParse(dSunrises[j].GetString(), out DateTime sr);
						_ = DateTime.TryParse(dSunsets[j].GetString(), out DateTime ss);

						if (sr.Date == hrTime.Date && sr.Hour == hrTime.Hour)
						{
							solarTime = sr.ToString(timeFormatWithMeridiem).ToLower();
							solarIcon = "m 150.993,164.69593 c 18.50337,5.022 18.30458,4.96805 18.30458,4.96805 M 150.029,129.66587 c 18.51899,-6.25424 18.32133,-6.18749 18.32133,-6.18749 m -37.92558,-22.9104 C 141.9647,85.117008 141.84096,85.282678 141.84096,85.282678 m -49.39568,2.713665 c 0.003,-19.28479 0.003,-19.078015 0.003,-19.078015 M 56.043275,100.56798 C 44.503315,85.117008 44.62705,85.282678 44.62705,85.282678 M 35.245651,129.66587 C 16.726657,123.41163 16.92432,123.47838 16.92432,123.47838 m 16.903227,41.21755 c -18.50337,5.022 -18.304579,4.96805 -18.304579,4.96805 m 29.114047,15.92468 A 60.516072,60.516072 0 0 1 31.930596,148.48835 60.516072,60.516072 0 0 1 92.44667,87.972278 60.516072,60.516072 0 0 1 152.96274,148.48835 v 0 a 60.516072,60.516072 0 0 1 -12.70642,37.10031 M 0,185.68997 c 184.89334,0 184.89334,0 184.89334,0 m 42.21885,-6.71745 c 0,-94.867063 0,-94.420632 0,-94.420632 m -27.68186,26.266412 27.6167,-27.616683 v 0 l 27.6167,27.616683";
							solarVis = Visibility.Visible;
							break;
						}
						if (ss.Date == hrTime.Date && ss.Hour == hrTime.Hour)
						{
							solarTime = ss.ToString(timeFormatWithMeridiem).ToLower();
							solarIcon = "m 150.993,164.69593 c 18.50337,5.022 18.30458,4.96805 18.30458,4.96805 M 150.029,129.66587 c 18.51899,-6.25424 18.32133,-6.18749 18.32133,-6.18749 m -37.92558,-22.9104 C 141.9647,85.117008 141.84096,85.282678 141.84096,85.282678 m -49.39568,2.713665 c 0.003,-19.28479 0.003,-19.078015 0.003,-19.078015 M 56.043275,100.56798 C 44.503315,85.117008 44.62705,85.282678 44.62705,85.282678 M 35.245651,129.66587 C 16.726657,123.41163 16.92432,123.47838 16.92432,123.47838 m 16.903227,41.21755 c -18.50337,5.022 -18.304579,4.96805 -18.304579,4.96805 m 29.114047,15.92468 A 60.516072,60.516072 0 0 1 31.930596,148.48835 60.516072,60.516072 0 0 1 92.44667,87.972278 60.516072,60.516072 0 0 1 152.96274,148.48835 v 0 a 60.516072,60.516072 0 0 1 -12.70642,37.10031 M 0,185.68997 c 184.89334,0 184.89334,0 184.89334,0 M 227.11219,80.529086 c 0,94.867064 0,94.420634 0,94.420634 m -27.68186,-26.26641 27.6167,27.61668 v 0 l 27.6167,-27.61668";
							solarVis = Visibility.Visible;
							break;
						}
					}

					if (added == 0)
					{
						data.UvIndex = GetSafeDouble(hUvs[i]).ToString("0.0");
						data.UvRating = GetUvRating(GetSafeDouble(hUvs[i]));
					}

					int? hrUsAqi = hUsAqi?.ValueKind == JsonValueKind.Array && i < hUsAqi.Value.GetArrayLength() && hUsAqi.Value[i].ValueKind != JsonValueKind.Null ? hUsAqi.Value[i].GetInt32() : null;
					int? hrEuAqi = hEuAqi?.ValueKind == JsonValueKind.Array && i < hEuAqi.Value.GetArrayLength() && hEuAqi.Value[i].ValueKind != JsonValueKind.Null ? hEuAqi.Value[i].GetInt32() : null;
					var (aqiValue, aqiLabel) = GetUnifiedAqi(hrUsAqi, hrEuAqi);

					int rawHourlyCode = GetSafeInt(hCodes[i]);
					double hourlyPrecip = GetSafeDouble(hPrecips[i]);
					int hourlyPrecipProb = GetSafeInt(hPrecipProbs[i]);

					if ((rawHourlyCode >= 51 && (hourlyPrecipProb < 20 || hourlyPrecip <= 0.01)) ||
						(rawHourlyCode >= 95 && hourlyPrecip <= 0.01))
					{
						rawHourlyCode = 3;
					}

					newHourlyData.Add(new HourlyForecast
					{
						RawTime = hrTime,
						BaseTime = timeStr,
						Time = timeStr,
						Location = displayCity,
						Status = GetWeatherStatus(rawHourlyCode),
						WeatherCode = rawHourlyCode,
						Temp = Math.Round(GetSafeDouble(hTemps[i])).ToString(),
						FeelsLike = Math.Round(GetSafeDouble(hAppTemps[i])).ToString(),
						Precip = (useCm ? hourlyPrecip * 2.54 : hourlyPrecip).ToString("0.00"),
						PrecipChance = hourlyPrecipProb.ToString(),
						Humidity = GetSafeInt(hHums[i]).ToString(),
						UvIndex = GetSafeDouble(hUvs[i]).ToString("0.0"),
						UvRating = GetUvRating(GetSafeDouble(hUvs[i])),
						Aqi = aqiValue,
						AqiRating = aqiLabel,
						WindSpeed = Math.Round(GetSafeDouble(hWindSpeeds[i])).ToString(),
						WindDir = GetWindDirection(hDir),
						WindAngle = flipWindArrow ? hDir + 180 : hDir,
						WindSpeedUnit = useKph ? "kph" : "mph",
						IsDay = GetSafeInt(hIsDays[i]) == 1,
						IconPath = GetIconPath(rawHourlyCode, GetSafeInt(hIsDays[i]) == 1),
						SolarEventTime = solarTime,
						SolarEventIconData = solarIcon,
						SolarEventVisibility = solarVis
					});
					added++;
				}
			}
			data.HourlyData = newHourlyData;

			JsonElement dTimes = daily.GetProperty("time");
			JsonElement dCodes = daily.GetProperty("weather_code");
			JsonElement dHighs = daily.GetProperty("temperature_2m_max");
			JsonElement dLows = daily.GetProperty("temperature_2m_min");
			JsonElement dApparentHighs = daily.GetProperty("apparent_temperature_max");
			JsonElement dApparentLows = daily.GetProperty("apparent_temperature_min");
			JsonElement dPrecipProbs = daily.GetProperty("precipitation_probability_max");
			JsonElement dPrecips = daily.GetProperty("precipitation_sum");
			JsonElement dUvs = daily.GetProperty("uv_index_max");
			JsonElement dWindSpeeds = daily.GetProperty("wind_speed_10m_max");
			JsonElement dWindDirs = daily.GetProperty("wind_direction_10m_dominant");

			List<DailyForecast> newDailyData = [];
			int dLength = dTimes.GetArrayLength();

			for (int i = 0; i < dLength; i++)
			{
				DateTime dTime = DateTime.Parse(dTimes[i].GetString()!);
				int dir = dWindDirs[i].ValueKind == JsonValueKind.Null ? 0 : dWindDirs[i].GetInt32();

				int sumHum = 0;
				int validHours = 0;
				Dictionary<int, int> daytimeCodes = [];

				for (int h = 0; h < 24; h++)
				{
					int index = (i * 24) + h;
					if (index < hHums.GetArrayLength())
					{
						sumHum += GetSafeInt(hHums[index]);
						validHours++;
					}

					if (index < hCodes.GetArrayLength() && index < hIsDays.GetArrayLength())
					{
						if (GetSafeInt(hIsDays[index]) == 1)
						{
							int c = GetSafeInt(hCodes[index]);
							daytimeCodes.TryGetValue(c, out int count);
							daytimeCodes[c] = count + 1;
						}
					}
				}
				string avgHum = validHours > 0 ? (sumHum / validHours).ToString() : "--";

				int dailyCode = GetSafeInt(dCodes[i]);
				double dailyPrecip = GetSafeDouble(dPrecips[i]);
				int dailyPrecipProb = GetSafeInt(dPrecipProbs[i]);

				bool isPhantomPrecip = dailyCode >= 51 && (dailyPrecipProb < 20 || dailyPrecip <= 0.01);
				bool isPhantomStorm = dailyCode >= 95 && dailyPrecip <= 0.01;
				bool isMorningFog = dailyCode == 45 || dailyCode == 48;

				if (dailyCode <= 3 || isMorningFog || isPhantomPrecip || isPhantomStorm)
				{
					if (daytimeCodes.Count > 0)
					{
						int dominantDayCode = daytimeCodes.OrderByDescending(kv => kv.Value).First().Key;

						if ((dominantDayCode >= 51 && (dailyPrecipProb < 20 || dailyPrecip <= 0.01)) ||
							(dominantDayCode >= 95 && dailyPrecip <= 0.01))
						{
							dailyCode = 3;
						}
						else
						{
							dailyCode = dominantDayCode;
						}
					}
					else if (isPhantomPrecip || isPhantomStorm)
					{
						dailyCode = 3;
					}
				}

				string suffix = (dTime.Day % 10 == 1 && dTime.Day != 11) ? "st" : (dTime.Day % 10 == 2 && dTime.Day != 12) ? "nd" : (dTime.Day % 10 == 3 && dTime.Day != 13) ? "rd" : "th";
				string baseDateStr = dTime.ToString("ddd, MMM d");
				string baseAgendaStr = $"{dTime:dddd, MMMM d}{suffix}";

				newDailyData.Add(new DailyForecast
				{
					RawDate = dTime.Date,
					BaseDate = baseDateStr,
					BaseAgendaDateHeader = baseAgendaStr,
					Weekday = dTime.ToString("ddd"),
					Date = baseDateStr,
					AgendaDateHeader = baseAgendaStr,
					Location = displayCity,
					Status = GetWeatherStatus(dailyCode),
					High = Math.Round(GetSafeDouble(dHighs[i])).ToString(),
					Low = Math.Round(GetSafeDouble(dLows[i])).ToString(),
					FeelsLikeHigh = Math.Round(GetSafeDouble(dApparentHighs[i])).ToString(),
					FeelsLikeLow = Math.Round(GetSafeDouble(dApparentLows[i])).ToString(),
					PrecipChance = GetSafeInt(dPrecipProbs[i]).ToString(),
					Precip = (useCm ? GetSafeDouble(dPrecips[i]) * 2.54 : GetSafeDouble(dPrecips[i])).ToString("0.00"),
					Humidity = avgHum,
					UvIndex = GetSafeDouble(dUvs[i]).ToString("0.0"),
					UvRating = GetUvRating(GetSafeDouble(dUvs[i])),
					WindSpeed = Math.Round(GetSafeDouble(dWindSpeeds[i])).ToString(),
					WindDir = GetWindDirection(dir),
					WindAngle = flipWindArrow ? dir + 180 : dir,
					WindSpeedUnit = useKph ? "kph" : "mph",
					Sunrise = string.IsNullOrEmpty(dSunrises[i].GetString()) ? "--:--" : DateTime.Parse(dSunrises[i].GetString()!).ToString(timeFormatWithoutSpace).ToLower(),
					Sunset = string.IsNullOrEmpty(dSunsets[i].GetString()) ? "--:--" : DateTime.Parse(dSunsets[i].GetString()!).ToString(timeFormatWithoutSpace).ToLower(),
					IconPath = GetIconPath(dailyCode, true)
				});
			}
			data.DailyData = newDailyData;

			return data;
		}

		private static double GetSafeDouble(JsonElement el) => el.ValueKind == JsonValueKind.Null ? 0.0 : el.GetDouble();
		private static int GetSafeInt(JsonElement el) => el.ValueKind == JsonValueKind.Null ? 0 : el.GetInt32();

		private static string GetWeatherStatus(int code)
		{
			return code switch
			{
				0 => "Clear sky",
				1 => "Mainly clear",
				2 => "Partly cloudy",
				3 => "Overcast",
				45 or 48 => "Fog",
				51 or 53 or 55 => "Drizzle",
				56 or 57 => "Freezing Drizzle",
				61 or 63 or 65 => "Rain",
				66 or 67 => "Freezing Rain",
				71 or 73 or 75 => "Snow",
				77 => "Snow grains",
				80 or 81 or 82 => "Rain showers",
				85 or 86 => "Snow showers",
				95 => "Thunderstorm",
				96 or 99 => "Thunderstorm with hail",
				_ => "Unknown"
			};
		}

		private static readonly string[] _windDirections = ["N", "NE", "E", "SE", "S", "SW", "W", "NW", "N"];

		private static string GetWindDirection(int degrees)
		{
			return _windDirections[(int)Math.Round((degrees % 360) / 45.0)];
		}

		public static readonly DependencyProperty FormattedTextProperty =
			DependencyProperty.RegisterAttached("FormattedText", typeof(string), typeof(WeatherService), new PropertyMetadata(null, OnFormattedTextChanged));

		public static void SetFormattedText(DependencyObject element, string value) => element.SetValue(FormattedTextProperty, value);
		public static string GetFormattedText(DependencyObject element) => (string)element.GetValue(FormattedTextProperty);

		private static void OnFormattedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is System.Windows.Documents.Span span)
			{
				span.Inlines.Clear();
				string text = e.NewValue as string ?? "";
				if (string.IsNullOrEmpty(text)) return;

				var parts = _boldRegex.Split(text);
				for (int i = 0; i < parts.Length; i++)
				{
					if (i % 2 == 1)
					{
						var semiBoldText = new System.Windows.Documents.Run(parts[i])
						{
							FontWeight = System.Windows.FontWeights.SemiBold,
							Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"]
						};
						span.Inlines.Add(semiBoldText);
					}
					else if (!string.IsNullOrEmpty(parts[i]))
					{
						span.Inlines.Add(new System.Windows.Documents.Run(parts[i]));
					}
				}
			}
		}
	}
}