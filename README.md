# <p align="center">Welcome to SkyPeek v1.0</p>
<br>
<p align="center"><img height="380" alt="SkyPeek Promotional Poster Art" src="https://github.com/user-attachments/assets/d7f48193-8269-4e90-905e-b60c21ed5159" /></p>


<div align="center">
<h4>Take a peek into your weather.</h4></div>

## Introduction

Thanks for checking out SkyPeek! I believe weather is basically a human right and as a species we deserve an easy way to get the weather (which our taxes pay for anyways) without being subjected to clickbait articles, stalkerish telemetry, subscription models, and all the other terrible things that plague modern software. So, I built SkyPeek--a fully featured, compact, efficient, and beautiful weather app that lives right in your PCs system tray by the clock. If you're looking for a one-stop shop for radar, per-city granular alerts, Windows theme integration, 10-day forecast, 24-hour forecast, Daily summary with air quality ratings, plenty of other bells & whistles, all with no registration, signs up, or API keys required anywhere--give SkyPeek a chance. 

<p align="center"><img width="400" alt="AdTrackingFreePromo" src="https://github.com/user-attachments/assets/48858a94-a481-484e-aef6-75306d3d1d3c" /> <img width="400" alt="AdTrackingFreePromo" src="https://github.com/user-attachments/assets/a4a7b67e-6290-4b81-bf8c-0b7183a557c6" /></p>

<div align="center">

## Table of Contents

