using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyJetWallet.Circle.Models.ChargeBacks;
using MyJetWallet.Circle.Models.Payments;
using MyJetWallet.Circle.Settings.Services;
using MyJetWallet.Domain;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
using Newtonsoft.Json;
using Service.Bitgo.DepositDetector.Domain.Models;
using Service.Bitgo.DepositDetector.Grpc;
using Service.Blockchain.Wallets.Grpc;
using Service.Circle.Signer.Grpc;
using Service.Circle.Signer.Grpc.Models;
using Service.Circle.Webhook.ServiceBus;
using Service.Circle.Webhooks.Domain.Models;
using Service.ClientWallets.Grpc;
using static Service.Blockchain.Wallets.Grpc.Models.UserWallets.GetUserByAddressRequest;

// ReSharper disable InconsistentLogPropertyNaming
// ReSharper disable TemplateIsNotCompileTimeConstantProblem
// ReSharper disable UnusedMember.Global

namespace Service.Circle.Webhooks.Services
{
    public class WebhookMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WebhookMiddleware> _logger;
        private readonly IServiceBusPublisher<WebhookQueueItem> _webhhookPublisher;
        public const string NotificationsPath = "/circle/webhook/notification";

        /// <summary>
        /// Middleware that handles all unhandled exceptions and logs them as errors.
        /// </summary>
        public WebhookMiddleware(
            RequestDelegate next,
            ILogger<WebhookMiddleware> logger,
            IServiceBusPublisher<WebhookQueueItem> webhhookPublisher)
        {
            _next = next;
            _logger = logger;
            _webhhookPublisher = webhhookPublisher;
        }

        /// <summary>
        /// Invokes the middleware
        /// </summary>
        public async Task Invoke(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments("/circle", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Receive call to {path}, method: {method}", context.Request.Path,
                    context.Request.Method);
            }

            if (!context.Request.Path.StartsWithSegments("/circle/webhook", StringComparison.OrdinalIgnoreCase))
            {
                await _next.Invoke(context);
                return;
            }

            var path = context.Request.Path;
            var method = context.Request.Method;

            var body = "--none--";

            if (method == "POST")
            {
                await using var buffer = new MemoryStream();

                await context.Request.Body.CopyToAsync(buffer);

                buffer.Position = 0L;

                using var reader = new StreamReader(buffer);

                body = await reader.ReadToEndAsync();
            }

            var query = context.Request.QueryString;

            _logger.LogInformation($"'{path}' | {query} | {method}\n{body}");


            if (path.StartsWithSegments(NotificationsPath) && method == "POST")
            {
                using var activity = MyTelemetry.StartActivity("Receive transfer webhook");

                path.ToString().AddToActivityAsTag("webhook-path");
                body.AddToActivityAsTag("webhook-body");

                _logger.LogInformation("Message from Circle: {message}", body);

                await _webhhookPublisher.PublishAsync(new WebhookQueueItem()
                {
                    Data = body
                });
            }

            context.Response.StatusCode = 200;
        }

        public class NotificationDto
        {
            [JsonProperty("Type")] public string Type { get; set; }
            [JsonProperty("MessageId")] public string MessageId { get; set; }
            [JsonProperty("Token")] public string Token { get; set; }
            [JsonProperty("TopicArn")] public string TopicArn { get; set; }
            [JsonProperty("Message")] public string Message { get; set; }
            [JsonProperty("SubscribeURL")] public string SubscribeUrl { get; set; }
            [JsonProperty("UnsubscribeURL")] public string UnsubscribeUrl { get; set; }
            [JsonProperty("Timestamp")] public string Timestamp { get; set; }
            [JsonProperty("SignatureVersion")] public string SignatureVersion { get; set; }
            [JsonProperty("SigningCertURL")] public string SigningCertUrl { get; set; }
        }

        public class MessageDto
        {
            [JsonProperty("clientId")] public string ClientId { get; set; }
            [JsonProperty("notificationType")] public string NotificationType { get; set; }
            [JsonProperty("version")] public int Version { get; set; }
            [JsonProperty("payment")] public PaymentInfo Payment { get; set; }
            [JsonProperty("transfer")] public PaymentInfo Transfer { get; set; }

            [JsonProperty("card")] public CardInfo Card { get; set; }
            [JsonProperty("chargeback")] public Chargeback Chargeback { get; set; }
        }

        [DataContract]
        public class CardInfo
        {
            [JsonProperty("id")]
            [DataMember(Order = 1)]
            public string Id { get; set; }

            [JsonProperty("verification")]
            [DataMember(Order = 2)]
            public VerificationInfo Verification { get; set; }

            [JsonProperty("riskEvaluation")]
            [DataMember(Order = 3)]
            public RiskEvaluationInfo RiskEvaluationInfo { get; set; }

            [JsonProperty("status")]
            [DataMember(Order = 4)]
            public string Status { get; set; }


        }

        [DataContract]
        public class VerificationInfo
        {
            [JsonProperty("cvv")]
            [DataMember(Order = 1)]
            public string Cvv { get; set; }

            [JsonProperty("avs")]
            [DataMember(Order = 2)]
            public string Avs { get; set; }
        }

        [DataContract]
        public class RiskEvaluationInfo
        {
            [JsonProperty("decision")]
            [DataMember(Order = 1)]
            public string Decision { get; set; }

            [JsonProperty("reason")]
            [DataMember(Order = 2)]
            public string Reason { get; set; }
        }
    }
}