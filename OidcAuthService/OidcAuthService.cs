using Hanssens.Net;
using IdentityModel.OidcClient;
using IdentityModel.OidcClient.Results;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Hypertherm.Logging;
using Hypertherm.Analytics;
using static Hypertherm.Logging.LoggingService;

namespace Hypertherm.OidcAuth
{
    public interface IAuthenticationService
    {
        Task<string> Login(string user = "default-user");
        void Logout(string user = "default-user");
    }

    public class OidcAuthService : IAuthenticationService
    {
        private IConfiguration _config;
        private IAnalyticsService _analyticsService;
        private ILoggingService _logger;

        static OidcClient _oidcClient;
        private string _scopes;
        static LocalStorage _localStorage;

        private JwtSecurityToken _identityToken;
        private JwtSecurityToken _accessToken;
        private string _refreshToken;

        public OidcAuthService(IConfiguration config, IAnalyticsService analyticsService, ILoggingService logger)
        {
            _config = config;
            _analyticsService = analyticsService;
            _logger = logger;

            // Setup Open ID Connect Options
            var browser = new SystemBrowser(Int32.Parse(_config["RedirectUriPort"]));
            string redirectUri = string.Format($"http://127.0.0.1:{browser.Port}");

            _scopes = "openid profile email api offline_access read read:custom write:custom read:truehole read:truebevel";

            var oidcOptions = new OidcClientOptions
            {
                Authority = _config["Authority"],
                ClientId = _config["ClientId"],
                RedirectUri = redirectUri,
                Scope = _scopes,
                FilterClaims = false,
                Browser = browser,
                RefreshTokenInnerHttpHandler = new HttpClientHandler(),
                Policy = new Policy { RequireAccessTokenHash = false }
            };
            _oidcClient = new OidcClient(oidcOptions);

            // Setup Local Encrypted Storage Config
            var localStorageConfig = new LocalStorageConfiguration()
            {
                AutoLoad = true,
                AutoSave = true,
                EnableEncryption = true,
                EncryptionSalt = Convert.ToBase64String(Encoding.ASCII.GetBytes(_config["StorageSaltString"]))
            };
            _localStorage = new LocalStorage(localStorageConfig, _config["StoragePassword"]);
        }

