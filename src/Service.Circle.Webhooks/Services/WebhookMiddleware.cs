using System;
using System.IO;
using System.Threading.Tasks;
using DotNetCoreDecorators;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyJetWallet.Circle.Models.Payments;
using MyJetWallet.Sdk.Service;
using Newtonsoft.Json;
using Service.Circle.Signer.Grpc;
using Service.Circle.Signer.Grpc.Models;
using Service.Circle.Webhooks.Domain.Models;

// ReSharper disable InconsistentLogPropertyNaming

// ReSharper disable TemplateIsNotCompileTimeConstantProblem


// ReSharper disable UnusedMember.Global

namespace Service.Circle.Webhooks.Services
{
    public class WebhookMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WebhookMiddleware> _logger;
        private readonly IPublisher<SignalCircleTransfer> _transferPublisher;
        private readonly ICirclePaymentsService _circlePaymentsService;

        public const string NotificationsPath = "/circle/webhook/notification";

        /// <summary>
        /// Middleware that handles all unhandled exceptions and logs them as errors.
        /// </summary>
        public WebhookMiddleware(RequestDelegate next, ILogger<WebhookMiddleware> logger,
            ICirclePaymentsService circlePaymentsService, IPublisher<SignalCircleTransfer> transferPublisher)
        {
            _next = next;
            _logger = logger;
            _circlePaymentsService = circlePaymentsService;
            _transferPublisher = transferPublisher;
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

                var dto = JsonConvert.DeserializeObject<NotificationDto>(body);

                path.ToString().AddToActivityAsTag("webhook-path");
                body.AddToActivityAsTag("webhook-body");

                if (dto is { Type: "Notification" })
                {
                    var message = JsonConvert.DeserializeObject<MessageDto>(dto.Message);
                    if (message != null)
                    {
                        if (message.NotificationType == "payment")
                        {
                            var (brokerId, clientId, walletId) = ParseDescription(message.Payment.Description);
                            if (brokerId != null)
                            {
                                var payment = await _circlePaymentsService.GetCirclePaymentInfo(new GetPaymentRequest
                                    { BrokerId = brokerId, PaymentId = message.Payment.Id });
                                if (payment.IsSuccess)
                                {
                                    await _transferPublisher.PublishAsync(new SignalCircleTransfer
                                    {
                                        BrokerId = brokerId,
                                        ClientId = clientId,
                                        WalletId = walletId,
                                        PaymentInfo = payment.Data
                                    });
                                }
                                else
                                {
                                    _logger.LogInformation("Unable to get payment info {id}", message.Payment.Id);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation("{type} message are not supported", message.NotificationType);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Empty message");
                    }
                }

                _logger.LogInformation("Message from Circle: {message}", JsonConvert.SerializeObject(dto));
            }

            context.Response.StatusCode = 200;
        }

        public (string, string, string) ParseDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return (null, null, null);

            var prm = description.Split("|-|");

            if (prm.Length != 3)
                return (null, null, null);

            if (string.IsNullOrEmpty(prm[0]) || string.IsNullOrEmpty(prm[1]) || string.IsNullOrEmpty(prm[2]))
                return (null, null, null);

            return (prm[0], prm[1], prm[2]);
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
        }
    }
}