using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using Hypertherm.Logging;
using Hypertherm.Analytics;
using Hypertherm.OidcAuth;
using static Hypertherm.Logging.LoggingService;

namespace Hypertherm.CcCli
{
    public class CcApiService
    {
        private ILoggingService _logger;
        private static HttpClient _httpClient = new HttpClient();
        private HttpClientHandler _clientHandler;
        private IAnalyticsService _analyticsService;
        private IAuthenticationService _authService;

        private bool _isError = false;
        public bool IsError => _isError;
        public CcApiService(IAnalyticsService analyticsService = null, IAuthenticationService authService = null,
                    ILoggingService logger = null, HttpClientHandler clientHandler = null)
        {
            _logger = logger;
            
            if(clientHandler == null)
            {
                // Production path - create a real clientHandler
                _clientHandler = new HttpClientHandler();
            }
            else
            {
                // Test path - use mocked clientHandler
                _clientHandler = clientHandler;
                _httpClient = new HttpClient(_clientHandler);
            }

            if(analyticsService != null
                || authService != null)
            {
                // Production path - requires real analytics and authenitcation services
                _analyticsService = analyticsService;
                _authService = authService;

                Setup();
            }
        }

        public void Setup()
        {
            var cookieContainer = new CookieContainer();
            _clientHandler.CookieContainer = cookieContainer;

            _httpClient = new HttpClient(_clientHandler);
            var url = CcApiUtilities.BuildUrl();
            cookieContainer.Add(url, new Cookie("CorrellationId", _analyticsService.SessionId));
            
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _authService.Login().GetAwaiter().GetResult());
            }
            catch(Exception e)
            {
                _isError = true;
                _logger.Log(e.ToString(), MessageType.Error);
            }
        }

        ~CcApiService()
        {
            _httpClient.Dispose();
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

        private void SetAcceptHeaderXlsxContent()
        {
            _httpClient.DefaultRequestHeaders
                .Accept
                .Clear();

            _httpClient.DefaultRequestHeaders
                .Accept
                .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        }

        private void SetAcceptHeaderDbContent()
        {
            _httpClient.DefaultRequestHeaders
                .Accept
                .Clear();

            _httpClient.DefaultRequestHeaders
                .Accept
                .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/octet-stream"));
        }

        public async Task<HttpStatusCode> IsCcApiAvailable()
        {
            SetAcceptHeaderJsonContent();
            var url = CcApiUtilities.BuildUrl();
            var response = await _httpClient.GetAsync(url);

            return response.StatusCode;
        }

        public async Task<List<JObject>> GetProducts()
        {
            SetAcceptHeaderJsonContent();
            var url = CcApiUtilities.BuildUrl();
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            string responseBody = await response.Content?.ReadAsStringAsync();
            
            _logger.Log($"ProductResponseContent: {responseBody}", MessageType.DebugInfo);

            var products = new List<JObject>();
            if (response.IsSuccessStatusCode
            && response.Content?.Headers?.ContentType?.MediaType == "application/json")
            {
                products = JObject.Parse(responseBody)["products"].Values<JObject>().ToList();
            }

            return products;
        }

        public async Task<List<string>> GetProductNames()
        {
            var products = await GetProducts();

            var productNames = new List<string>();
            if(products.Count() > 0)
            {
                foreach(var product in products)
                {
                    productNames.Add(product.Property("shortname").Value.ToString());
                }
            }

            return productNames;
        }

        public async Task GetAllCutChartData(string ccFilename, string type = "XLSX")
        {
            await GetBaseCutChartData(ccFilename, "", "", type);
        }

        public async Task GetBaseCutChartData(
                            string ccFilename, string product,
                            string units = "english", string type = "XLSX")
        {
            if (ccFilename != "" && (type.ToUpper() == "XLSX" || type.ToUpper() == "DB"))
            {
                if (type.ToUpper() == "XLSX")
                {
                    SetAcceptHeaderXlsxContent();
                }
                else if (type.ToUpper() == "DB")
                {
                    SetAcceptHeaderDbContent();
                }

                var url = CcApiUtilities.BuildUrl(
                    new[] { product },
                    new Dictionary<string, string>() { { "units", units } });
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                using (Stream contentStream = await (await _httpClient.SendAsync(request)).Content.ReadAsStreamAsync(),
                stream = new FileStream(ccFilename, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await contentStream.CopyToAsync(stream);
                }
            }
        }

        public async Task<string> GetXmlTransformedCutChartData(
                            string ccFilename, string xmlFilename,
                            string productName,
                            string type = "XLSX")
        {
            string responseBody = "";
            string error = "";

            if (ccFilename != "" && (type.ToUpper() == "XLSX" || type.ToUpper() == "DB"))
            {
                SetAcceptHeaderJsonContent();
                var url = CcApiUtilities.BuildUrl(new[] { productName, "customs" });
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(File.ReadAllText(xmlFilename),
                                Encoding.UTF8,
                                "application/xml");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    responseBody = await response.Content.ReadAsStringAsync();

                    if (type.ToUpper() == "XLSX")
                    {
                        SetAcceptHeaderXlsxContent();
                    }
                    else if (type.ToUpper() == "DB")
                    {
                        SetAcceptHeaderDbContent();
                    }
                    url = CcApiUtilities.BuildUrl(new[] { productName, "customs", JObject.Parse(responseBody)["id"].Value<string>() });
                    request = new HttpRequestMessage(HttpMethod.Get, url);

                    using (Stream contentStream = await (await _httpClient.SendAsync(request)).Content.ReadAsStreamAsync(),
                    stream = new FileStream(ccFilename, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await contentStream.CopyToAsync(stream);
                    }
                }
                else
                {
                    responseBody = await response.Content.ReadAsStringAsync();
                    error = JObject.Parse(responseBody)["error"].Value<string>();
                }
            }

            return error;
        }

    }

    public class CcApiUtilities
    {
        public static Uri BuildUrl(string[] path = null, Dictionary<string, string> queryParams = null)
        {
            UriBuilder uriBuilder = new UriBuilder();
            uriBuilder.Scheme = "Https";
            uriBuilder.Host = "api.hypertherm.com";
            
            if (path != null)
            {
                foreach (var item in path.Select((value, i) => new { i, value }))
                {
                    if (item.value == "")
                    {
                        path[item.i] = null;
                    }
                }
            }

            uriBuilder.Path = path != null ? "/cutchart/" + String.Join("/", path) : "/cutchart/";

            var url = new Uri(uriBuilder.ToString());
            if (queryParams != null)
            {
                url = new Uri(QueryHelpers.AddQueryString(uriBuilder.ToString(), queryParams));
            }

            return url;
        }
    }
}