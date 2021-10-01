using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.Service;
using Newtonsoft.Json;
// ReSharper disable InconsistentLogPropertyNaming

// ReSharper disable TemplateIsNotCompileTimeConstantProblem


// ReSharper disable UnusedMember.Global

namespace Service.Circle.Webhooks.Services
{
    public class WebhookMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<WebhookMiddleware> _logger;

        public const string NotificationsPath = "/circle/webhook/notification";

        /// <summary>
        /// Middleware that handles all unhandled exceptions and logs them as errors.
        /// </summary>
        public WebhookMiddleware(RequestDelegate next, ILogger<WebhookMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Invokes the middleware
        /// </summary>
        public async Task Invoke(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
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

                _logger.LogInformation("Message from Circle: {message}", JsonConvert.SerializeObject(dto));
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
    }
}