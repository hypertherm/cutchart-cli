using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;

namespace Hypertherm.CcCli.Mocks
{
    public abstract class MockHandler : HttpClientHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(SendAsync(request.Method, request.Headers.Accept.ToString() ,request.RequestUri.PathAndQuery));
        }

        public abstract HttpResponseMessage SendAsync(HttpMethod method, string acceptHeaders, string url);
    }

    public class HttpTestUtilities
    {
        public static MockHandler CreateMockClientHandler()
        {
            var handler = A.Fake<MockHandler>(opt => opt.CallsBaseMethods());

            // Mock base "GET" endpoint that returns json
            A.CallTo(() => handler.SendAsync(HttpMethod.Get, MediaTypeNames.Application.Json, CcApiUtilities.BuildUrl(null, null).PathAndQuery))
                .Returns(Success(productsJsonResponse, MediaTypeNames.Application.Json));

            // Mock product(powermax105) "GET" endpoint with english unist query parameter that returns xlxs
            A.CallTo(() => handler.SendAsync(HttpMethod.Get, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", CcApiUtilities.BuildUrl(
                    new[] { "powermax105" },
                    new Dictionary<string, string>() { { "units", "english" } }).PathAndQuery))
                .ReturnsLazily(() => Success("", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

            // Mock base "GET" endpoint that returns database
            A.CallTo(() => handler.SendAsync(HttpMethod.Get, MediaTypeNames.Application.Octet, CcApiUtilities.BuildUrl(
                    new[] { "" },
                    new Dictionary<string, string>() { { "units", "" } }).PathAndQuery))
                .ReturnsLazily(() => Success("", MediaTypeNames.Application.Octet));

            // Mock customs "POST" endpoint with xml that returns json
            A.CallTo(() => handler.SendAsync(HttpMethod.Post, MediaTypeNames.Application.Json, CcApiUtilities.BuildUrl(
                    new[] { "maxpro200", "customs" },
                    null).PathAndQuery))
                .ReturnsLazily(() => Success(customsJsonResponse, MediaTypeNames.Application.Json));

            // Mock customs "GET" endpoint with xml that returns json
            A.CallTo(() => handler.SendAsync(HttpMethod.Get, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", CcApiUtilities.BuildUrl(
                    new[] { "maxpro200", "customs", "11" },
                    null).PathAndQuery))
                .ReturnsLazily(() => Success("", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

            return handler;
        }

        public static HttpResponseMessage Success(string content, string contentType)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content = new StringContent(content);
            response.Content.Headers.ContentType.MediaType = contentType;
            
            return response;
        }

        static string productsJsonResponse = @"{
    ""products"": [
        {
            ""tag"": ""Powermax"",
            ""product"": ""Powermax 105"",
            ""shortname"": ""powermax105"",
            ""links"": [
                {
                    ""rel"": ""data"",
                    ""type"": ""GET"",
                    ""href"": ""/cutchart/powermax105""
                },
                {
                    ""rel"": ""versions"",
                    ""type"": ""GET"",
                    ""href"": ""/cutchart/powermax105/versions""
                },
                {
                    ""rel"": ""customizations"",
                    ""type"": ""GET"",
                    ""href"": ""/cutchart/powermax105/customs""
                }
            ],
            ""date_created"": ""2020-02-28T13:30:11Z"",
            ""version"": 1
        },
        {
            ""tag"": ""MAXPRO"",
            ""product"": ""MAXPRO 200"",
            ""shortname"": ""maxpro200"",
            ""links"": [
                {
                    ""rel"": ""data"",
                    ""type"": ""GET"",
                    ""href"": ""/api/cutchart/maxpro200""
                },
                {
                    ""rel"": ""versions"",
                    ""type"": ""GET"",
                    ""href"": ""/api/cutchart/maxpro200/versions""
                },
                {
                    ""rel"": ""customizations"",
                    ""type"": ""GET"",
                    ""href"": ""/api/cutchart/maxpro200/customs""
                }
            ],
            ""date_created"": ""2020-02-28T13:30:11Z"",
            ""version"": 1
        }
    ]
}";
        static string customsJsonResponse = @"{
    ""id"": 11,
    ""link"": ""/api/cutchart/powermax105/customs/5""
}";

#region Find MIME type from data
        [DllImport("urlmon.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
        static extern int FindMimeFromData(IntPtr pBC,
            [MarshalAs(UnmanagedType.LPWStr)] string pwzUrl,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.I1, SizeParamIndex=3)]
            byte[] pBuffer,
            int cbSize,
            [MarshalAs(UnmanagedType.LPWStr)] string pwzMimeProposed,
            int dwMimeFlags,
            out IntPtr ppwzMimeOut,
            int dwReserved);

        public static string getMimeFromFile(string filename)
        {
            if (!File.Exists(filename))
            {
                throw new FileNotFoundException(filename + " not found");
            }

            byte[] buffer = new byte[256];
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                if (fs.Length >= 256)
                {
                    fs.Read(buffer, 0, 256);
                }
                else
                {
                    fs.Read(buffer, 0, (int)fs.Length);
                }
            }
            try
            {
                IntPtr mimetype;
                FindMimeFromData((IntPtr)0, null, buffer, 256, null, 0, out mimetype, 0);
                string mime = Marshal.PtrToStringUni(mimetype);
                Marshal.FreeCoTaskMem(mimetype);

                return mime;
            }
            catch (Exception)
            {
                return "unknown/unknown";
            }
        }
        #endregion
    }
}