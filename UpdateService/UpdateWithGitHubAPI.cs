
using Hypertherm.Logging;
using Hypertherm.Analytics;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using static Hypertherm.Logging.LoggingService;
using System.Net.Mime;
using System.Net.Http.Headers;

namespace Hypertherm.Update
{
    public class UpdateWithGitHubAPI : IUpdateService
    {
        private IAnalyticsService _analyticsService;
        private ILoggingService _logger;
        private static HttpClient _httpClient;

        private List<JObject> _releases = new List<JObject>();

        private Version _latestReleasedVersion = new Version("0.0.0.0");
        public Version LatestReleasedVersion(){ return _latestReleasedVersion; }
        private string _latestReleaseUrl = "";

        public UpdateWithGitHubAPI(HttpClient client, IAnalyticsService analyticsService, ILoggingService logger)
        {
            _analyticsService = analyticsService;
            _logger = logger;
            _httpClient = client;

            GetLatestReleasedVersionInfo().GetAwaiter().GetResult();
        }

        public bool IsUpdateAvailable()
        {
            _analyticsService.GenericTrace($"Checking for updates.");

            var currentVersion = Version.Parse(Assembly.GetEntryAssembly().GetName().Version.ToString());

            return _latestReleasedVersion > currentVersion;
        }

        public async Task<List<string>> ListReleases()
        {
            List<string> releases = new List<string>();

            if(NetworkUtilities.NetworkConnectivity.IsNetworkAvailable())
            {
                SetAcceptHeaderJsonContent();
                SetUserAgentHeader();

                var response = await _httpClient.GetAsync("");
                    if (response.Content?.Headers?.ContentType?.MediaType == MediaTypeNames.Application.Json)
                    {
                        string responseBody = await response.Content?.ReadAsStringAsync();
                        var releaseArray = JArray.Parse(responseBody);

                        foreach(var release in releaseArray)
                        {
                        releases.Add(release["tag_name"].Value<string>());
                    }
                }
            }
            else
            {
                _logger.Log($"No network connection detected, unable to access {_httpClient.BaseAddress}.", MessageType.Error);
            }
            return releases;
        }

        public async Task Update(string version = "latest")
        {
            if(NetworkUtilities.NetworkConnectivity.IsNetworkAvailable())
            {
                _analyticsService.GenericTrace($"Performing an update to latest.");
                string downloadUrl = "";

                if(version == "latest")
                {
                    downloadUrl = _latestReleaseUrl;
                }
                else
                {
                    SetAcceptHeaderJsonContent();
                    SetUserAgentHeader();

                    var response = await _httpClient.GetAsync($"/tags/{version}");
                    string responseBody = await response.Content?.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode
                    && response.Content?.Headers?.ContentType?.MediaType == "application/json")
                    {
                        downloadUrl = JObject.Parse(responseBody)["assets"].Values<JObject>().ToList()[0]["browser_download_url"].Value<string>();
                    }
                }

                var currentDir = Directory.GetCurrentDirectory() + "\\";
                var tmpDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToString()}\\cc-cli\\tmp\\";
                Directory.CreateDirectory(tmpDir);
                var ccCliFilename = "cc-cli.exe";

                var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);

                using (Stream contentStream = await (await _httpClient.SendAsync(request)).Content.ReadAsStreamAsync(),
                stream = new FileStream(tmpDir + ccCliFilename, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await contentStream.CopyToAsync(stream);
                }

                if(File.Exists(tmpDir + ccCliFilename))
                {
                    var update = "update.bat";
                    var currentVersion = Version.Parse(Assembly.GetEntryAssembly().GetName().Version.ToString());
                    var oldVersDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToString()}\\cc-cli\\versions\\{currentVersion}\\";
                    Directory.CreateDirectory(oldVersDir);

                    using (Stream updateStream = File.Open(tmpDir + update, FileMode.Create))
                    {
                        updateStream.Write(Encoding.ASCII.GetBytes("echo off \n"));

                        //Update cc-cli.exe taskkill to cc-cli, and loop until Windows completely kill the process before continue
                        updateStream.Write(Encoding.ASCII.GetBytes("taskkill /f /im cc-cli.exe >nul 2>&1 \n"));
                        updateStream.Write(Encoding.ASCII.GetBytes(":LOOP \n"));
                        updateStream.Write(Encoding.ASCII.GetBytes("tasklist | find /i \"cc-cli.exe\" >nul 2>&1 \n")); 
                        updateStream.Write(Encoding.ASCII.GetBytes("IF ERRORLEVEL 1 (GOTO CONTINUE) \n")); 
                        updateStream.Write(Encoding.ASCII.GetBytes("ELSE (Timeout /T 5 /Nobreak \n GOTO LOOP)\n :CONTINUE \n"));

                        updateStream.Write(Encoding.ASCII.GetBytes($"xcopy /I /Q /Y \"{currentDir + ccCliFilename}\" \"{oldVersDir}\" \n"));
                        updateStream.Write(Encoding.ASCII.GetBytes("timeout /T 2 /nobreak >nul 2>&1 \n")); // Add wait or timeout
                        updateStream.Write(Encoding.ASCII.GetBytes($"xcopy /I /Q /Y \"{tmpDir + ccCliFilename}\" \"{currentDir}\" \n"));
                        updateStream.Write(Encoding.ASCII.GetBytes("timeout /T 2 /nobreak >nul 2>&1 \n")); 
                    }

                    ExecuteCommand(tmpDir + update, currentDir);
                }
            }
            else
            {
                _logger.Log($"Update failed. No network connection detected, unable to access {_httpClient.BaseAddress}.", MessageType.Error);
            }
        }

        private async Task GetLatestReleasedVersionInfo()
        {
            if(NetworkUtilities.NetworkConnectivity.IsNetworkAvailable())
            {
                _analyticsService.GenericTrace($"Getting the latest release's version information.");

                SetAcceptHeaderJsonContent();
                SetUserAgentHeader();

                // Put a try catch around this with logger service used to notify on fail
                var response = await _httpClient.GetAsync("/latest");
                string responseBody = await response.Content?.ReadAsStringAsync();

                if (response.IsSuccessStatusCode
                && response.Content?.Headers?.ContentType?.MediaType == "application/json")
                {
                    _latestReleasedVersion =  Version.Parse(JObject.Parse(responseBody)["tag_name"].Value<string>().Substring(1));
                    _latestReleaseUrl = JObject.Parse(responseBody)["assets"].Values<JObject>().ToList()[0]["browser_download_url"].Value<string>();
                }
            }
        }

        private void SetAcceptHeaderJsonContent()
        {
            _httpClient.DefaultRequestHeaders
                .Accept
                .Clear();

            _httpClient.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        }

        private void SetUserAgentHeader()
        {
            _httpClient.DefaultRequestHeaders
                .UserAgent
                .Clear();

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "cutchart-cli");
        }

        static void ExecuteCommand(string command, string workingDir = "./")
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = true;
            processInfo.WorkingDirectory = workingDir;

            var process = Process.Start(processInfo);
        }
    }
}
