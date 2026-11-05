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
using System.Windows.Media;

namespace SkyPeek
{
	public static partial class AlertService
	{
		private static readonly HttpClient _httpClient = new(new System.Net.Http.SocketsHttpHandler
		{
			PooledConnectionLifetime = System.TimeSpan.FromMinutes(5)
		})
		{
			Timeout = System.TimeSpan.FromSeconds(15)
		};

		private static async Task<(string City, string State, string Country)?> GetReverseGeocodeAsync(string lat, string lon)
		{
			try
			{
				string geoUrl = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={lat}&lon={lon}&zoom=10&addressdetails=1";
				using HttpRequestMessage request = new(HttpMethod.Get, geoUrl);
				request.Headers.TryAddWithoutValidation("User-Agent", "SkyPeekApp/1.0 (contact@skypeek.app)");

				using HttpResponseMessage geoResponse = await _httpClient.SendAsync(request);
				if (!geoResponse.IsSuccessStatusCode) return null;

				string geoJson = await geoResponse.Content.ReadAsStringAsync();
				using JsonDocument geoDoc = JsonDocument.Parse(geoJson);

				if (!geoDoc.RootElement.TryGetProperty("address", out var address)) return null;

				string city = address.TryGetProperty("city", out var cProp) ? cProp.GetString() ?? "" : "";
				if (string.IsNullOrWhiteSpace(city)) city = address.TryGetProperty("town", out var tProp) ? tProp.GetString() ?? "" : "";
				if (string.IsNullOrWhiteSpace(city)) city = address.TryGetProperty("village", out var vProp) ? vProp.GetString() ?? "" : "";

				string state = address.TryGetProperty("state", out var sProp) ? sProp.GetString() ?? "" : "";
				string countryCode = address.TryGetProperty("country_code", out var ccProp) ? ccProp.GetString() ?? "" : "";

				if (countryCode.Equals("gb", StringComparison.OrdinalIgnoreCase) ||
					countryCode.Equals("uk", StringComparison.OrdinalIgnoreCase) ||
					countryCode.Equals("au", StringComparison.OrdinalIgnoreCase))
				{
					List<string> subRegions = [];
					if (!string.IsNullOrWhiteSpace(state)) subRegions.Add(state);
					foreach (var prop in address.EnumerateObject())
					{
						if (prop.Value.ValueKind == JsonValueKind.String)
						{
							string val = prop.Value.GetString() ?? "";
							string nameLower = prop.Name.ToLowerInvariant();
							if (!string.IsNullOrWhiteSpace(val) && val.Length > 2 &&
								!val.Equals("England", StringComparison.OrdinalIgnoreCase) &&
								!val.Equals("Scotland", StringComparison.OrdinalIgnoreCase) &&
								!val.Equals("Wales", StringComparison.OrdinalIgnoreCase) &&
								!val.Equals("Northern Ireland", StringComparison.OrdinalIgnoreCase) &&
								!val.Equals("United Kingdom", StringComparison.OrdinalIgnoreCase) &&
								!val.Equals("Australia", StringComparison.OrdinalIgnoreCase) &&
								nameLower != "country" && nameLower != "country_code" && nameLower != "postcode" && !nameLower.StartsWith("iso3166"))
							{
								subRegions.Add(val);
							}
						}
					}
					if (subRegions.Count > 0) state = string.Join("|", subRegions.Distinct(StringComparer.OrdinalIgnoreCase));
				}

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
			catch
			{
				return null;
			}
		}

		private static string ParagraphSplitPattern => @"\n[ \t]*\n+";
		private static readonly Regex _paragraphSplitRegex = new(ParagraphSplitPattern, RegexOptions.Compiled);

		private static string WhitespaceCleanupPattern => @"[ \t]{2,}";
		private static readonly Regex _whitespaceCleanupRegex = new(WhitespaceCleanupPattern, RegexOptions.Compiled);

		private static string ScriptTagPattern => @"<script[\s\S]*?</script>";
		private static readonly Regex _scriptTagRegex = new(ScriptTagPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static string StyleTagPattern => @"<style[\s\S]*?</style>";
		private static readonly Regex _styleTagRegex = new(StyleTagPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static string HtmlTagPattern => @"<[^>]*>";
		private static readonly Regex _htmlTagRegex = new(HtmlTagPattern, RegexOptions.Compiled);

		private static string MultipleSpacesPattern => @"[ \t]+";
		private static readonly Regex _multipleSpacesRegex = new(MultipleSpacesPattern, RegexOptions.Compiled);

		private static string MultipleNewlinesPattern => @"\n\s*\n+";
		private static readonly Regex _multipleNewlinesRegex = new(MultipleNewlinesPattern, RegexOptions.Compiled);

		private static string ExpirationExtractionPattern => @"(?:until|valid to)\s+([A-Za-z0-9\s:]+?)(?:\.|,|\n|$)";
		private static readonly Regex _expirationExtractionRegex = new(ExpirationExtractionPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static string GuidPattern => @"id=([a-f0-9\-]+)";
		private static readonly Regex _guidRegex = new(GuidPattern, RegexOptions.Compiled);

		private static string WarningHeadPattern => @"(?s)<div class=""warning-detail"">\s*<h2>(.*?)</h2>";
		private static readonly Regex _warningHeadRegex = new(WarningHeadPattern, RegexOptions.Compiled);

		private static string WarningExpectPattern => @"(?s)<h3>What should I expect\?</h3>\s*<ul>(.*?)</ul>";
		private static readonly Regex _warningExpectRegex = new(WarningExpectPattern, RegexOptions.Compiled);

		private static string WarningListItemsPattern => @"(?s)<li>(.*?)</li>";
		private static readonly Regex _warningListItemsRegex = new(WarningListItemsPattern, RegexOptions.Compiled);

		private static string WarningDetailPattern => @"(?s)<h3>Further detail</h3>\s*<p>(.*?)</p>";
		private static readonly Regex _warningDetailRegex = new(WarningDetailPattern, RegexOptions.Compiled);

		private static string ThreeOrMoreNewlinesPattern => @"\n{3,}";
		private static readonly Regex _threeOrMoreNewlinesRegex = new(ThreeOrMoreNewlinesPattern, RegexOptions.Compiled);

		private static string WarningIssuedPattern => @"(?s)<section class=""issued-date"">\s*<h3>Issued</h3>\s*<p>\s*<time[^>]*>(.*?)</time>";
		private static readonly Regex _warningIssuedRegex = new(WarningIssuedPattern, RegexOptions.Compiled);

		private static string MetOfficePattern => @"(.*?) affecting (.*?)(?: valid from (.*?) to (.*))?$";
		private static readonly Regex _metOfficeRegex = new(MetOfficePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

		private static string BomIdPattern => @"ID[A-Z]\d{5}";
		private static readonly Regex _bomIdRegex = new(BomIdPattern, RegexOptions.Compiled);

		private static string BomTitlePrefixPattern => @"^\d{1,2}/\d{2}:\d{2}(?:\s+[A-Za-z]+){1,2}\s+";
		private static readonly Regex _bomTitlePrefixRegex = new(BomTitlePrefixPattern, RegexOptions.Compiled);

		private static string TitleForRegionPattern => @"^(.*?)\s+for\s+(.*)$";
		private static readonly Regex _titleForRegionRegex = new(TitleForRegionPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static string ForWhitespacePattern => @"\bfor\b\s+";
		private static readonly Regex _forWhitespaceRegex = new(ForWhitespacePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

		public static async Task<List<WeatherAlert>> GetAlertsAsync(double lat, double lon, string cityName, string country, HashSet<string> dismissedAlerts, int alertFilterLevel)
		{
			if (alertFilterLevel == 0) return [];

			// Snapshot the HashSet on the calling (UI) thread before handing it off to the background thread pool
			HashSet<string> dismissedSnapshot = dismissedAlerts?.ToHashSet() ?? [];

			return await Task.Run<List<WeatherAlert>>(async () =>
			{
				try
				{
					string cUpper = country.ToUpperInvariant();
					List<WeatherAlert> alerts = [];
					if (cUpper == "USA" || cUpper == "UNITED STATES" || cUpper == "US")
						alerts = await GetUsaAlertsAsync(lat, lon, dismissedSnapshot, alertFilterLevel);
					else if (cUpper == "CANADA" || cUpper == "CA")
						alerts = await GetCanadaAlertsAsync(lat, lon, dismissedSnapshot, alertFilterLevel);
					else if (cUpper == "AUSTRALIA" || cUpper == "AU")
						alerts = await GetAustraliaAlertsAsync(lat, lon, cityName, dismissedSnapshot, alertFilterLevel);
					else if (cUpper == "UK" || cUpper == "UNITED KINGDOM" || cUpper == "GB")
						alerts = await GetUkAlertsAsync(lat, lon, cityName, dismissedSnapshot, alertFilterLevel);
					else if (cUpper == "NEW ZEALAND" || cUpper == "NZ")
						alerts = await GetNewZealandAlertsAsync(lat, lon, dismissedSnapshot, alertFilterLevel);

					return [.. alerts.OrderByDescending(a => a.SeverityLevel)];
			}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Alert fetch error ({country}): {ex.Message}");
					return [];
				}
			});
		}

		private static async Task<List<WeatherAlert>> GetUkAlertsAsync(double lat, double lon, string cityName, HashSet<string> dismissedAlerts, int alertFilterLevel)
		{
			List<WeatherAlert> alerts = [];
			System.Diagnostics.Debug.WriteLine($"[UK ALERTS] Starting fetch for {cityName} (Lat: {lat}, Lon: {lon})");

			var geo = await GetReverseGeocodeAsync(lat.ToString(System.Globalization.CultureInfo.InvariantCulture), lon.ToString(System.Globalization.CultureInfo.InvariantCulture));

			static string CleanUkTerm(string? input)
			{
				string s = (input ?? "").ToLowerInvariant()
					.Replace("city of ", "").Replace(" city", "")
					.Replace(" council", "").Replace(" unitary authority", "")
					.Replace("&", "and").Replace(",", "").Replace("-", " ").Trim();

				s = _multipleSpacesRegex.Replace(s, " ").Trim();

				return s is "england" or "scotland" or "wales" or "northern ireland" or "united kingdom" or "uk" or "gb" ? "" : s;
			}

			string locCity = CleanUkTerm(geo?.City);
			string fallbackCity = CleanUkTerm(cityName);

			List<string> rawTokens = [locCity, fallbackCity];
			if (!string.IsNullOrWhiteSpace(geo?.State))
			{
				foreach (string part in geo.Value.State.Split('|', StringSplitOptions.RemoveEmptyEntries))
				{
					rawTokens.Add(CleanUkTerm(part));
				}
			}

			string[] matchTokens = rawTokens
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.SelectMany(s => s.Split('/', StringSplitOptions.RemoveEmptyEntries))
				.Select(s => s.Trim())
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Distinct()
				.ToArray();

			System.Diagnostics.Debug.WriteLine($"[UK ALERTS] Match Tokens -> {string.Join(", ", matchTokens.Select(t => $"'{t}'"))}");

			if (matchTokens.Length == 0)
			{
				System.Diagnostics.Debug.WriteLine("[UK ALERTS] All location identifiers are empty. Aborting fetch.");
				return alerts;
			}

			string ukUrl = "https://www.metoffice.gov.uk/public/data/PWSCache/WarningsRSS/Region/UK";
			System.Diagnostics.Debug.WriteLine($"[UK ALERTS] Requesting RSS feed: {ukUrl}");

			try
			{
				using HttpRequestMessage request = new(HttpMethod.Get, ukUrl);
				request.Headers.TryAddWithoutValidation("User-Agent", "SkyPeekApp/1.0 (contact@skypeek.app)");
				request.Headers.TryAddWithoutValidation("Accept", "application/rss+xml, application/xml, text/xml");

				using HttpResponseMessage response = await _httpClient.SendAsync(request);
				System.Diagnostics.Debug.WriteLine($"[UK ALERTS] HTTP Status: {response.StatusCode}");

				if (!response.IsSuccessStatusCode) return alerts;

				using System.IO.Stream xmlStream = await response.Content.ReadAsStreamAsync();
				System.Xml.XmlDocument xmlDoc = new();
				xmlDoc.Load(xmlStream);

				System.Xml.XmlNodeList? items = xmlDoc.GetElementsByTagName("item");
				System.Diagnostics.Debug.WriteLine($"[UK ALERTS] XML loaded. Found {items?.Count ?? 0} 'item' nodes.");

				if (items != null && items.Count > 0)
				{
					string? cachedUkWarningsHtml = null;
					try
					{
						using HttpRequestMessage pageRequest = new(HttpMethod.Get, "https://weather.metoffice.gov.uk/warnings-and-advice/uk-warnings");
						pageRequest.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
						pageRequest.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");

						using var htmlResponse = await _httpClient.SendAsync(pageRequest);
						if (htmlResponse.IsSuccessStatusCode)
						{
							cachedUkWarningsHtml = await htmlResponse.Content.ReadAsStringAsync();
						}
					}
					catch { /* Silently fallback to standard RSS description on fetch failure */ }

					string NormalizeMatchString(string input) => _multipleSpacesRegex.Replace((input ?? "").ToLowerInvariant().Replace("&", "and").Replace(",", "").Replace("-", " "), " ");

					foreach (System.Xml.XmlNode item in items)
					{
						string title = item.SelectSingleNode("title")?.InnerText ?? "Unknown Warning";
						string description = item.SelectSingleNode("description")?.InnerText ?? "No description provided.";
						string link = item.SelectSingleNode("link")?.InnerText ?? "";

						string titleLower = NormalizeMatchString(title);
						string descLower = NormalizeMatchString(description);

						string guid = _guidRegex.Match(link).Groups[1].Value;
						string blockLower = "";

						// Look-ahead: Extract HTML block early so we can match against hidden sub-counties (e.g. Cornwall)
						if (!string.IsNullOrWhiteSpace(guid) && !string.IsNullOrWhiteSpace(cachedUkWarningsHtml))
						{
							var earlyBlockMatch = Regex.Match(cachedUkWarningsHtml, $@"(?s)id=""warningSidebar_{guid}"".*?(?=<section class=""warning-content-matrix"")");
							if (earlyBlockMatch.Success) blockLower = NormalizeMatchString(earlyBlockMatch.Groups[0].Value);
						}

						var match = _metOfficeRegex.Match(description);
						string affectedAuthoritiesLower = match.Success ? NormalizeMatchString(match.Groups[2].Value) : descLower;

						bool affectsLocation = matchTokens.Any(token =>
						{
							string pattern = $@"\b{Regex.Escape(token)}\b";
							return Regex.IsMatch(titleLower, pattern) ||
								   Regex.IsMatch(affectedAuthoritiesLower, pattern) ||
								   (!string.IsNullOrWhiteSpace(blockLower) && Regex.IsMatch(blockLower, pattern));
						});

						if (!affectsLocation)
						{
							System.Diagnostics.Debug.WriteLine($"[UK ALERTS] SKIPPING: '{title}' (Does not match location tokens)");
							continue;
						}

						System.Diagnostics.Debug.WriteLine($"[UK ALERTS] MATCH FOUND! Alert '{title}' affects this location.");

						string pubDate = item.SelectSingleNode("pubDate")?.InnerText ?? "";
						string alertId = !string.IsNullOrWhiteSpace(guid) ? $"UKMET_{guid}" : $"UKMET_{title}_{pubDate}".Replace(" ", "_");

						if ((dismissedAlerts != null && dismissedAlerts.Contains(alertId)) || alerts.Any(a => a.Id == alertId))
						{
							System.Diagnostics.Debug.WriteLine($"[UK ALERTS] IGNORING: Alert '{alertId}' is already active or dismissed.");
							continue;
						}

						string severity = "minor";
						if (titleLower.Contains("red")) severity = "extreme";
						else if (titleLower.Contains("amber") || titleLower.Contains("orange")) severity = "severe";
						else if (titleLower.Contains("yellow")) severity = "moderate";

						if (GetSeverityLevel(severity) < alertFilterLevel)
						{
							System.Diagnostics.Debug.WriteLine($"[UK ALERTS] FILTERED OUT: Severity '{severity}' is below user threshold of {alertFilterLevel}.");
							continue;
						}

						System.Diagnostics.Debug.WriteLine($"[UK ALERTS] SUCCESSFULLY ADDED: '{alertId}' with severity '{severity}'.");

						string? payloadColor = item.SelectSingleNode("color")?.InnerText;

						string formattedDesc = FormatAlertDescription(description);
						string expiresTime = ExtractExpirationFromText(titleLower + " " + descLower);
						string deepDetails = "";
						string issuedTime = "";

						if (!string.IsNullOrWhiteSpace(guid) && !string.IsNullOrWhiteSpace(cachedUkWarningsHtml))
						{
							try
							{
								if (!string.IsNullOrWhiteSpace(cachedUkWarningsHtml))
								{
									var blockMatch = Regex.Match(cachedUkWarningsHtml, $@"(?s)id=""warningSidebar_{guid}"".*?(?=<section class=""warning-content-matrix"")");

									if (blockMatch.Success)
									{
										string block = blockMatch.Groups[0].Value;

										var headMatch = _warningHeadRegex.Match(block);
										if (headMatch.Success) deepDetails += $"{headMatch.Groups[1].Value.Trim()}\n";

										var expectMatch = _warningExpectRegex.Match(block);
										if (expectMatch.Success)
										{
											deepDetails += "What should I expect?\n";
											var listItems = _warningListItemsRegex.Matches(expectMatch.Groups[1].Value);
											foreach (Match li in listItems)
											{
												deepDetails += $"  • {li.Groups[1].Value.Trim()}\n";
											}
											deepDetails += "\n";
										}

										var detailMatch = _warningDetailRegex.Match(block);
										if (detailMatch.Success)
										{
											string detailText = detailMatch.Groups[1].Value.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n").Trim();
											deepDetails += $"{_threeOrMoreNewlinesRegex.Replace(detailText, "\n\n")}\n\n";
										}

										var issuedMatch = _warningIssuedRegex.Match(block);
										if (issuedMatch.Success)
										{
											issuedTime = $"{issuedMatch.Groups[1].Value.Trim()}\n";
										}
									}
								}
							}
							catch { /* Silently fallback to RSS description on failure */ }
						}

						if (match.Success)
						{
							string regions = match.Groups[2].Value.Trim();
							formattedDesc = _threeOrMoreNewlinesRegex.Replace($"{deepDetails}{issuedTime}Regions and local authorities affected:\n{regions}".Trim(), "\n\n");

							if (match.Groups[4].Success && !string.IsNullOrWhiteSpace(match.Groups[4].Value))
							{
								expiresTime = match.Groups[4].Value.Trim();
							}
						}
						else if (!string.IsNullOrWhiteSpace(deepDetails))
						{
							formattedDesc = _threeOrMoreNewlinesRegex.Replace($"{deepDetails}{issuedTime}".Trim(), "\n\n");
						}

						alerts.Add(new WeatherAlert
						{
							Id = alertId,
							Event = CapitalizeFirst(title),
							Sender = "Met Office",
							Description = formattedDesc + (string.IsNullOrWhiteSpace(link) ? "" : $"\n\nMore info: {link}") + $"\n\nExpires: {expiresTime}",
							SeverityColor = GetColorForSeverity(severity, payloadColor),
							SeverityLevel = GetSeverityLevel(severity)
						});
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[UK ALERTS] Exception caught: {ex}");
			}

			System.Diagnostics.Debug.WriteLine($"[UK ALERTS] Finished. Returning {alerts.Count} total alerts.");
			return alerts;
		}

		private static async Task<List<WeatherAlert>> GetUsaAlertsAsync(double lat, double lon, HashSet<string> dismissedAlerts, int alertFilterLevel)
		{
			List<WeatherAlert> alerts = [];
			string alertsUrl = $"https://api.weather.gov/alerts/active?point={Math.Round(lat, 4).ToString(System.Globalization.CultureInfo.InvariantCulture)},{Math.Round(lon, 4).ToString(System.Globalization.CultureInfo.InvariantCulture)}";
			using HttpRequestMessage request = new(HttpMethod.Get, alertsUrl);
			request.Headers.TryAddWithoutValidation("User-Agent", "(SkyPeekApp, contact@skypeek.app)");
			request.Headers.TryAddWithoutValidation("Accept", "application/geo+json");

			using HttpResponseMessage response = await _httpClient.SendAsync(request);
			if (!response.IsSuccessStatusCode) return alerts;

			string json = await response.Content.ReadAsStringAsync();
			using JsonDocument doc = JsonDocument.Parse(json);
			if (doc.RootElement.TryGetProperty("features", out JsonElement features) && features.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement feature in features.EnumerateArray())
				{
					if (!feature.TryGetProperty("properties", out JsonElement props)) continue;
					string evName = props.TryGetProperty("event", out JsonElement evProp) && evProp.ValueKind == JsonValueKind.String ? evProp.GetString() ?? "Unknown" : "Unknown";
					string expiresTime = props.TryGetProperty("expires", out JsonElement expProp) && expProp.ValueKind == JsonValueKind.String ? expProp.GetString() ?? "NoExp" : "NoExp";
					string alertId = $"NWS_{evName}_{expiresTime}".Replace(" ", "_");

					if ((dismissedAlerts != null && dismissedAlerts.Contains(alertId)) || alerts.Any(a => a.Id == alertId)) continue;

					string severity = props.TryGetProperty("severity", out JsonElement sevProp) && sevProp.ValueKind == JsonValueKind.String ? sevProp.GetString() ?? "" : "";
					if (GetSeverityLevel(severity) < alertFilterLevel) continue;

					string? payloadColor = props.TryGetProperty("color", out JsonElement colProp) && colProp.ValueKind == JsonValueKind.String ? colProp.GetString() : null;
					if (string.IsNullOrWhiteSpace(payloadColor) && props.TryGetProperty("parameters", out JsonElement paramProps))
					{
						if (paramProps.TryGetProperty("color", out JsonElement colorArr) && colorArr.ValueKind == JsonValueKind.Array && colorArr.GetArrayLength() > 0)
							payloadColor = colorArr[0].GetString();
						else if (paramProps.TryGetProperty("eventColor", out JsonElement evColorArr) && evColorArr.ValueKind == JsonValueKind.Array && evColorArr.GetArrayLength() > 0)
							payloadColor = evColorArr[0].GetString();
					}

					alerts.Add(new WeatherAlert
					{
						Id = alertId,
						Event = CapitalizeFirst(evName),
						Sender = props.TryGetProperty("senderName", out JsonElement senderProp) && senderProp.ValueKind == JsonValueKind.String ? senderProp.GetString() ?? "National Weather Service" : "National Weather Service",
						Description = FormatAlertDescription(props.TryGetProperty("description", out JsonElement descProp) && descProp.ValueKind != JsonValueKind.Null ? descProp.GetString() : null) + $" Expires: {FormatExpirationDate(expiresTime)}",
						SeverityColor = GetColorForSeverity(severity, payloadColor),
						SeverityLevel = GetSeverityLevel(severity)
					});
				}
			}

			return alerts;
		}

		private static readonly string[] _bomFooterMarkers = ["Warnings Information", "Graphical Views", "This page was created at", "The Bureau and State Emergency Service would appreciate", "Warnings\n\nWater"];

		private static async Task<List<WeatherAlert>> GetAustraliaAlertsAsync(double lat, double lon, string cityName, HashSet<string> dismissedAlerts, int alertFilterLevel)
		{
			List<WeatherAlert> alerts = [];
			var geo = await GetReverseGeocodeAsync(lat.ToString(System.Globalization.CultureInfo.InvariantCulture), lon.ToString(System.Globalization.CultureInfo.InvariantCulture));
			string rawState = (geo?.State ?? "").ToLowerInvariant();
			string stateAbbrev = rawState switch
			{
				var s when s.Contains("victoria") || s.Split('|').Contains("vic") => "IDZ00059.warnings_vic",
				var s when s.Contains("queensland") || s.Split('|').Contains("qld") => "IDZ00056.warnings_qld",
				var s when s.Contains("western australia") || s.Split('|').Contains("wa") => "IDZ00060.warnings_wa",
				var s when s.Contains("south australia") || s.Split('|').Contains("sa") => "IDZ00057.warnings_sa",
				var s when s.Contains("tasmania") || s.Split('|').Contains("tas") => "IDZ00058.warnings_tas",
				var s when s.Contains("northern territory") || s.Split('|').Contains("nt") => "IDZ00055.warnings_nt",
				_ => "IDZ00054.warnings_nsw" // Covers NSW, ACT, and acts as fallback
			};

			static string CleanAuTerm(string? input)
			{
				string s = (input ?? "").ToLowerInvariant()
					.Replace("city of ", "").Replace(" city", "")
					.Replace("shire of ", "").Replace(" shire", "")
					.Replace("council of ", "").Replace(" council", "")
					.Replace(" regional", "").Replace(" municipality", "")
					.Replace("&", "and").Replace(",", "").Replace("-", " ").Trim();
				s = _multipleSpacesRegex.Replace(s, " ").Trim();
				return s is "new south wales" or "nsw" or "victoria" or "vic" or "queensland" or "qld" or "western australia" or "wa" or "south australia" or "sa" or "tasmania" or "tas" or "northern territory" or "nt" or "australian capital territory" or "act" or "australia" or "au" ? "" : s;
			}

			List<string> rawTokens = [CleanAuTerm(geo?.City), CleanAuTerm(cityName)];
			if (!string.IsNullOrWhiteSpace(geo?.State))
			{
				foreach (string part in geo.Value.State.Split('|', StringSplitOptions.RemoveEmptyEntries))
				{
					rawTokens.Add(CleanAuTerm(part));
				}
			}

			string[] matchTokens = rawTokens
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.SelectMany(s => s.Split('/', StringSplitOptions.RemoveEmptyEntries))
				.Select(s => s.Trim())
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Distinct()
				.ToArray();

			string bomUrl = $"https://www.bom.gov.au/fwo/{stateAbbrev}.xml";
			using HttpRequestMessage request = new(HttpMethod.Get, bomUrl);
			request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
			request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
			request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

			using HttpResponseMessage response = await _httpClient.SendAsync(request);
			if (!response.IsSuccessStatusCode)
			{
				System.Diagnostics.Debug.WriteLine($"BOM Alert Fetch Failed: {response.StatusCode}");
				return alerts;
			}

			using System.IO.Stream xmlStream = await response.Content.ReadAsStreamAsync();
			System.Xml.XmlDocument xmlDoc = new();
			xmlDoc.Load(xmlStream);

			System.Xml.XmlNodeList items = xmlDoc.GetElementsByTagName("item");
			if (items != null)
			{
				foreach (System.Xml.XmlNode item in items)
				{
					string rawTitle = item.SelectSingleNode("title")?.InnerText ?? "Unknown Warning";
					rawTitle = _multipleSpacesRegex.Replace(rawTitle.Replace("\n", " ").Replace("\r", " ").Replace('\xA0', ' '), " ").Trim();

					string description = item.SelectSingleNode("description")?.InnerText ?? "No description provided.";

					if (rawTitle.Contains("Cancellation", StringComparison.OrdinalIgnoreCase) ||
						rawTitle.Contains("Warning Summary", StringComparison.OrdinalIgnoreCase)) continue;

					rawTitle = _bomTitlePrefixRegex.Replace(rawTitle, "").Trim();

					string title = rawTitle;
					string regionText = "";
					var titleMatch = _titleForRegionRegex.Match(rawTitle);
					if (titleMatch.Success)
					{
						title = titleMatch.Groups[1].Value.Trim();
						regionText = "for " + titleMatch.Groups[2].Value.Trim() + "\n\n";
					}

					string link = item.SelectSingleNode("link")?.InnerText ?? "";
					string pubDate = item.SelectSingleNode("pubDate")?.InnerText ?? "";

					string severity = "minor";
					string titleLower = title.ToLower();

					if (titleLower.Contains("extreme") || titleLower.Contains("catastrophic")) severity = "extreme";
					else if (titleLower.Contains("severe") || titleLower.Contains("major")) severity = "severe";
					else if (titleLower.Contains("warning")) severity = "moderate";

					if (GetSeverityLevel(severity) < alertFilterLevel) continue;

					string? payloadColor = item.SelectSingleNode("color")?.InnerText;

					string detailedDesc = description;
					if (!string.IsNullOrWhiteSpace(link))
					{
						try
						{
							using var pageResponse = await _httpClient.GetAsync(link);
							if (pageResponse.IsSuccessStatusCode)
							{
								string html = await pageResponse.Content.ReadAsStringAsync();
								string rawText = _scriptTagRegex.Replace(html, "");
								rawText = _styleTagRegex.Replace(rawText, "");
								rawText = _htmlTagRegex.Replace(rawText, "\n");
								rawText = System.Net.WebUtility.HtmlDecode(rawText);

								rawText = _multipleSpacesRegex.Replace(rawText, " ");
								rawText = _multipleNewlinesRegex.Replace(rawText, "\n\n").Trim();

								var idMatch = _bomIdRegex.Match(rawText);
								if (idMatch.Success)
								{
									rawText = rawText[(idMatch.Index + idMatch.Length)..].Trim();
								}

								int firstTitle = rawText.IndexOf(title, StringComparison.OrdinalIgnoreCase);
								if (firstTitle >= 0)
								{
									int secondTitle = rawText.IndexOf(title, firstTitle + title.Length, StringComparison.OrdinalIgnoreCase);
									rawText = secondTitle > 0 ? rawText[(secondTitle + title.Length)..].Trim() : rawText[(firstTitle + title.Length)..].Trim();
								}

								rawText = _forWhitespaceRegex.Replace(rawText, "for ");

								foreach (var marker in _bomFooterMarkers)
								{
									int endIndex = rawText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
									if (endIndex > 0) rawText = rawText[..endIndex].Trim();
								}
								detailedDesc = rawText;
								regionText = "";
							}
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.WriteLine($"BOM live fetch failure: {ex.Message}");
						}
					}

					if (matchTokens.Length > 0)
					{
						string searchableText = _multipleSpacesRegex.Replace($"{rawTitle} {regionText} {detailedDesc}".ToLowerInvariant().Replace("&", "and").Replace(",", "").Replace("-", " "), " ");
						bool affectsLocation = matchTokens.Any(token => Regex.IsMatch(searchableText, $@"\b{Regex.Escape(token)}\b"));
						if (!affectsLocation) continue;
					}

					int textHash = 0;
					foreach (char c in detailedDesc) textHash = unchecked(textHash * 31 + c);
					string alertId = $"BOM_{title}_{pubDate}_{textHash}".Replace(" ", "_");

					if ((dismissedAlerts != null && dismissedAlerts.Contains(alertId)) || alerts.Any(a => a.Id == alertId)) continue;

					alerts.Add(new WeatherAlert
					{
						Id = alertId,
						Event = CapitalizeFirst(title),
						Sender = "Bureau of Meteorology",
						Description = regionText + FormatAlertDescription(detailedDesc) + (string.IsNullOrWhiteSpace(link) ? "" : $"\n\nMore info: {link}") + $"\n\nExpires: {ExtractExpirationFromText(detailedDesc)}",
						SeverityColor = GetColorForSeverity(severity, payloadColor),
						SeverityLevel = GetSeverityLevel(severity)
					});
				}
			}
			return alerts;
		}

		private static async Task<List<WeatherAlert>> GetCanadaAlertsAsync(double lat, double lon, HashSet<string> dismissedAlerts, int alertFilterLevel)
		{
			List<WeatherAlert> alerts = [];
			string bbox = $"{(lon - 0.2).ToString(System.Globalization.CultureInfo.InvariantCulture)},{(lat - 0.2).ToString(System.Globalization.CultureInfo.InvariantCulture)},{(lon + 0.2).ToString(System.Globalization.CultureInfo.InvariantCulture)},{(lat + 0.2).ToString(System.Globalization.CultureInfo.InvariantCulture)}";
			string gcUrl = $"https://api.weather.gc.ca/collections/weather-alerts/items?lang=en&bbox={bbox}";
			using HttpRequestMessage request = new(HttpMethod.Get, gcUrl);
			request.Headers.TryAddWithoutValidation("User-Agent", "SkyPeekApp/1.0 (contact@skypeek.app)");
			request.Headers.TryAddWithoutValidation("Accept", "application/geo+json");

			using HttpResponseMessage response = await _httpClient.SendAsync(request);

			if (!response.IsSuccessStatusCode) return alerts;

			string json = await response.Content.ReadAsStringAsync();
			using JsonDocument doc = JsonDocument.Parse(json);
			if (doc.RootElement.TryGetProperty("features", out JsonElement features) && features.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement feature in features.EnumerateArray())
				{
					if (feature.TryGetProperty("geometry", out JsonElement geom) && !IsPointInGeoJsonGeometry(lat, lon, geom))
						continue;

					if (feature.TryGetProperty("properties", out JsonElement props))
					{
						string title = props.TryGetProperty("headline", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() ?? "Unknown Warning" :
									   props.TryGetProperty("headline_en", out var he) && he.ValueKind == JsonValueKind.String ? he.GetString() ?? "Unknown Warning" :
									   props.TryGetProperty("alert_name_en", out var an) && an.ValueKind == JsonValueKind.String ? an.GetString() ?? "Unknown Warning" : "Unknown Warning";

						string status = props.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() ?? "" :
										props.TryGetProperty("status_en", out var ste) && ste.ValueKind == JsonValueKind.String ? ste.GetString() ?? "" : "";

						if (status.Contains("ended", StringComparison.OrdinalIgnoreCase) || status.Contains("cancelled", StringComparison.OrdinalIgnoreCase)) continue;

						string description = props.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? "No description provided." :
											 props.TryGetProperty("description_en", out var de) && de.ValueKind == JsonValueKind.String ? de.GetString() ?? "No description provided." :
											 props.TryGetProperty("alert_text_en", out var ate) && ate.ValueKind == JsonValueKind.String ? ate.GetString() ?? "No description provided." : "No description provided.";

						string pubDate = props.TryGetProperty("onset", out var on) && on.ValueKind == JsonValueKind.String ? on.GetString() ?? "" :
										 props.TryGetProperty("publication_datetime", out var pd) && pd.ValueKind == JsonValueKind.String ? pd.GetString() ?? "" : "";

						string alertId = $"GC_{title}_{pubDate}".Replace(" ", "_");

						if ((dismissedAlerts != null && dismissedAlerts.Contains(alertId)) || alerts.Any(a => a.Id == alertId)) continue;

						string impact = props.TryGetProperty("severity", out var imp) && imp.ValueKind == JsonValueKind.String ? imp.GetString() ?? "minor" :
										props.TryGetProperty("impact_en", out var impe) && impe.ValueKind == JsonValueKind.String ? impe.GetString() ?? "minor" : "minor";

						if (GetSeverityLevel(impact) < alertFilterLevel) continue;

						string? payloadColor = props.TryGetProperty("color", out var cProp) && cProp.ValueKind == JsonValueKind.String ? cProp.GetString() : null;
						if (string.IsNullOrWhiteSpace(payloadColor))
							payloadColor = props.TryGetProperty("alert_color", out var acProp) && acProp.ValueKind == JsonValueKind.String ? acProp.GetString() : null;

						string exp = props.TryGetProperty("expiration_datetime", out var expDt) && expDt.ValueKind != JsonValueKind.Null ? expDt.GetString() ?? "" : props.TryGetProperty("event_end_datetime", out var evEndDt) && evEndDt.ValueKind != JsonValueKind.Null ? evEndDt.GetString() ?? "" : "";
						alerts.Add(new WeatherAlert
						{
							Id = alertId,
							Event = CapitalizeFirst(title),
							Sender = "Environment Canada",
							Description = FormatAlertDescription(description) + $"\n\nExpires: {FormatExpirationDate(exp)}",
							SeverityColor = GetColorForSeverity(impact, payloadColor),
							SeverityLevel = GetSeverityLevel(impact)
						});
					}
				}
			}
			return alerts;
		}

		private static string CapitalizeFirst(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return text ?? "";
			return char.ToUpperInvariant(text[0]) + text[1..];
		}

		private static string FormatAlertDescription(string? description)
		{
			if (string.IsNullOrWhiteSpace(description)) return "No description provided.";
			string[] paragraphs = _paragraphSplitRegex.Split(description.Replace("\r\n", "\n"));
			for (int i = 0; i < paragraphs.Length; i++)
			{
				paragraphs[i] = _whitespaceCleanupRegex.Replace(paragraphs[i].Replace("\n", " "), " ").Trim();
			}
			return string.Join("\n\n", paragraphs);
		}

		private static string FormatExpirationDate(string? isoDate)
		{
			if (string.IsNullOrWhiteSpace(isoDate) || isoDate == "NoExp" || isoDate == "0") return "Undetermined";
			if (DateTime.TryParse(isoDate, out DateTime dt)) return dt.ToString("MMM d, h:mm tt");
			return "Undetermined";
		}

		private static string ExtractExpirationFromText(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return "Undetermined";
			var match = _expirationExtractionRegex.Match(text);
			if (match.Success)
			{
				string extracted = match.Groups[1].Value.Trim();
				return extracted.Length > 35 ? "Undetermined" : extracted;
			}
			return "Undetermined";
		}

		private static int GetSeverityLevel(string apiSeverityString)
		{
			if (string.IsNullOrEmpty(apiSeverityString)) return 1;
			return apiSeverityString.ToLower() switch
			{
				"catastrophic" => 5,
				"extreme" => 4,
				"severe" => 3,
				"moderate" => 2,
				"minor" => 1,
				"advisory" => 1,
				_ => 1
			};
		}

		private static Color GetColorForSeverity(string severity, string? payloadColor = null)
		{
			if (!string.IsNullOrWhiteSpace(payloadColor))
			{
				try
				{
					var obj = ColorConverter.ConvertFromString(payloadColor);
					if (obj is Color c) return c;
				}
				catch { }
			}

			return severity.ToLower() switch
			{
				"catastrophic" => Color.FromRgb(255, 0, 255), // Fuchsia / Magenta
				"extreme" => Color.FromRgb(220, 53, 69),
				"severe" => Color.FromRgb(253, 126, 20),
				"moderate" => Color.FromRgb(255, 193, 7),
				"minor" => Color.FromRgb(23, 162, 184),
				"advisory" => Color.FromRgb(23, 162, 184),
				_ => Color.FromRgb(108, 117, 125)
			};
		}

		private static readonly char[] _geoSplitChars = [' ', '\n', '\r'];

		private static bool IsPointInGeoJsonGeometry(double lat, double lon, JsonElement geom)
		{
			if (geom.ValueKind != JsonValueKind.Object || !geom.TryGetProperty("type", out JsonElement typeProp)) return true;
			string type = typeProp.GetString() ?? "";
			if (!geom.TryGetProperty("coordinates", out JsonElement coords) || coords.ValueKind != JsonValueKind.Array) return true;

			if (type.Equals("Polygon", StringComparison.OrdinalIgnoreCase))
			{
				return IsPointInGeoJsonPolygon(lat, lon, coords);
			}
			else if (type.Equals("MultiPolygon", StringComparison.OrdinalIgnoreCase))
			{
				foreach (JsonElement poly in coords.EnumerateArray())
				{
					if (IsPointInGeoJsonPolygon(lat, lon, poly)) return true;
				}
				return false;
			}
			return true;
		}

		private static bool IsPointInGeoJsonPolygon(double lat, double lon, JsonElement polyCoords)
		{
			if (polyCoords.ValueKind != JsonValueKind.Array || polyCoords.GetArrayLength() == 0) return false;
			JsonElement outerRing = polyCoords[0];
			if (outerRing.ValueKind != JsonValueKind.Array) return false;

			List<(double Lat, double Lon)> poly = [];
			foreach (JsonElement pt in outerRing.EnumerateArray())
			{
				if (pt.ValueKind == JsonValueKind.Array && pt.GetArrayLength() >= 2 &&
					pt[0].TryGetDouble(out double pLon) && pt[1].TryGetDouble(out double pLat))
				{
					poly.Add((pLat, pLon));
				}
			}

			if (poly.Count < 3) return false;

			bool isInside = false;
			int j = poly.Count - 1;
			for (int i = 0; i < poly.Count; i++)
			{
				double xi = poly[i].Lon, yi = poly[i].Lat;
				double xj = poly[j].Lon, yj = poly[j].Lat;

				bool intersect = ((yi > lat) != (yj > lat)) && (lon < (xj - xi) * (lat - yi) / (yj - yi) + xi);
				if (intersect) isInside = !isInside;

				j = i;
			}
			return isInside;
		}

		private static bool IsPointInPolygon(double lat, double lon, string? polygonString)
		{
			if (string.IsNullOrWhiteSpace(polygonString)) return false;

			string[] points = polygonString.Split(_geoSplitChars, StringSplitOptions.RemoveEmptyEntries);
			if (points.Length < 3) return false;

			List<(double Lat, double Lon)> poly = [];
			foreach (string point in points)
			{
				string[] coords = point.Split(',');
				if (coords.Length == 2 &&
					double.TryParse(coords[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pLat) &&
					double.TryParse(coords[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pLon))
				{
					poly.Add((pLat, pLon));
				}
			}

			if (poly.Count < 3) return false;

			bool isInside = false;
			int j = poly.Count - 1;
			for (int i = 0; i < poly.Count; i++)
			{
				double xi = poly[i].Lon, yi = poly[i].Lat;
				double xj = poly[j].Lon, yj = poly[j].Lat;

				bool intersect = ((yi > lat) != (yj > lat)) && (lon < (xj - xi) * (lat - yi) / (yj - yi) + xi);
				if (intersect) isInside = !isInside;

				j = i;
			}
			return isInside;
		}

		private static bool IsPointInCircle(double lat, double lon, string? circleString)
		{
			if (string.IsNullOrWhiteSpace(circleString)) return false;

			string[] parts = circleString.Split(_geoSplitChars, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 2)
			{
				string[] coords = parts[0].Split(',');
				if (coords.Length == 2 &&
					double.TryParse(coords[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cLat) &&
					double.TryParse(coords[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cLon) &&
					double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double radiusKm))
				{
					double R = 6371; // Earth radius in km
					double dLat = (lat - cLat) * Math.PI / 180.0;
					double dLon = (lon - cLon) * Math.PI / 180.0;
					double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(cLat * Math.PI / 180.0) * Math.Cos(lat * Math.PI / 180.0) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
					double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
					return (R * c) <= radiusKm;
				}
			}
			return false;
		}

		private static async Task<List<WeatherAlert>> GetNewZealandAlertsAsync(double lat, double lon, HashSet<string> dismissedAlerts, int alertFilterLevel)
		{
			List<WeatherAlert> alerts = [];
			string nzUrl = "https://alerts.metservice.com/cap/rss";

			try
			{
				using HttpRequestMessage request = new(HttpMethod.Get, nzUrl);
				request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
				request.Headers.TryAddWithoutValidation("Accept", "application/rss+xml, application/xml, text/xml");

				using HttpResponseMessage response = await _httpClient.SendAsync(request);
				if (!response.IsSuccessStatusCode) return alerts;

				using System.IO.Stream xmlStream = await response.Content.ReadAsStreamAsync();
				System.Xml.XmlDocument xmlDoc = new();
				xmlDoc.Load(xmlStream);

				System.Xml.XmlNodeList? items = xmlDoc.GetElementsByTagName("item");

				if (items != null)
				{
					foreach (System.Xml.XmlNode item in items)
					{
						string title = item.SelectSingleNode("title")?.InnerText ?? "Unknown Warning";
						string link = item.SelectSingleNode("link")?.InnerText ?? "";
						string pubDate = item.SelectSingleNode("pubDate")?.InnerText ?? "";

						string alertId = $"NZMET_{title}_{pubDate}".Replace(" ", "_");

						if ((dismissedAlerts != null && dismissedAlerts.Contains(alertId)) || alerts.Any(a => a.Id == alertId)) continue;

						string severity = "minor";
						string titleLower = title.ToLower();

						if (titleLower.Contains("red")) severity = "extreme";
						else if (titleLower.Contains("orange")) severity = "severe";
						else if (titleLower.Contains("watch")) severity = "moderate";

						if (GetSeverityLevel(severity) < alertFilterLevel) continue;

						string? payloadColor = item.SelectSingleNode("color")?.InnerText;

						string detailedDesc = item.SelectSingleNode("description")?.InnerText ?? "No description provided.";
						string expiresTime = "Undetermined";
						bool affectsLocation = false;

						if (!string.IsNullOrWhiteSpace(link))
						{
							try
							{
								using HttpRequestMessage pageRequest = new(HttpMethod.Get, link);
								pageRequest.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
								using var pageResponse = await _httpClient.SendAsync(pageRequest);

								if (pageResponse.IsSuccessStatusCode)
								{
									string capXml = await pageResponse.Content.ReadAsStringAsync();
									System.Xml.XmlDocument capDoc = new();
									capDoc.LoadXml(capXml);

									var polygons = capDoc.GetElementsByTagName("polygon", "*");
									for (int i = 0; i < polygons.Count; i++)
									{
										if (IsPointInPolygon(lat, lon, polygons[i]?.InnerText))
										{
											affectsLocation = true;
											break;
										}
									}

									if (!affectsLocation)
									{
										var circles = capDoc.GetElementsByTagName("circle", "*");
										for (int i = 0; i < circles.Count; i++)
										{
											if (IsPointInCircle(lat, lon, circles[i]?.InnerText))
											{
												affectsLocation = true;
												break;
											}
										}
									}

									var descs = capDoc.GetElementsByTagName("description", "*");
									if (descs.Count > 0) detailedDesc = descs[0]?.InnerText ?? detailedDesc;

									var instructions = capDoc.GetElementsByTagName("instruction", "*");
									if (instructions.Count > 0 && !string.IsNullOrWhiteSpace(instructions[0]?.InnerText))
									{
										detailedDesc += "\n\nAction: " + instructions[0]!.InnerText;
									}

									var expiresList = capDoc.GetElementsByTagName("expires", "*");
									if (expiresList.Count > 0) expiresTime = expiresList[0]?.InnerText ?? "Undetermined";
								}
							}
							catch (Exception ex)
							{
								System.Diagnostics.Debug.WriteLine($"MetService live fetch failure: {ex.Message}");
							}
						}

						if (!affectsLocation) continue;

						alerts.Add(new WeatherAlert
						{
							Id = alertId,
							Event = CapitalizeFirst(title),
							Sender = "MetService New Zealand",
							Description = FormatAlertDescription(detailedDesc) + (string.IsNullOrWhiteSpace(link) ? "" : $"\n\nMore info: {link}") + $"\n\nExpires: {FormatExpirationDate(expiresTime)}",
							SeverityColor = GetColorForSeverity(severity, payloadColor),
							SeverityLevel = GetSeverityLevel(severity)
						});
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"MetService Alert Error: {ex.Message}");
			}

			return alerts;
		}
	}
}