        public async Task<string> Login(string user = "default-user")
        {
            dynamic userInfo = new ExpandoObject();

            if(NetworkUtilities.NetworkConnectivity.IsNetworkAvailable())
            {
                var storageKey = "cc-cli:" + user;
                // Check local storage for tokens
                if (_localStorage.Exists(storageKey))
                {
                    _logger.Log($"Local Storage exists.", MessageType.DebugInfo);
                    if (_localStorage.Get<Dictionary<string, string>>(storageKey).ContainsKey("IdentityToken"))
                    {
                        _identityToken = new JwtSecurityToken(_localStorage.Get<Dictionary<string, string>>(storageKey)["IdentityToken"]);
                        _logger.Log($"Identity Token acquired from local storage.", MessageType.DebugInfo);
                    }

                    if (_localStorage.Get<Dictionary<string, string>>(storageKey).ContainsKey("AccessToken"))
                    {
                        _accessToken = new JwtSecurityToken(_localStorage.Get<Dictionary<string, string>>(storageKey)["AccessToken"]);
                        _logger.Log($"Access Token acquired from local storage.", MessageType.DebugInfo);
                    }

                    if (_localStorage.Get<Dictionary<string, string>>(storageKey).ContainsKey("RefreshToken"))
                    {
                        _refreshToken = _localStorage.Get<Dictionary<string, string>>(storageKey)["RefreshToken"];
                        _logger.Log($"Refresh Token acquired from local storage.", MessageType.DebugInfo);
                    }
                }

                // Get new access token if expired or is going to within 5 mins
                if (!String.IsNullOrEmpty(_refreshToken) && !ValidAccessToken(_accessToken))
                {
                    // Try to refresh our token
                    var refreshResult = await RefreshAccessToken(_refreshToken);

                    if (!refreshResult.IsError)
                    {
                        _identityToken = new JwtSecurityToken(refreshResult.IdentityToken);
                        _accessToken = new JwtSecurityToken(refreshResult.AccessToken);
                        _logger.Log($"Identity & Access Tokens acquired from Refresh Token.", MessageType.DebugInfo);
                    }
                    else
                    {
                        _logger.Log($"Refresh Token failed to acquire new tokens.", MessageType.DebugInfo);
                        throw new Exception(refreshResult.Error);
                    }
                }

                // Get new access token if refresh fails
                if (!ValidAccessToken(_accessToken))
                {
                    // Acquire new token
                    var result = await AcquireNewAccessToken();

                    if (!result.IsError)
                    {
                        _identityToken = new JwtSecurityToken(result.IdentityToken);
                        _accessToken = new JwtSecurityToken(result.AccessToken);
                        _refreshToken = result.RefreshToken;
                        _logger.Log($"New Identity, Access, & Refresh Tokens acquired.", MessageType.DebugInfo);
                    }
                    else
                    {
                        _logger.Log($"Identity & Access Tokens acquired from Refresh Token.", MessageType.DebugInfo);
                        throw new Exception(result.Error);
                    }
                }

                if (ValidAccessToken(_accessToken))
                {
                    _localStorage.Store(storageKey,
                        new Dictionary<string, string>{
                            {"IdentityToken", _identityToken.RawData},
                            {"AccessToken", _accessToken.RawData},
                            {"RefreshToken", _refreshToken}
                        });
                    _localStorage.Persist();

                    // App Insights stuff
                    userInfo.name = _identityToken?.Claims.Where(y => y.Type == "name").Select(x => x.Value).FirstOrDefault();
                    userInfo.nickname = _identityToken?.Claims.Where(y => y.Type == "nickname").Select(x => x.Value).FirstOrDefault();
                    userInfo.email = _identityToken?.Claims.Where(y => y.Type == "email").Select(x => x.Value).FirstOrDefault();
                    userInfo.issuer = _identityToken?.Claims.Where(y => y.Type == "iss").Select(x => x.Value).FirstOrDefault();
                    userInfo.subject = _identityToken?.Claims.Where(y => y.Type == "sub").Select(x => x.Value).FirstOrDefault();
                    userInfo.accessToken = String.IsNullOrEmpty(_accessToken?.RawData) ? "" : _accessToken.RawData;

                    _logger.Log($"User Info:", MessageType.DebugInfo);
                    foreach (var infoItem in userInfo as IDictionary<string, object> ?? new Dictionary<string, object>())
                    {
                        _logger.Log($"  -{infoItem.Key.ToUpper()}: {infoItem.Value}", MessageType.DebugInfo);
                    }

                    _analyticsService.SetUser(
                        userInfo.name,
                        userInfo.nickname,
                        userInfo.email,
                        userInfo.issuer,
                        userInfo.subject
                    );
                }
                else
                {
                    // If there is no valid Access Token, send back an empty string.
                    userInfo.accessToken = "";
                }
            }
            else
            {
                _logger.Log("Login failed. No network connection detected.", MessageType.Error);
                // If there is no valid Access Token, send back an empty string.
                userInfo.accessToken = "";
            }

            return userInfo.accessToken;
        }

        private bool ValidAccessToken(JwtSecurityToken token)
        {
            var tokenIsCurrent = false;

            if (token != null)
            {
                // Check if token has expired or will expire in next 5 mins
                if (token.ValidTo > DateTime.Now.AddMinutes(5))
                {
                    tokenIsCurrent = true;
                }
            }

            return tokenIsCurrent;
        }

        private async Task<RefreshTokenResult> RefreshAccessToken(string refreshToken)
        {
            return await _oidcClient.RefreshTokenAsync(refreshToken);
        }

        private async Task<LoginResult> AcquireNewAccessToken()
        {
            var audience = new Dictionary<string, string>
            {
                { "audience", _config["Audience"] }
            };

            var request = new LoginRequest
            {
                FrontChannelExtraParameters = audience
            };

            return await _oidcClient.LoginAsync(request);
        }

        public void Logout(string user = "default-user")
        {
            _oidcClient.LogoutAsync();
            _localStorage.Store("cc-cli:" + user, new Dictionary<string, string> { });

            _analyticsService.GenericTrace($"Logging out user.");
            _logger.Log($"Logged out.", MessageType.DisplayInfo);
        }
    }
}