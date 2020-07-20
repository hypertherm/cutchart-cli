using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Hypertherm.Logging;
using Hypertherm.Analytics;
using Hypertherm.OidcAuth;
using Hypertherm.Update;
using static Hypertherm.Logging.LoggingService;
using System.Diagnostics;
using Hanssens.Net;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace Hypertherm.CcCli
{
    class Program
    {
        private static ConsoleColor _defaultConsoleColor = Console.ForegroundColor;

        private static IUpdateService _updater;
        private static ILoggingService _logger;
        private static IAnalyticsService _analyzer;

        private static LocalStorage _localStorageGlobalSettings;

        // Global Settings Constants
        private const string CHECKFORUPDATES = "check-for-updates";
        private static bool _checkForUpdates = true;

        static void Main(string[] args)
        {
            ArgumentHandler argHandler = new ArgumentHandler(args);
            
            ServiceProvider provider = ApplicationServiceProvider
                .CreateServiceProvider(argHandler.ArgData.Debug);

            _logger = provider.GetRequiredService<ILoggingService>();
            _analyzer = provider.GetRequiredService<IAnalyticsService>();
            _updater = provider.GetRequiredService<IUpdateService>();

            // Since the argumenthandler needs to establish the options for the logger
            // for now the argumenthandler builds up an error string and the prints it out.
            // This could be better by passing the logger into the argumenthandler by ref
            // and then using it internally as you go.
            if (argHandler.ArgData.IsError)
            {
                _logger.Log(argHandler.ArgData.LogString, MessageType.Error);

                return;
            }

            if (argHandler.ArgData.Debug)
            {
                _logger.Log(argHandler.ArgData.ToString(), MessageType.DebugInfo);
            }

           

            // Setup Local Encrypted Storage Config
            var localStorageConfig = new LocalStorageConfiguration()
            {
                AutoLoad = true,
                AutoSave = true,
                EnableEncryption = true,
                EncryptionSalt = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes(
                        provider.GetRequiredService<IConfiguration>()["StorageSaltString"]
                    )
                )
            };
            // Create a Local Storage object for Global persisting settings
            _localStorageGlobalSettings = new LocalStorage(
                localStorageConfig, 
                provider.GetRequiredService<IConfiguration>()["StoragePassword"]
            );

            var storageKeyBase = "cc-cli:global.";
            if (_localStorageGlobalSettings.Exists(storageKeyBase + CHECKFORUPDATES))
            {
                _checkForUpdates = (bool)_localStorageGlobalSettings.Get(storageKeyBase + CHECKFORUPDATES);
            }
            else
            {
                _localStorageGlobalSettings.Store(storageKeyBase + CHECKFORUPDATES, _checkForUpdates);
                _localStorageGlobalSettings.Persist();
            }
            
            // Set up preferred IAnalyticsService, we are using Application Insights.
            // You can build your own if you extend the IAnalyticsService interface.
            _analyzer.GenericTrace("Analytics Initialized.");

            if(argHandler.ArgData.NoCommands)
            {
                if (argHandler.ArgData.Help)
                {
                    _logger.Log(argHandler.ArgData.HelpString, MessageType.DisplayInfo);
                }
                else if (argHandler.ArgData.Version)
                {
                    _logger.Log(Assembly.GetEntryAssembly().GetName().Version.ToString(), MessageType.DisplayInfo);
                }
                else if(!string.IsNullOrEmpty(argHandler.ArgData.Settings))
                {
                    if(argHandler.ArgData.Settings != "show")
                    {
                        var settingToToggle = storageKeyBase + argHandler.ArgData.Settings;
                        if(_localStorageGlobalSettings.Exists(settingToToggle))
                        {
                            _localStorageGlobalSettings.Store(settingToToggle, !(bool)_localStorageGlobalSettings.Get(settingToToggle));
                            _localStorageGlobalSettings.Persist();
                        }
                        else
                        {
                            _logger.Log($"\"{argHandler.ArgData.Settings}\" is not a valid setting.", MessageType.Error);
                        }
                    }

                    // Possibly make a list of settings to iterate over or a standalone class to manage them
                    _logger.Log($"{storageKeyBase}settings" , MessageType.DisplayInfo);
                    _logger.Log($"  {CHECKFORUPDATES}: {_localStorageGlobalSettings.Get(storageKeyBase + CHECKFORUPDATES)}", MessageType.DisplayInfo);
                }
                else if(argHandler.ArgData.Update)
                {
                    List<string> releases = new List<string>();
                    releases = _updater.ListReleases()
                                       .GetAwaiter()
                                       .GetResult();

                    var userResponse = "";
                    if(releases.Count > 0)
                    {
                        _logger.Log("Available versions:", MessageType.DisplayInfo);
                        foreach(var release in releases)
                        {
                            if("v" + _updater.LatestReleasedVersion().ToString() == release)
                            {
                                _logger.Log($"  {release} *latest*", MessageType.DisplayInfo);
                            }
                            else
                            {
                                _logger.Log($"  {release}", MessageType.DisplayInfo);
                            }
                        }
                        _logger.Log("Specify a version or just press 'Enter' to cancel.", MessageType.DisplayInfo);

                        // Change argumenet string to desired response when debugging
                        userResponse = GetUserInput("");

                        if(!string.IsNullOrEmpty(userResponse))
                        {
                            if(userResponse == "latest" && _updater.IsUpdateAvailable())
                            {
                                UpdateIsAvailableConversation();
                            }
                            else
                            {
                                string releaseVersion = "";

                                if(releases.Contains(userResponse))
                                {
                                    releaseVersion = userResponse;
                                }
                                else if(releases.Contains("v" + userResponse))
                                {
                                    releaseVersion = "v" + userResponse;
                                }

                                if(releaseVersion != "")
                                {
                                    _logger.Log($"Updating to release {releaseVersion}.", MessageType.DisplayInfo);
                                    _updater.Update(releaseVersion)
                                        .GetAwaiter()
                                        .GetResult();
                                }
                                else
                                {
                                    _logger.Log("No release version was specified.", MessageType.DisplayInfo);
                                }
                            }
                        }
                        else
                        {
                            _logger.Log("Update process was cancelled.", MessageType.DisplayInfo);
                        }
                    }
                }
                else if (argHandler.ArgData.DumpLog)
                {
                    _logger.DumpLog();
                }
                else if (argHandler.ArgData.ClearLog)
                {
                    _logger.ClearLog();
                }
            }
            else
            {
                // Check for updates if enabled
                if(_checkForUpdates)
                {
                    if(_updater.IsUpdateAvailable())
                    {
                        if(UpdateIsAvailableConversation())
                        {
                            Thread.Sleep(5000);

                            return;
                        }
                        _logger.Log("Disable update notifications? ('y/yes' or 'n/no')", MessageType.DisplayInfo);
                        
                        // check for user response and store it in local storage for future runs
                        var userResponse = !UserYesNoRespose();

                        _localStorageGlobalSettings.Store(storageKeyBase + CHECKFORUPDATES, userResponse);
                        _localStorageGlobalSettings.Persist();
                    }
                }

                var _authenticator = new OidcAuthService(
                    new HttpClientHandler(), // does not support retry policy at present
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<IAnalyticsService>(),
                    provider.GetRequiredService<ILoggingService>()
                );

                if (argHandler.ArgData.Logout)
                {
                    _authenticator.Logout();
                }
                
                if (!String.IsNullOrEmpty(argHandler.ArgData.Command))
                {
                    IApiService ccApiService = provider.GetRequiredService<IApiService>();
                    ccApiService.SetupAuth(_authenticator);
                    if(!ccApiService.IsError)
                    {
                        if (argHandler.ArgData.Command == "products")
                        {
                            IEnumerable<string> productNames = ccApiService.GetProductNames().GetAwaiter().GetResult();

                            if(new List<string>(productNames).Count > 0)
                            {
                                foreach (string productName in productNames)
                                {
                                    _logger.Log(productName, MessageType.DisplayInfo);
                                }
                            }
                            else
                            {
                                _logger.Log("No product information was found.", MessageType.Error);
                            }
                        }
                        else if (argHandler.ArgData.Command == "cutchart")
                        {
                            if((argHandler.ArgData.CcType == "XLSX" && argHandler.ArgData.Product != "")
                                || argHandler.ArgData.CcType == "DB")
                            {
                                if (argHandler.ArgData.OutFile != null)
                                {
                                    ccApiService.GetBaseCutChartData(
                                            argHandler.ArgData.OutFile,
                                            argHandler.ArgData.Product,
                                            argHandler.ArgData.Units, argHandler.ArgData.CcType)
                                            .GetAwaiter()
                                            .GetResult();

                                    ExitStatus(argHandler.ArgData.OutFile);
                                }
                                else
                                {
                                    _logger.Log("An 'outfile' must be specified to save cut chart data.", MessageType.Error);
                                }
                            }
                            else
                            {
                                _logger.Log("A 'product' must be specified to get cut chart data as .XLSX.", MessageType.Error);
                            }
                        }
                        else if(argHandler.ArgData.Command == "customs")
                        {
                            if(argHandler.ArgData.CcType == "XLSX")
                            {
                                if(argHandler.ArgData.Product != null)
                                {
                                    if (argHandler.ArgData.XmlFile != null)
                                    {
                                        if (argHandler.ArgData.OutFile != null)
                                        {
                                            ccApiService.GetXmlTransformedCutChartData(
                                                argHandler.ArgData.OutFile, argHandler.ArgData.XmlFile,
                                                argHandler.ArgData.Product,
                                                argHandler.ArgData.CcType)
                                                .GetAwaiter()
                                                .GetResult();

                                            ExitStatus(argHandler.ArgData.OutFile);
                                        }
                                        else
                                        {
                                            _logger.Log("An 'outfile' must be specified to save cut chart data.", MessageType.Error);
                                        }
                                    }
                                    else
                                    {
                                        _logger.Log("An 'xmlfile' must be specified to customize cut chart data. Refer to the README for help with the XML schema.", MessageType.Error);
                                    }
                                }
                                else
                                {
                                    _logger.Log("A 'product' must be specified to customize cut chart data.", MessageType.Error);
                                }
                            }
                            else
                            {
                                _logger.Log("The .XLSX file type must be specified to customize cut chart data.", MessageType.Error);
                            }
                        }
                    }
                }
                else
                {
                    // No command was found
                    
                    // If the user only meant to log out then that is fine
                    // Logging out is the only option we accept in multple user flows
                    if(!argHandler.ArgData.Logout)
                    {
                        _logger.Log("No 'command' was found in the argument list.", MessageType.DebugInfo);
                        _logger.Log(argHandler.ArgData.HelpString, MessageType.DisplayInfo);
                    }
                }
            }

            // Newline buffer at end of output before new command prompt displays
            _logger.Log("", MessageType.DisplayInfo);
        }

        private static bool UpdateIsAvailableConversation(string testResponse = "n")
        {
            var updated = false;

            _logger.Log($"An update to cc-cli v{_updater.LatestReleasedVersion().ToString()} is available. Continue with update? ('y/yes' or 'n/no')", MessageType.DisplayInfo);

            if(UserYesNoRespose(testResponse))
            {
                _logger.Log("Updating to latest release.", MessageType.DisplayInfo);
                _updater.Update()
                .GetAwaiter()
                .GetResult();

                updated = true;
            }

            return updated;
        }

        // Pass in a value to debug specific user responses.
        private static bool UserYesNoRespose(string testResponse = "n")
        {
            // Change argumenet string to desired response when debugging
            var userResponse = GetUserInput(testResponse);

            while(userResponse != "y"
                && userResponse != "yes"
                && userResponse != "n"
                && userResponse != "no")
            {
                _logger.Log("Please provide a valid response.", MessageType.DisplayInfo);
                userResponse = GetUserInput(testResponse);
            }

            return userResponse == "y" || userResponse == "yes";
        }

        private static string GetUserInput(string debugResponse = "")
        {
            var userResponse = "";

            if(Debugger.IsAttached)
            {
                // Change this to a version to debug the check for specifiv updates code.
                userResponse = debugResponse;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                userResponse = Console.ReadLine().ToLower();
                Console.ForegroundColor = _defaultConsoleColor;
            }

            return userResponse;
        }

        private static void ExitStatus(string outfile)
        {
            if (File.Exists(outfile))
            {
                _logger.Log("Succeeded! :D", MessageType.DisplayInfo);
            }
            else
            {
                _logger.Log("Failed. D:", MessageType.DisplayInfo);
            }
        }
    }
}
