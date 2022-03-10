﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        private readonly IServiceBusPublisher<SignalCircleTransfer> _transferPublisher;
        private readonly ICirclePaymentsService _circlePaymentsService;
        private readonly ICircleBlockchainMapper _circleBlockchainMapper;
        private readonly ICircleAssetMapper _circleAssetMapper;
        private readonly IWalletService _walletService;
        private readonly Wallets.Grpc.ICircleBankAccountsService _circleBankAccountsService;
        private readonly IClientWalletService _clientWalletService;
        public const string NotificationsPath = "/circle/webhook/notification";

        /// <summary>
        /// Middleware that handles all unhandled exceptions and logs them as errors.
        /// </summary>
        public WebhookMiddleware(
            RequestDelegate next,
            ILogger<WebhookMiddleware> logger,
            ICirclePaymentsService circlePaymentsService,
            IServiceBusPublisher<SignalCircleTransfer> transferPublisher,
            ICircleBlockchainMapper circleBlockchainMapper,
            ICircleAssetMapper circleAssetMapper,
            IWalletService walletService,
            Wallets.Grpc.ICircleBankAccountsService circleBankAccountsService,
            IClientWalletService clientWalletService)
        {
            _next = next;
            _logger = logger;
            _circlePaymentsService = circlePaymentsService;
            _transferPublisher = transferPublisher;
            _circleBlockchainMapper = circleBlockchainMapper;
            _circleAssetMapper = circleAssetMapper;
            _walletService = walletService;
            _circleBankAccountsService = circleBankAccountsService;
            _clientWalletService = clientWalletService;
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
                        switch (message)
                        {
                            case { NotificationType: "payments" }:
                                {
                                    var (brokerId, clientId, walletId) = ParseDescription(message.Payment.Description);

                                    if (message.Payment.Source.Type == "wire" &&
                                        message.Payment.Status == PaymentStatus.Paid)
                                    {
                                        var payment = await _circlePaymentsService.GetCirclePaymentInfo(
                                            new GetPaymentRequest
                                            { BrokerId = DomainConstants.DefaultBroker, PaymentId = message.Payment.Id });

                                        if (payment.IsSuccess)
                                        {
                                            var bankAccount = await _circleBankAccountsService.GetCircleBankAccountByIdOnly(
                                            new Wallets.Grpc.Models.BankAccounts.GetClientBankAccountByIdRequest
                                            {
                                                BankAccountId = payment.Data.Source.Id,
                                            });

                                            var list = await _clientWalletService.GetWalletsByClient(new ()
                                            {
                                                ClientId = bankAccount.Data.ClientId,
                                                BrokerId = bankAccount.Data.BrokerId,
                                            });

                                            var defaultWallet = list.Wallets.FirstOrDefault(e => e.IsDefault) ?? list.Wallets.FirstOrDefault();

                                            if (defaultWallet == null)
                                            {
                                                _logger.LogError("Cannot found default wallet for Broker/Client: {brokerId}/{clientId}", bankAccount.Data.BrokerId, bankAccount.Data.ClientId);
                                                throw new Exception($"Cannot found default wallet for Broker/Client: {bankAccount.Data.BrokerId}/{bankAccount.Data.ClientId}");
                                            }

                                            await _transferPublisher.PublishAsync(new SignalCircleTransfer
                                            {
                                                BrokerId = bankAccount.Data.BrokerId,
                                                ClientId = bankAccount.Data.ClientId,
                                                WalletId = defaultWallet.WalletId,
                                                PaymentInfo = payment.Data
                                            });
                                        }
                                        else
                                        {
                                            _logger.LogError("Unable to get payment info {id}", message.Payment.Id);
                                        }
                                    }
                                    else if (brokerId != null)
                                    {
                                        var payment = await _circlePaymentsService.GetCirclePaymentInfo(
                                            new GetPaymentRequest
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
                                            _logger.LogError("Unable to get payment info {id}", message.Payment.Id);
                                        }
                                    }

                                    break;
                                }
                            case { NotificationType: "transfers" } when message.Transfer.Source.Type == "blockchain":
                                {
                                    //deposits
                                    var asset = _circleAssetMapper.CircleAssetToAsset("jetwallet",
                                        message.Transfer.Amount.Currency);
                                    if (string.IsNullOrEmpty(asset?.AssetSymbol))
                                    {
                                        _logger.LogError("Unknown circle asset {asset}", message.Transfer.Amount.Currency);
                                        return;
                                    }

                                    var chain = message.Transfer.Source.Type == "wallet"
                                        ? message.Transfer.Destination.Chain
                                        : message
                                            .Transfer.Source.Chain;
                                    var blockchain = _circleBlockchainMapper.CircleBlockchainToBlockchain("jetwallet",
                                        chain);
                                    if (string.IsNullOrEmpty(blockchain?.Blockchain))
                                    {
                                        _logger.LogError("Unknown circle blockchain {blockchain}",
                                            chain);
                                        return;
                                    }

                                    var addressInfo = await _walletService.GetUserByAddressAsync(new Blockchain.Wallets.Grpc.Models.UserWallets.GetUserByAddressRequest
                                    {
                                        Addresses = new AddressAndTag[]
                                        {
                                            new AddressAndTag()
                                            {
                                                Address = message.Transfer.Destination.Address,
                                                Tag = null
                                            }
                                        }
                                    });

                                    if (addressInfo?.Error != null)
                                    {
                                        _logger.LogError("Error on BW side @{context}", message);
                                        throw new Exception("Error on BW side");
                                    }

                                    if (addressInfo == null || addressInfo.Users == null || !addressInfo.Users.Any())
                                    {
                                        _logger.LogError("Unknown circle address {blockchain} : {address}",
                                            chain, message.Transfer.Destination.Address);
                                        return;
                                    }

                                    var user = addressInfo.Users.First();

                                    var payment = await _circlePaymentsService.GetCircleTransferInfo(new GetPaymentRequest
                                    { BrokerId = user.BrokerId, PaymentId = message.Transfer.Id });
                                    if (payment.IsSuccess)
                                    {
                                        await _transferPublisher.PublishAsync(new SignalCircleTransfer
                                        {
                                            BrokerId = user.BrokerId,
                                            ClientId = user.ClientId,
                                            WalletId = user.WalletId,
                                            PaymentInfo = payment.Data
                                        });
                                    }
                                    else
                                    {
                                        _logger.LogError("Unable to get payment info {id}", message.Payment.Id);
                                    }

                                    break;
                                }
                            case { NotificationType: "transfers" } when message.Transfer.Source.Type == "wallet":
                                {
                                    //withdrawals
                                    var payment = await _circlePaymentsService.GetCircleTransferInfo(new GetPaymentRequest
                                    { BrokerId = "jetwallet", PaymentId = message.Transfer.Id });
                                    if (payment.IsSuccess)
                                    {
                                        await _transferPublisher.PublishAsync(new SignalCircleTransfer
                                        {
                                            PaymentInfo = payment.Data
                                        });
                                    }
                                    else
                                    {
                                        _logger.LogError("Unable to get payment info {id}", message.Payment.Id);
                                    }

                                    break;
                                }
                            default:
                                _logger.LogInformation("{type} message are not supported", message.NotificationType);
                                break;
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
            [JsonProperty("transfer")] public PaymentInfo Transfer { get; set; }
        }
    }
}