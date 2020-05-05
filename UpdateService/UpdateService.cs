
using Hypertherm.Logging;
using Hypertherm.Analytics;
using static Hypertherm.Logging.LoggingService;
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

namespace Hypertherm.Update
{
    public interface IUpdateService
    {
        Task Update(string version = "latest");
        Task<bool> IsUpdateAvailable();
        Task<List<string>> ListReleases();
    }

    public class UpdateWithGitHubAPI : IUpdateService
    {
        private string gitHubUrl;
        private IAnalyticsService _analyticsService;
        private ILoggingService _logger;
        private static HttpClient _httpClient;

        private List<JObject> _releases = new List<JObject>();
        private string _latestReleaseUrl;

        public UpdateWithGitHubAPI(IAnalyticsService analyticsService, ILoggingService logger, string url = "")
        {
            _analyticsService = analyticsService;
            _logger = logger;
            gitHubUrl = url;
            _httpClient = new HttpClient();
        }

        public async Task<bool> IsUpdateAvailable()
        {
            _analyticsService.GenericTrace($"Checking for updates.");

            var latestVersion = new Version("0.0.0.0");
            var currentVersion = Version.Parse(Assembly.GetEntryAssembly().GetName().Version.ToString());

            SetAcceptHeaderJsonContent();
            SetUserAgentHeader();

            var response = await _httpClient.GetAsync($"https://api.github.com/repos/hypertherm/cutchart-cli/releases/latest");
            string responseBody = await response.Content?.ReadAsStringAsync();

            if (response.IsSuccessStatusCode
            && response.Content?.Headers?.ContentType?.MediaType == "application/json")
            {
                latestVersion =  Version.Parse(JObject.Parse(responseBody)["tag_name"].Value<string>().Substring(1));
                _latestReleaseUrl = JObject.Parse(responseBody)["assets"].Values<JObject>().ToList()[0]["browser_download_url"].Value<string>();
            }

            return latestVersion > currentVersion;
        }

        public async Task<List<string>> ListReleases()
        {
            List<string> releases = new List<string>();

            SetAcceptHeaderJsonContent();
            SetUserAgentHeader();

            var response = await _httpClient.GetAsync($"https://api.github.com/repos/hypertherm/cutchart-cli/releases");
            string responseBody = await response.Content?.ReadAsStringAsync();

            if (response.IsSuccessStatusCode
            && response.Content?.Headers?.ContentType?.MediaType == "application/json")
            {
                var releaseArray = JArray.Parse(responseBody);

                foreach(var release in releaseArray)
                {
                    releases.Add(release["tag_name"].Value<string>());
                }
            }

            return releases;
        }

        public async Task Update(string version = "latest")
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

                var response = await _httpClient.GetAsync($"https://api.github.com/repos/hypertherm/cutchart-cli/releases/tags/{version}");
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
                    updateStream.Write(Encoding.ASCII.GetBytes("taskkill /f /im cc-cli.exe \n"));
                    updateStream.Write(Encoding.ASCII.GetBytes("")); // Add wait or timeout
                    updateStream.Write(Encoding.ASCII.GetBytes($"xcopy /I /Q /Y \"{currentDir + ccCliFilename}\" \"{oldVersDir}\" \n"));
                    updateStream.Write(Encoding.ASCII.GetBytes("")); // Add wait or timeout
                    updateStream.Write(Encoding.ASCII.GetBytes($"xcopy /I /Q /Y \"{tmpDir + ccCliFilename}\" \"{currentDir}\" \n"));
                }
                
                ExecuteCommand(tmpDir + update, currentDir);
            }
        }

        private void SetAcceptHeaderJsonContent()
        {
            _httpClient.DefaultRequestHeaders
                .Accept
                .Clear();

            _httpClient.DefaultRequestHeaders
                .Accept
                .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
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