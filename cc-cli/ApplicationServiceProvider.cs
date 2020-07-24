using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Hypertherm.Analytics;
using Hypertherm.Logging;
using Hypertherm.Update;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using static Hypertherm.Logging.LoggingService;

namespace Hypertherm.CcCli
{
    public class ApplicationServiceProvider
    {
        private static IAsyncPolicy<HttpResponseMessage> UpdateRetryPolicy()
        {
            IAsyncPolicy<HttpResponseMessage> policy = Policy
                .Handle<SocketException>()
                .OrTransientHttpError()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(
                        Math.Pow(2, retryAttempt)
                    )
                );

            return policy;
        }

        private static IAsyncPolicy<HttpResponseMessage> ApiPolicy =>
            Policy
                .Handle<SocketException>()
                .OrResult<HttpResponseMessage>(msg => 
                    !( // don't retry if any of the following are
                        msg.StatusCode == HttpStatusCode.OK // 200 if successfully got response
                        || msg.StatusCode == HttpStatusCode.Created // 201 if  the object was created
                        || msg.StatusCode == HttpStatusCode.BadRequest // 400 if the request was bad, retrying won't make a difference
                        || msg.StatusCode == HttpStatusCode.Unauthorized // 401, if you have not appended authorization token as it won't make a difference
                        || msg.StatusCode == HttpStatusCode.Forbidden // 403, if you do not have authorization to the resource as it won't make a difference
                        || msg.StatusCode == HttpStatusCode.UnprocessableEntity // 422 the cc-cli has a bug and is trying to use the wrong template
                        || msg.StatusCode == HttpStatusCode.NotFound // 404, the api has changed or the CLI app is broken and needs to be updated as it won't make a difference
                        || msg.StatusCode == HttpStatusCode.UnsupportedMediaType // 415, The endpoint has changed what it accepts and the ccapi is no longer working 
                    )
                )
                .OrTransientHttpError()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(
                        Math.Pow(2, retryAttempt)
                    )
                );
        public static void AddApiService(
            HttpMessageHandler httpHandler,
            IServiceCollection serviceCollection
        )
        {
            serviceCollection
                .AddHttpClient<IApiService, CcApiService>()
                .ConfigureHttpMessageHandlerBuilder(builder =>
                    {
                        builder.PrimaryHandler = httpHandler;
                    }
                )
                .AddPolicyHandler(ApiPolicy);
        }
        public static ServiceProvider CreateServiceProvider(bool isOutputDebug)
        {
            IConfiguration configService = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile("authconfig.json", true, true)
                .Build();
            ILoggingService logService = new LoggingService(isOutputDebug ?
                MessageType.DebugInfo
                : MessageType.Error
            );
            
            // Set up preferred IAnalyticsService, we are using Application Insights.
            // You can build your own if you extend the IAnalyticsService interface.
            IAnalyticsService analyticsService = new ApplicationInsightsAnalytics(
                configService,
                logService
            );

            //setup our DI
            var serviceCollection = new ServiceCollection()
                .AddSingleton<IConfiguration>(configService)
                .AddSingleton<ILoggingService>(logService)
                .AddSingleton<IAnalyticsService>(analyticsService);

            serviceCollection
                .AddHttpClient<IUpdateService, UpdateWithGitHubAPI>(client =>
                    {
                        client.BaseAddress = new Uri(
                            "https://api.github.com/repos/hypertherm/cutchart-cli/"
                        );
                    }
                )
                .AddPolicyHandler(UpdateRetryPolicy());

            CookieContainer cookieContainer = new CookieContainer();

            cookieContainer.Add(
                CcApiUtilities.BuildUrl(),
                new Cookie(
                    "CorrellationId",
                    analyticsService.SessionId
                )
            );
            HttpClientHandler apiHttpHandler = new HttpClientHandler
            {
                CookieContainer = cookieContainer,
                UseCookies = true
            };
            AddApiService(apiHttpHandler, serviceCollection);

            ServiceProvider provider = serviceCollection.BuildServiceProvider();

            return provider;
        }
    }
}