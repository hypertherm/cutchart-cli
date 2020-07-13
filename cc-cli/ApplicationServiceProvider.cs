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
using Polly.Registry;
using static Hypertherm.Logging.LoggingService;

namespace Hypertherm.CcCli
{
    public class ApplicationServiceProvider
    {
        private static IAsyncPolicy<HttpResponseMessage> UpdateRetryPolicy()
        {
            IAsyncPolicy<HttpResponseMessage> policy = Policy
                .Handle<SocketException>()
                .OrResult<HttpResponseMessage>(msg => 
                    msg.StatusCode != HttpStatusCode.BadRequest // 400
                    || msg.StatusCode != HttpStatusCode.Unauthorized // 401
                    || msg.StatusCode != HttpStatusCode.Forbidden // 403
                ) // Don't retry if it is a bad request or you don't have authentication
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
                    msg.StatusCode != HttpStatusCode.BadRequest // 400
                    || msg.StatusCode != HttpStatusCode.Unauthorized // 401
                    || msg.StatusCode != HttpStatusCode.Forbidden // 403
                ) // Don't retry if it is a bad request or you don't have authentication
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
                            "https://api.github.com/repos/hypertherm/cutchart-cli/releases"
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