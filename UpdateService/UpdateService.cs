
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
                _latestReleaseUrl = JObject.Parse(responseBody)["assets"].Values<JObject>().ToList()[0]["browser_download_url"].Value<string>();
                latestVersion =  Version.Parse(JObject.Parse(responseBody)["tag_name"].Value<string>().Substring(1));
            }

            return latestVersion > currentVersion;
        }

        public async Task Update(string version = "latest")
        {
            _analyticsService.GenericTrace($"Performing an update to latest.");
            if(version == "latest")
            {
                var currentDir = Directory.GetCurrentDirectory() + "\\";
                var tmpDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToString()}\\cc-cli\\tmp\\";
                Directory.CreateDirectory(tmpDir);
                var ccCliFilename = "cc-cli.exe";
                var update = "update.bat";
                
                var request = new HttpRequestMessage(HttpMethod.Get, _latestReleaseUrl);

                using (Stream contentStream = await (await _httpClient.SendAsync(request)).Content.ReadAsStreamAsync(),
                stream = new FileStream(tmpDir + ccCliFilename, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await contentStream.CopyToAsync(stream);
                }
                
                if(File.Exists(tmpDir + ccCliFilename))
                {
                    var currentVersion = Version.Parse(Assembly.GetEntryAssembly().GetName().Version.ToString());
                    var oldVersDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToString()}\\cc-cli\\versions\\{currentVersion}\\";
                    Directory.CreateDirectory(oldVersDir);
                    var oldVersFilename = ccCliFilename + "_" + DateTime.Now.ToString();

                    using (Stream updateStream = File.Open(tmpDir + update, FileMode.Create))
                    {
                        updateStream.Write(Encoding.ASCII.GetBytes("taskkill /f /im cc-cli.exe\n"));
                        updateStream.Write(Encoding.ASCII.GetBytes($"xcopy /S /I /Q /Y /F \"{currentDir + ccCliFilename}\" \"{oldVersDir + oldVersFilename}\""));
                        updateStream.Write(Encoding.ASCII.GetBytes($"xcopy /S /I /Q /Y /F \"{tmpDir + ccCliFilename}\" \"{currentDir}\""));
                    }
                    
                    ExecuteCommand(tmpDir + update, currentDir);
                }
            }
            else
            {
                _logger.Log("Version selection is not supported yet.", MessageType.Warning);
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