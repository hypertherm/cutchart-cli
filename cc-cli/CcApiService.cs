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
using System.Net.Mime;

namespace Hypertherm.CcCli
{
    public interface IApiService
    {
        bool IsError { get; }
        Task<HttpStatusCode> IsCcApiAvailable();
        Task<List<JObject>> GetProducts();
        Task<List<string>> GetProductNames();
        Task GetAllCutChartData(string ccFilename, string type = "XLSX");
        Task GetBaseCutChartData(
                            string ccFilename, string product,
                            string units = "english", string type = "XLSX");
        Task GetXmlTransformedCutChartData(
            string ccFilename, string xmlFilename,
            string productName,
            string type = "XLSX");

        void SetupAuth(IAuthenticationService authService);
    }
    public class CcApiService: IApiService
    {
        private ILoggingService _logger;
        private HttpClient _httpClient;
        private IAnalyticsService _analyticsService;

        private bool _isError = false;
        public bool IsError => _isError;
        public CcApiService(IAnalyticsService analyticsService,
                    ILoggingService logger, HttpClient client)
        {
            _logger = logger;
            _analyticsService = analyticsService;
            _httpClient = client;
        }

        public void SetupAuth(IAuthenticationService authService)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", authService.Login().GetAwaiter().GetResult());
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
                .Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        }

        private void SetAcceptHeaderXlsxContent()
        {
            _httpClient.DefaultRequestHeaders
                .Accept
                .Clear();

            _httpClient.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        }

        private void SetAcceptHeaderDbContent()
        {
            _httpClient.DefaultRequestHeaders
                .Accept
                .Clear();

            _httpClient.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Octet));
        }

        public async Task<HttpStatusCode> IsCcApiAvailable()
        {
            SetAcceptHeaderJsonContent();
            var url = CcApiUtilities.BuildUrl();
            try
            {
                var response = await _httpClient.GetAsync(url);
                return response.StatusCode;
            }
            catch (Exception e)
            {
                _logger.Log($"Error: {e}", MessageType.DebugInfo);
                _logger.Log($"Failed to connect to API service to access Products.", MessageType.Error);
                return HttpStatusCode.NotFound;
            }
        }

        public async Task<List<JObject>> GetProducts()
        {
            var products = new List<JObject>(){};

            if(NetworkUtilities.NetworkConnectivity.IsNetworkAvailable())
            {
                SetAcceptHeaderJsonContent();
                var url = CcApiUtilities.BuildUrl();
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                string responseBody = await response.Content?.ReadAsStringAsync();
                
                _logger.Log($"ProductResponseContent: {responseBody}", MessageType.DebugInfo);
                    if (response.Content?.Headers?.ContentType?.MediaType == MediaTypeNames.Application.Json)
                    {
                
                        products = JObject.Parse(responseBody)
                            ["products"]
                            .Values<JObject>()
                            .ToList();
                    }


                }

            else
            {
                _logger.Log("Could not access product information. No network connection detected.", MessageType.Error);
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
            if(NetworkUtilities.NetworkConnectivity.IsNetworkAvailable())
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
            else
            {
                _logger.Log("Could not access cutchart data. No network connection detected.", MessageType.Error);
            }
        }

        public async Task GetXmlTransformedCutChartData(
                            string ccFilename, string xmlFilename,
                            string productName,
                            string type = "XLSX")
        {
            if(NetworkUtilities.NetworkConnectivity.IsNetworkAvailable())
            {
                if (ccFilename != "" && (type.ToUpper() == "XLSX" || type.ToUpper() == "DB"))
                {
                    SetAcceptHeaderJsonContent();

                    var url = CcApiUtilities.BuildUrl(new[] { productName, "customs" });
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Content = new StringContent(File.ReadAllText(xmlFilename),
                                    Encoding.UTF8,
                                    "application/xml");

                    string responseBody = "";
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
                        _logger.Log(JObject.Parse(responseBody)["error"].Value<string>(), MessageType.Error);
                    }
                }
            }
            else
            {
                _logger.Log("Could not access custom cutchart data. No network connection detected.", MessageType.Error);
            }
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