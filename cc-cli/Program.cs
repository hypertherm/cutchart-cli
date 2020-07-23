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

        private static ILoggingService _logger;

        // Global Settings Constants
        private const string _storageKeyBase = "cc-cli:global.";
        private const string CHECKFORUPDATES = "check-for-updates";
        private static bool _checkForUpdates = true;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += delegate {
                ExitProgram(false);
            };

            ArgumentHandler argHandler = new ArgumentHandler(args);

            ServiceProvider provider = ApplicationServiceProvider
                .CreateServiceProvider(argHandler.ArgData.Debug);

            _logger = provider.GetRequiredService<ILoggingService>();
            IUpdateService updater = provider.GetRequiredService<IUpdateService>();

            if(string.IsNullOrEmpty(provider.GetRequiredService<IConfiguration>()["ClientID"]))
            {
                ExitProgram(false, $"The \"ClientID\" is invalid or missing, terminating application.");
            }
            else if(string.IsNullOrEmpty(provider.GetRequiredService<IConfiguration>()["InstrumentationKey"]))
            {
                _logger.Log($"The \"InstrumentationKey\" is invalid or missing, resulting in an unconfigured Analytics Service.", MessageType.Warning);
            }

            // Since the argumenthandler needs to establish the options for the logger
            // for now the argumenthandler builds up an error string and the prints it out.
            // This could be better by passing the logger into the argumenthandler by ref
            // and then using it internally as you go.
            if (argHandler.ArgData.IsError)
            {
                ExitProgram(false, argHandler.ArgData.LogString);
            }
            else
            {
                _logger.Log(argHandler.ArgData.LogString, MessageType.DebugInfo);
            }

            if (argHandler.ArgData.Debug)
            {
                _logger.Log(argHandler.ArgData.ToString(), MessageType.DebugInfo);
            }

            LocalStorage localStorageGlobalSettings = null;

            if(!string.IsNullOrEmpty(provider.GetRequiredService<IConfiguration>()["StorageSaltString"])
                && !string.IsNullOrEmpty(provider.GetRequiredService<IConfiguration>()["StoragePassword"]))
            {
                localStorageGlobalSettings = SetupEncryptedLocalStorage(
                    provider.GetRequiredService<IConfiguration>()["StorageSaltString"],
                    provider.GetRequiredService<IConfiguration>()["StoragePassword"]
                );
            }
            else
            {
                _logger.Log($"One or more of the encryption secrets are invalid or missing.", MessageType.Warning);
                _logger.Log($"Do you want to continue using an unencrypted local storage?", MessageType.DisplayText);
                if(UserYesNoRespose(""))
                {
                    _logger.Log($"Switching to an unencrypted local storage.", MessageType.Warning);
                    localStorageGlobalSettings = SetupUnencryptedLocalStorage();
                }
                else
                {
                    ExitProgram(false);
                }
            }

            if(argHandler.ArgData.NoCommands)
            {
                if (argHandler.ArgData.Help)
                {
                    _logger.Log(argHandler.ArgData.HelpString, MessageType.DisplayText);
                }
                else if (argHandler.ArgData.Version)
                {
                    _logger.Log(Assembly.GetEntryAssembly().GetName().Version.ToString(3), MessageType.DisplayData);
                }
                else if(!string.IsNullOrEmpty(argHandler.ArgData.Settings))
                {
                    if(argHandler.ArgData.Settings != "show")
                    {
                        var settingToToggle = _storageKeyBase + argHandler.ArgData.Settings;
                        if(localStorageGlobalSettings.Exists(settingToToggle))
                        {
                            localStorageGlobalSettings.Store(settingToToggle, !(bool)localStorageGlobalSettings.Get(settingToToggle));
                            localStorageGlobalSettings.Persist();
                        }
                        else
                        {
                            _logger.Log($"\"{argHandler.ArgData.Settings}\" is not a valid setting.", MessageType.Warning);
                        }
                    }

                    // Possibly make a list of settings to iterate over or a standalone class to manage them
                    _logger.Log($"{_storageKeyBase}settings" , MessageType.DisplayText);
                    _logger.Log($"  {CHECKFORUPDATES}: {localStorageGlobalSettings.Get(_storageKeyBase + CHECKFORUPDATES)}", MessageType.DisplayData);
                }
                else if(argHandler.ArgData.Update)
                {
                    List<string> releases = new List<string>();
                    releases = updater.ListReleases()
                                        .GetAwaiter()
                                        .GetResult();

                    var userResponse = "";
                    var numberOfReleases = releases.Count;
                    if(numberOfReleases > 0)
                    {
                        var currentVersion = Assembly.GetEntryAssembly().GetName().Version.ToString(3);
                        _logger.Log("Available versions:", MessageType.DisplayText);

                        var releaseToDisplay = 0;
                        do
                        {
                            for(int i = 0; i < 5; i++)
                            {
                                if(releaseToDisplay < numberOfReleases)
                                {
                                    string versionFlags = "";

                                    if(releases[releaseToDisplay] == "v" + updater.LatestReleasedVersion().ToString(3))
                                    {
                                        versionFlags += " [latest]";
                                    }

                                    if(releases[releaseToDisplay] == "v" + currentVersion)
                                    {
                                        versionFlags += " [current]";
                                    }
                                    
                                    _logger.Log($"  {releases[releaseToDisplay]}{versionFlags}", MessageType.DisplayData);
                                    
                                    releaseToDisplay++;
                                }
                            }

                            if(releaseToDisplay < numberOfReleases)
                            {
                                _logger.Log("(Type \"more\" to see additional versions.)", MessageType.DisplayText);
                            }
                            _logger.Log("Specify a version or press 'Enter' to cancel update.", MessageType.DisplayText);

                            // Change argumenet string to desired user response when debugging
                            userResponse = GetUserInput("");

                            if(!string.IsNullOrEmpty(userResponse) && userResponse != "more")
                            {
                                if(userResponse == "latest" && updater.IsUpdateAvailable())
                                {
                                    if(UpdateIsAvailableConversation(updater))
                                    {
                                        Thread.Sleep(5000);
                                        ExitProgram(true, "cc-cli.exe is exiting to complete update.");
                                    }
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
                                        updater.Update(releaseVersion)
                                                .GetAwaiter()
                                                .GetResult();
                                    }
                                    else
                                    {
                                        _logger.Log("No release version was specified.", MessageType.Warning);
                                    }
                                }
                            }
                            else if(userResponse != "more")
                            {
                                _logger.Log("Update process was cancelled.", MessageType.DisplayText);
                            }
                        }while(userResponse == "more");
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
                if(_checkForUpdates && updater.IsUpdateAvailable())
                {
                    if(UpdateIsAvailableConversation(updater))
                    {
                        Thread.Sleep(5000);
                        ExitProgram(true, "cc-cli.exe is exiting to complete update.");
                    }
                    _logger.Log("Disable update notifications? ('y/yes' or 'n/no')", MessageType.DisplayText);
                    
                    // check for user response and store it in local storage for future runs
                    localStorageGlobalSettings.Store(_storageKeyBase + CHECKFORUPDATES, !UserYesNoRespose());
                    localStorageGlobalSettings.Persist();
                }

                var _authenticator = new OidcAuthService(
                    new HttpClientHandler(), // does not support retry policy at present
                    provider.GetRequiredService<IConfiguration>(),
                    localStorageGlobalSettings,
                    provider.GetRequiredService<IAnalyticsService>(),
                    _logger
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
                                    _logger.Log(productName, MessageType.DisplayData);
                                }
                            }
                            else
                            {
                                ExitProgram(false,"No product information was found.");
                            }
                        }
                        else if (argHandler.ArgData.Command == "cutchart")
                        {
                            if((argHandler.ArgData.CcType == "XLSX" && argHandler.ArgData.Product != "")
                                || argHandler.ArgData.CcType == "DB")
                            {
                                if (argHandler.ArgData.OutFile != null)
                                {
                                    if(File.Exists(argHandler.ArgData.OutFile))
                                    {
                                        File.Delete(argHandler.ArgData.OutFile);
                                    }

                                    ccApiService.GetBaseCutChartData(
                                            argHandler.ArgData.OutFile,
                                            argHandler.ArgData.Product,
                                            argHandler.ArgData.Units, argHandler.ArgData.CcType)
                                            .GetAwaiter()
                                            .GetResult();

                                    if(File.Exists(argHandler.ArgData.OutFile))
                                    {
                                        ExitProgram(true, "Base cut chart data has successfully dowloaded.");
                                    }
                                    else
                                    {
                                        ExitProgram(false);
                                    }
                                }
                                else
                                {
                                    ExitProgram(false, "An 'outfile' must be specified to save cut chart data.");
                                }
                            }
                            else
                            {
                                ExitProgram(false, "A 'product' must be specified to get cut chart data as .XLSX.");
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
                                            if(File.Exists(argHandler.ArgData.OutFile))
                                            {
                                                File.Delete(argHandler.ArgData.OutFile);
                                            }

                                            ccApiService.GetXmlTransformedCutChartData(
                                                argHandler.ArgData.OutFile, argHandler.ArgData.XmlFile,
                                                argHandler.ArgData.Product,
                                                argHandler.ArgData.CcType)
                                                .GetAwaiter()
                                                .GetResult();
                                            
                                            if(File.Exists(argHandler.ArgData.OutFile))
                                            {
                                                ExitProgram(true, "Custom cut chart data has successfully dowloaded.");
                                            }
                                            else
                                            {
                                                ExitProgram(false);
                                            }
                                        }
                                        else
                                        {
                                            ExitProgram(false, "An 'outfile' must be specified to save cut chart data.");
                                        }
                                    }
                                    else
                                    {
                                        ExitProgram(false, "An 'xmlfile' must be specified to customize cut chart data. Refer to the README for help with the XML schema.");
                                    }
                                }
                                else
                                {
                                    ExitProgram(false, "A 'product' must be specified to customize cut chart data.");
                                }
                            }
                            else
                            {
                                ExitProgram(false, "The .XLSX file type must be specified to customize cut chart data.");
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
                        ExitProgram(false, "No valid 'command' or 'switch/option' was found in the argument list. Use \"-h or --help\" for more information");
                    }
                }
            }

            ExitProgram(true);
        }

        private static LocalStorage SetupUnencryptedLocalStorage()
        {
            LocalStorage localStorageGlobalSettings;

            // Setup Local Unencrypted Storage Config
            var localStorageConfig = new LocalStorageConfiguration()
            {
                
                Filename = "cc-cli-unencrypted-storage.localstorage",
                AutoLoad = true,
                AutoSave = true,
                EnableEncryption = false
            };

            // Create a Local Storage object for Global persisting settings
            localStorageGlobalSettings = new LocalStorage(localStorageConfig);

            if (localStorageGlobalSettings.Exists(_storageKeyBase + CHECKFORUPDATES))
            {
                _checkForUpdates = (bool)localStorageGlobalSettings.Get(_storageKeyBase + CHECKFORUPDATES);
            }
            else
            {
                localStorageGlobalSettings.Store(_storageKeyBase + CHECKFORUPDATES, _checkForUpdates);
                localStorageGlobalSettings.Persist();
            }
            
            return localStorageGlobalSettings;
        }

        private static LocalStorage SetupEncryptedLocalStorage(string saltString, string password)
        {
            LocalStorage localStorageGlobalSettings;

            // Setup Local Encrypted Storage Config
            var localStorageConfig = new LocalStorageConfiguration()
            {
                Filename = "cc-cli-encrypted-storage.localstorage",
                AutoLoad = true,
                AutoSave = true,
                EnableEncryption = true,
                EncryptionSalt = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes(saltString)
                )
            };
            
            // Create a Local Storage object for Global persisting settings
            localStorageGlobalSettings = new LocalStorage(
                localStorageConfig, 
                password
            );
            
            if (localStorageGlobalSettings.Exists(_storageKeyBase + CHECKFORUPDATES))
            {
                _checkForUpdates = (bool)localStorageGlobalSettings.Get(_storageKeyBase + CHECKFORUPDATES);
            }
            else
            {
                localStorageGlobalSettings.Store(_storageKeyBase + CHECKFORUPDATES, _checkForUpdates);
                localStorageGlobalSettings.Persist();
            }
            
            return localStorageGlobalSettings;
        }

        private static bool UpdateIsAvailableConversation(IUpdateService updater ,string testResponse = "n")
        {
            var performUpdate = false;

            _logger.Log($"An update to cc-cli v{updater.LatestReleasedVersion().ToString(3)} is available. Continue with update? ('y/yes' or 'n/no')", MessageType.DisplayText);
            if(UserYesNoRespose(testResponse))
            {
                _logger.Log("Updating to latest release.", MessageType.DisplayText);
                updater.Update()
                        .GetAwaiter()
                        .GetResult();

                performUpdate = true;
            }

            return performUpdate;
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
                _logger.Log("Please provide a valid response.", MessageType.Warning);
                userResponse = GetUserInput(testResponse);
            }

            return userResponse == "y" || userResponse == "yes";
        }

        private static string GetUserInput(string debugResponse = "")
        {
            var userResponse = "";

            Console.ForegroundColor = ConsoleColor.Magenta;
            if(Debugger.IsAttached)
            {
                userResponse = debugResponse.ToLower();
                Console.WriteLine(userResponse);
            }
            else
            {
                userResponse = Console.ReadLine().ToLower();
            }
            Console.ForegroundColor = _defaultConsoleColor;

            return userResponse;
        }

        private static void ExitProgram(bool success, string message = "")
        {
            int exitCode = 0;

            if(!string.IsNullOrEmpty(message)
                && !string.IsNullOrWhiteSpace(message))
            {
                message += "\n";
            }

            if (success)
            {
                exitCode = 0;
                _logger.Log(message, MessageType.DisplayText);
            }
            else
            {
                exitCode = -1;
                _logger.Log(message, MessageType.Error);
            }

            Console.ForegroundColor = _defaultConsoleColor;

            System.Environment.Exit(exitCode);
        }
    }
}