1\. [System Requirements](#system-requirements)<br>
2\. [Installation & Removal](#installation--removal)<br>
3\. [Quick-Start Guide](#quick-start-guide)<br>
4\. [Navigation & Usage](#navigation--usage)<br>
5\. [Current Tab](#current-tab)<br>
6\. [Hourly Tab](#hourly-tab)<br>
7\. [Daily Tab](#daily-tab)<br>
8\. [Location Management](#location-management)<br>
9\. [Weather Alerts](#weather-alerts)<br>
10\. [Daily Summary](#daily-summary)<br>
11\. [Live Radar](#live-radar)<br>
12\. [Tray Menu](#tray-menu)<br>
13\. [Settings](#settings)<br>
14\. [Third Party Services Info](#third-party-services-info)<br>
15\. [Accessibility Information](#accessibility-information)<br>
16\. [Updates & Support](#updates--support)<br>
17\. [Version Changelog](#version-changelog)<br>
18\. [User License](#user-license)<br>
19\. [Shameless Plugs](#shameless-plugs)

</div>
    
## System Requirements

- Windows 10 Version 2004 (May 2020 Update) OR Windows 11
- 64-bit system architecture
- Processor (CPU): Multi-core processor with at least 1.0 GHz clock speed
- Memory (RAM): at least 4GB
- Graphics (GPU): A DirectX-compatible GPU with at least 256MB of VRAM is highly recommended. SkyPeek is heavily optimized, but it does feature (disable-able) animated vector graphics that may tax lower end machines.
- ~212MB of hard drive space
- Mouse or Keyboard: SkyPeek is WCAG 2.0 compliant and includes a custom keyboard navigation engine that fully supports all features, as well as traditional mouse controls. 
- Internet Connection: An active connection is strictly required to fetch live meteorological data, interactive radar tile layers, and active weather alerts.
- Sound output (like a connected speaker) if you want to hear the audible alerts.

## Installation & Removal

### Installation

To install via Microsoft Store, visit this URL: https://apps.microsoft.com/detail/9PK6CB35BSGD <br>
*Note: Installation instructions do not apply to Microsoft Store installations*

1. Double-click SkyPeek_1.0.0_Setup.exe, or whatever version you're installing after downloading it from the most current version on the Releases page. 
2. Push "Next". Accept the license agreement, and press "Next" again.
3. Verify the default installation path, or select your own from the "Browse" button. Press "Next" to continue.
4. Select whether or not to receive the optional Desktop shortcut.
5. Verify installation procedures, and then click "Install".
6. Once the installer completes the operation, SkyPeek is installed and ready to go. Click "Finish" to launch the program.

### Removal
1. Open your Windows Settings -> Installed Apps
2. Find or search for SkyPeek in the Installed Apps list
3. Click the 3 dots on the SkyPeek app for its options list
4. Click "Uninstall" to remove SkyPeek from your system

## Quick-Start Guide
<p align="center"><img width="500" alt="TrayIconPromo" src="https://github.com/user-attachments/assets/d29fd285-4ad1-42b6-a20b-c2798db338dc" /></p>

1. SkyPeek will load your home location from either Windows Location Services or your network's IP address on first boot. 
2. To open the main weather window, left-click on SkyPeek's tray icon. You can navigate the tab views at the bottom by clicking, scrolling, or using the keyboard. 
3. To manually add a location, left-click the SkyPeek tray icon once and in the Current tab click the location at the top (defaults to Chicago, Illinois). Left-click "Location Manager" in this list. 
4. In the Location Manager, enter the location into the entry fields and click "Add location" to add it to your location list.
5. Left-click the tray icon to bring up your home location's weather info at any time. Right-click the tray icon for additional options & settings.

## Navigation & Usage

- SkyPeek can be fully navigated using keyboard controls or mouse controls.
- To use keyboard navigation, press an arrow key when a SkyPeek window is in focus. Then use the arrow keys to move the selection indicator around and press "Enter" to choose your selection. "Backspace" will take you back.
- To summon the pop-up weather window with a hotkey, you must go to SkyPeek Settings -> Behavior to enable this feature and setup your hotkey. 
- To access the tray icon menu without a mouse, press "Win + B" on your keyboard and then navigate with the arrows to the SkyPeek tray icon. You can then press "Shift + F10" to open the right click menu, or "Enter" to open the weather window. 
- Left-click the tray icon to bring up your home locations' weather info at any time. Right-click the tray icon for additional options & settings. 
- SkyPeek fully supports all mouse navigation features and utilizes the scroll wheel and click-and-drag operations intuitively. For more specific mouse control interactions, please read the additional sections below. 

## Current Tab
<p align="center"><img width="500" alt="CurrentWeatherPromo" src="https://github.com/user-attachments/assets/492459cb-0923-4e0b-869a-f80e0294f16c" /></p>

- The Current tab is the default Home tab for the SkyPeek weather window. It displays the current weather information for your "Home" location.
- To view the current status of another location, click the current location at the top of the Current tab page and select one of your added locations. To add locations, select "Location Manager" at the bottom of the drop-down menu.
- Clicking the Radar button on the top-right corner of the Current tab will bring you to the Radar View (see [Live Radar](#live-radar)).
- When one of your added locations receives an active weather alert or warning, an alert icon will appear to the left of the current status. Click this icon to view your alerts. See [Weather Alerts](#weather-alerts) for more alert details.
- Clicking the current status weather icon above the temperature will bring you to the current location's Daily Summary. See [Daily Summary](#daily-summary) for additional details.
- The current temperature is displayed large and front-and-center with the "Feels like" temperature conveniently floating to its side on the left.
- If you hover your mouse over the current weather status text (i.e., "Overcast") the tooltip pop-up will display the last time the weather information was refreshed.
- The precipitation values for the Current information automatically transitions between forecasted precipitation amount and chance of precipitation. Similarly, the UV index and Air Quality values also automatically transition to keep the weather widget compact. 

## Hourly Tab
<p align="center"><img width="500" alt="CurrentWeatherPromo" src="https://github.com/user-attachments/assets/d08fe723-db9c-4fb0-bfb9-31d9325a6f07" /></p>

- The Hourly tab displays your current location's weather forecast organized in one-hour card blocks over the next 24-hour period. You can click on the scroll bar, use your scroll wheel on your mouse, or your keyboard controls to navigate through the hourly cards. 
- Like the Current Tab, the precipitation values on the hourly cards automatically transitions between the forecasted precipitation amount and the chance of precipitation. Similarly, the UV index and Air Quality values also automatically transition to keep the weather widget compact.
- You may notice the colored graph line plotted behind each hourly card. This is the hourly temperature graph and it is colored to indicate the intensity of the temperature for that hour. Higher temps are red, and cooler temps go through yellow, green, and then blue at the lowest temperatures. The y-axis represents the high and low temperatures for the full 24-hours so quickly scrolling through the hourly cards provides a rapid understanding of the day's temperature fluctuations. 
- The Hourly tab has two ways to present the 24-hour forecast: the graph view, and the agenda view. To swap between these two views, you can click the button on the top-right of the Hourly Tab to toggle between the two. Additionally, you can double-click anywhere on the hourly cards themselves to quickly toggle from agenda to graph and back again. 

## Daily Tab
<p align="center"><img width="500" alt="CurrentWeatherPromo" src="https://github.com/user-attachments/assets/4a07731f-6909-46f1-bc8c-bd5a581aaf59" /></p>

- The Daily tab provides you with a complete 10-day forecast for your current location. To navigate through the daily forecasts click on the scroll bar, use your mouse's scroll wheel, or use keyboard controls to navigate right and left through the cards. 
- Like the Hourly tab, the precipitation values on the hourly cards automatically transitions between the forecasted precipitation amount and the chance of precipitation. Similarly, the UV index and Air Quality values also automatically transition to keep the weather widget compact.
- You may notice the Daily tab graph has two lines compared to the Hourly tab's single line. These two lines are colored to represent the high and low temperatures for the displayed day's forecast card. When you scroll through the cards you can use the color of the lines to get a rapid sense of how the daily temperatures will proceed for the future. 
- The Daily tab also presents its daily forecast in two ways: the graph view, and the agenda view. To swap between these two views, you can click the button on the top-right of the Current tab to toggle between the two. Additionally, you can double-click anywhere on the Daily cards themselves to quickly toggle from agenda to graph and back again. 

## Location Management
<p align="center"><img width="500" alt="CurrentWeatherPromo" src="https://github.com/user-attachments/assets/e9afca7a-a5a8-4653-a632-c8381af7dc2c" /></p>

- SkyPeek supports as many locations as it will take until it crashes (I added 10 at one point and it was fine). Do keep in mind some of the weather service APIs are not unlimited and if you add hundreds of locations and ask for their weather at once, you may get rate limited from the services themselves. 
- To manually add to or edit your existing location list you must first open your weather window and open the location drop down menu at the top from clicking on your Home location. Select "Location Manager" from this list. 
- Enter your Country, State/Province/etc., and city name in the fields provided and select your city name from the suggested locations that pop-up as you type.
- Click "Add location" under those fields, and see your new location get added to your list below.
- To remove a location, click the small red "x" on the location item in the list.
- To reorder your list, click and drag a location to the position you want and release the drag. 
- Alert options are available per-individual location. For more info, see [Weather Alerts](#weather-alerts) below. 

## Weather Alerts
<p align="center"><img width="500" src="https://github.com/user-attachments/assets/cd201213-cec1-4173-aa52-c9fa88bcb2d8" /></p>

- To enable or disable alerts or adjust program settings for how alerts behave and sound, see the Alerts Settings in [Settings](#settings).
- SkyPeek has a robust, customizable alert system that supports 5 countries: USA, Canada, UK, Australia, and New Zealand. 
- To customize alerts on a per-location basis, go to the Location Manager page from your location drop down menu in the Current tab and toggle the bell icon "on" to enable audible alerts for that location. 
- The triangle alert icon next to the bell cycles through the severity alert levels that you'll receive for each location. A toast notification will appear when adjusting the alert severity levels for a location to clarify your changes.
- When enabled in Settings, audible alert tones will be sounded once per location, even if that location receives multiple new alerts. 
- When an alert is received the Current view will display a pulsing triangular alert icon in the top left corner which you can click on to transition to the Active Alerts page. Alternatively, there is a "View Alerts" shortcut in the right click menu on the tray icon for quick access.
- The Active Alerts page displays a feed of every currently active alert for every location you've added in Location Manager. The Alert feed will automatically scroll to the location you're viewing in the Current tab. 
- Alerts are organized from top to down by severity level. When multiple locations have active alerts, the location groups are presented in the same order that they are set in your Location Manager. 
- A "New:" counter is shown to the right of the "Active Alerts" header text when there are alerts you have not seen yet. If you click on this counter text, the alert feed below will automatically scroll to the next unread alert. 
- To mark alerts as "read" you simply need to visually view them once in the Active Alerts page and they will automatically get marked as read. 
- To permanently clear an alert from the feed, click the "Clear alert" that appears below the alert text that you want to clear.
- To permanently clear ALL alerts in one go, scroll to the very bottom of the Active Alerts page and click the "Clear All Alerts" button.
- **TIP:** The alert text for each alert is highlightable, so you can copy/paste any important information and share it with your friends or family easily. 

## Daily Summary

- To access the daily summary, click on the large weather icon above the current temperature display in the Current tab. 
- Here, the entire day's weather is summarized for your selected location in paragraph form with bolded weather values — including a friendly sunscreen reminder, if you'll need it. 
- Beneath the summary paragraph is a table for the day's Air Quality report. Here you can find both primary and secondary air pollutants and their severity ratings for the day for your selected location. 
- Like the Alerts page, the text here supports highlighting and copying so you can easily share your daily summary with friends or family. 

## Live Radar
<p align="center"><img width="500" alt="CurrentWeatherPromo" src="https://github.com/user-attachments/assets/6ac45bd1-ac7b-4576-8b82-4f30de72c436" /></p>

- The Live Radar page displays your currently selected location and its surrounding area's radar map, along with playback controls.
- Additionally, export buttons are provided so you can share your location's radar with friends or family. You can either copy the current radar frame to your clipboard to paste anywhere you want, or save the whole radar loop as an animated GIF. 
- **Note:** Exported radar images exclude your specific location dot on all radar images for anonymity. 

## Tray Menu

- Displays the current temperature of your Home location. Appears colored to indicate an active alert and its severity, provided the setting is enabled in the Settings window (see [Settings](#settings)).
- Right-click the tray icon to open the tray menu. Settings will spawn the Settings window, where you can configure SkyPeek to your liking. An option to "Clear alerts" will become enabled when an alert is active and will quickly clear all active weather alerts. "Exit SkyPeek" shuts down the application.
- Left-clicking the tray icon will spawn the weather window. This is where you view all current and forecasted weather information. 
- To interact with the tray icon using only the keyboard, press "Win + B" and then navigate with the arrows to the SkyPeek tray icon. You can then press "Enter" to open the weather window or "Shift + F10" to open the right click menu.
- Hover your mouse cursor over the tray icon to bring up the tooltip with a brief summary of your current conditions.

## Settings
<p align="center"><img width="500" alt="CurrentWeatherPromo" src="https://github.com/user-attachments/assets/dfa327c4-c840-4871-8f3f-33838e4c11f7" /></p>

The Settings window is organized into 5 tabbed groups that contain settings related to each category. To apply any changes, press the "Save & close" button at the bottom of the window. To reject all changes and go back to the settings you had before changing anything, simply close the window using the "X" button in the navigation bar at the top-right of the window. 

- **Alerts**: You can enable/disable the alert system entirely, or customize your alert sounds that play when an alert comes in. When "Enable home alert colors for system tray" is enabled, the tray icon temperature will match the color of any active alert your home location is experiencing. 
- **Appearance**: SkyPeek has a number of theming options so you can make sure it fits in just right with your Windows vibe. You can select between a light and dark theme or set whether you want SkyPeek to apply your Windows theme accent color to its backgrounds and borders. The setting to "Use animated weather icons" is available here. Disabling this option will replace the animated weather icons with alternative static versions. The "Scale weather window" feature allows you to resize the main weather window to your liking. This ensures it will be your ideal size independently of screen resolution or DPI settings. Adjusting the slider provides a live preview of the window for perfect adjustment. 
- **Behavior**: To enable the SkyPeek weather pop-up at anytime without requiring interaction with the tray icon, enable the checkbox here and configure your keyboard shortcut to summon the weather window. The "Default tab view" setting lets you customize what you see when you first open the weather window. You can choose between the main three tabs or the Radar page. 
- **Units & time**: Customize the units SkyPeek uses to match your preference here. You can convert Fahrenheit to Celsius, mph to kph, inches to centimeters, point the wind arrow towards the wind source, and even switch to a 24-hour clock system. 
- **System**: Core system settings for SkyPeek. Set whether it starts on boot or whether or not it respects your Windows location here. Adjust the refresh interval for your weather information here from the default of 20 minutes. A 15 minute minimum is in place to balance real world data update frequency and to protect your IP from getting timed out from any of the services (especially if you have many locations added). You can export and import all of your SkyPeek locations and settings here by pressing the button. Just choose where to save the .JSON settings file. When you're ready to import your settings, just click the import settings button and load that .JSON file.

## Third Party Services Info

SkyPeek connects to various free weather services to provide all of its features without any account creation or API key requirements. Truly — SkyPeek *would not exist* without these providers. Here is a list of all services used and what they are for:

- **Open-Meteo**: This is where all of the current & forecasted data comes from, as well as handling user location name translations into coordinates for internal use.
  - *Attribution*: Weather data provided by Open-Meteo (https://open-meteo.com) under CC BY 4.0.
- **BigDataCloud API**: Used for reverse geocoding to determine the user's current city/town based on their client IP address.
  - *Attribution*: Reverse geocoding powered by BigDataCloud (https://www.bigdatacloud.com).
- **Nominatim (OpenStreetMap)**: Used to translate geo-coordinates into readable address, city, and state names. Needed to properly parse countries whose alert systems are based on a region rather than town.
  - *Attribution*: Map data © OpenStreetMap contributors. Data is available under the Open Database License (https://www.openstreetmap.org/copyright).
- **NWS Active Alerts API**: Live alert and warning provider for United States-based locations. Provided by the National Weather Service.
  - *(Public domain data; no explicit copyright attribution required).*
- **Environment Canada Alerts API**: Provides active weather alerts and hazards for Canadian locations.
  - *Attribution*: Contains information licensed under the Open Government License – Canada.
- **UK Met Office Alerts Feed**: Retrieves and parses severe weather warnings for locations across the United Kingdom.
  - *Attribution*: Contains public sector information licensed under the Open Government Licence v3.0.
- **MetService CAP Alerts Feed**: Retrieves severe weather alerts, warnings, and watches for New Zealand.
  - *Attribution*: This product incorporates information made freely available by MetService. Despite this, MetService is not associated with, and does not endorse, SkyPeek or how this product uses MetService information.
- **BoM Hazard Feeds**: Fetches and aggregates weather warnings for Australian regions. Provided by the Bureau of Meteorology.
  - *Attribution*: Alerts provided by the Bureau of Meteorology.
- **RainViewer API & Tilecache**: Provides the precipitation data for the Live Radar image tiles and associated metadata.
  - *Attribution*: Weather data provided by RainViewer (https://www.rainviewer.com).
- **OpenStreetMap Tiles**: Source of the base map tiles for the Live Radar map.
  - *Attribution*: Map data © OpenStreetMap contributors. Data is available under the Open Database License (https://www.openstreetmap.org/copyright).

## Accessibility Information

- SkyPeek was created to meet or exceed Web Content Accessibility Guidelines (WCAG) Level AA standards.
- Accessibility features include custom keyboard navigation engine with clear visual focus indicators, annotated elements for screen reader support, a contrast-appropriate UI, and options for reduced motion when navigating SkyPeek interfaces.
- For more information, visit https://www.w3.org/WAI/standards-guidelines/wcag/

## Updates & Support

- Updates: If an update is released, you will receive a notification (if enabled in Windows) in your Windows notifications along with a download link, and a new  option in SkyPeek's right click menu will appear for the update as well. This only applies to SkyPeek users that installed the program via the .exe setup file or the standalone portable version of the program. The Windows App store comes with automatic updates automatically and will not exhibit this update behavior.
- **Offline mode**: if your system goes offline, an "Offline" tag will get added to your tray icon tooltip. Your last downloaded weather models will be displayed until connection is restored. Restoring connection will trigger an update if the time offline exceeds your weather refresh interval. 
- **API Unreachable**: if the weather services go down or your connection gets rate limited and you're not getting refresh updates (are you using any other weather sites or programs that may share the same API?), an "API Unreachable" tag will be added to your tray icon tooltip. The last downloaded weather models will be displayed normally until connection to weather services has been restored. This will trigger an update if the disconnection time exceeds your refresh interval.
- **Stuttering navigation, or slow transitions?** Try disabling window transparency and the animated weather icons in Settings -> Appearance. 
- **In the event that SkyPeek becomes unresponsive**, disappears from the system tray, or any other strange behavior — right click the windows taskbar and click "Task Manager". Then, search for SkyPeek and right-click on it. Choose "End task". Reopen SkyPeek.
- **Contact info**: SkyPeek is a personal passion project and does not offer contact information for formal support. If you want to stalk the developer, you can find them playing games on Twitch or YouTube @crustyoldhuman
- For a genuine issue with the app (like if you've discovered an error log by your settings file) breaking or failing to do it's weather duties properly, please submit an issue here on GitHub. NOTE: I will check here periodically but again, SkyPeek is not my fulltime job, but a passion project.
  - https://github.com/crustyoldhuman/SkyPeek/issues

## Version Changelog

- **1.0.0** - Initial release. We're feature complete, and ready to peek.

## User License

SkyPeek is distributed under the GNU General Public License (GPL). In short, this means you are free to use, modify, and distribute the software, provided that any modified versions or derivative works are also open-source and licensed under the identical GPL terms.

For more information see here: https://www.gnu.org/licenses/gpl-3.0.en.html

## Shameless Plugs

<h3><p align="center">Gaming Streams</h3></p>
<p align="center">I stream games no one cares about to a tiny audience on Twitch & YouTube. If you like yammering people, weird games, and custom stream interactions, check me out:
<p align="center"><img width="125" alt="crustyoldhuman avatar" src="https://github.com/user-attachments/assets/e4de8e77-57ed-4a98-8f70-d2c7246ca553" /></p>

<p align="center">https://www.youtube.com/@crustyoldhuman</p>
<p align="center">https://www.twitch.tv/crustyoldhuman</p>

<h3><p align="center">ProperDim</h3></p>
<p align="center">A screen brightness management and automation utility I designed specifically for PCs running on HDTVs so you have better control over your brightness levels without messing with your TV's terrible settings menu. Works on regular monitors as well. Includes hotkeys, state recovery, and custom minimum brightness settings.

<p align="center"><img width="400" alt="ProperDim Banner" src="https://github.com/user-attachments/assets/bae3ad72-5907-4276-b488-631919e46f79" /></p>
<p align="center">GitHub page: https://github.com/crustyoldhuman/ProperDim</p>
<p align="center">Microsoft Store (automatic updates): https://apps.microsoft.com/detail/9p4lhj799hxm</p>

<h3><p align="center">Rapt Media Player</h3></p>
<p align="center">Rapt is a zero-bloat, minimalist media player designed for dark-room viewing, featuring dynamic ambient edge-glow, a tasteful audio visualizer, custom draggable subtitles, and instant hardware upscaling. It's entirely local and privacy-respecting, and can be downloaded and ran offline, or you can bookmark the live github url below.

<p align="center"><img width="500" src="https://github.com/user-attachments/assets/bb1bef2d-ca66-4ca1-bfa8-d1f6df702090" /></p>
<p align="center">Try it right now in your browser, no installation required: https://crustyoldhuman.github.io/Rapt/RaptVideoPlayer.html</p>
<p align="center">GitHub page: https://github.com/crustyoldhuman/Rapt</p>
<p align="center">Microsoft Store (automatic updates): https://apps.microsoft.com/detail/9P074TF9RTJ2</p>

---
Thanks for checking out SkyPeek, I'm really proud of it. If you like it, please consider sharing it with a friend or on social media.<br>

*Created with love by [crustyoldhuman](https://github.com/crustyoldhuman)*
