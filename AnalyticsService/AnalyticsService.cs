using System;
using Hypertherm.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;

namespace Hypertherm.Analytics
{
    public interface IAnalyticsService
    {
        string SessionId { get; }

        void GenericEvent(EventTelemetry eventTelemetry);
        void GenericTrace(string eventString);
        void SetUser(string name, string nickname, string email, string issuerId, string userId);
    }

    public class ApplicationInsightsAnalytics : IAnalyticsService
    {
        private ILoggingService _logger;
        private TelemetryClient _telemetryClient;
        private string _sessionId;
        public string SessionId => _sessionId;

        public ApplicationInsightsAnalytics(IConfiguration config, ILoggingService logger)
        {
            _logger = logger;

            var instKey = config["InstrumentationKey"];
            var teleConfig = new TelemetryConfiguration(instKey);

            _telemetryClient = new TelemetryClient(teleConfig);
            
            // The TelemetryClient constructor that takes a TelemetryConfiguration
            // is not working, so for now we are setting the instaKey manually.
            _telemetryClient.InstrumentationKey = instKey;

            // configure app insights with base data
            _telemetryClient.Context.Operation.Name = "CC CLI";
            _sessionId = Guid.NewGuid().ToString();
            _telemetryClient.Context.Session.Id = _sessionId;

            GenericTrace("Analytics Initialized.");
        }

        public void SetUser(string name, string nickname, string email, string issuerId, string userId)
        {
            name = !String.IsNullOrEmpty(name) ? name : "Unknown Name ID";
            nickname = !String.IsNullOrEmpty(nickname) ? nickname : "Unknown Nickname ID";
            email = !String.IsNullOrEmpty(email) ? email : "Unknown Email ID";
            issuerId = !String.IsNullOrEmpty(issuerId) ? issuerId : "Unknown Issuer ID";
            userId = !String.IsNullOrEmpty(userId) ? userId : "Unknown User ID";

            _telemetryClient.Context.User.Id = $"{issuerId}::{userId}";
            _telemetryClient.Context.User.AuthenticatedUserId = $"{issuerId}::{userId}";

            _telemetryClient.TrackTrace($"Acquired access token for User: {issuerId}::{userId}");

            // Send an app insights event containing user and issuer ids
            var evt = new EventTelemetry("User Identity");
            evt.Properties.Add("Name", name);
            evt.Properties.Add("Nickname", nickname);
            evt.Properties.Add("Email", email);
            evt.Properties.Add("Issuer", issuerId);
            evt.Properties.Add("UserId", userId);
            GenericEvent(evt);
        }

        public void GenericEvent(EventTelemetry eventTelemetry)
        {
            _telemetryClient.TrackEvent(eventTelemetry);
            _telemetryClient.Flush();
        }

        public void GenericTrace(string eventString)
        {
            _telemetryClient.TrackTrace(eventString);
            _telemetryClient.Flush();
        }
    }
}