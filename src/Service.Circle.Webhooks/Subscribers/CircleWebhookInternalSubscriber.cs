using DotNetCoreDecorators;
using Microsoft.Extensions.Logging;
using MyJetWallet.Circle.Models.Payments;
using MyJetWallet.Circle.Settings.Services;
using MyJetWallet.Domain;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
using Newtonsoft.Json;
using Service.Circle.Signer.Grpc;
using Service.Circle.Signer.Grpc.Models;
using Service.Circle.Webhook.ServiceBus;
using Service.Circle.Webhooks.Domain.Models;
using Service.ClientWallets.Grpc;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Service.Blockchain.Wallets.Grpc.Models.UserWallets.GetUserByAddressRequest;
using static Service.Circle.Webhooks.Services.WebhookMiddleware;

namespace Service.Circle.Webhooks.Subscribers
{
    public class CircleWebhookInternalSubscriber
    {
        private readonly ILogger<CircleWebhookInternalSubscriber> _logger;
        private readonly ICirclePaymentsService _circlePaymentsService;
        private readonly IServiceBusPublisher<SignalCircleTransfer> _transferPublisher;
        private readonly IServiceBusPublisher<SignalCirclePayout> _payoutPublisher;
        private readonly IServiceBusPublisher<SignalCircleCard> _cardPublisher;
        private readonly IServiceBusPublisher<SignalCircleChargeback> _chargebackPublisher;
        private readonly ICircleBlockchainMapper _circleBlockchainMapper;
        private readonly ICircleAssetMapper _circleAssetMapper;
        private readonly Service.Blockchain.Wallets.Grpc.IWalletService _walletService;
        private readonly Wallets.Grpc.ICircleBankAccountsService _circleBankAccountsService;
        private readonly IClientWalletService _clientWalletService;
        private readonly ICircleCardsService _circleCardsService;
        private readonly ICirclePayoutsService _circlePayoutsService;

        public CircleWebhookInternalSubscriber(
            ILogger<CircleWebhookInternalSubscriber> logger,
            ISubscriber<WebhookQueueItem> subscriber,
            ICirclePaymentsService circlePaymentsService,
            IServiceBusPublisher<SignalCircleTransfer> transferPublisher,
            IServiceBusPublisher<SignalCirclePayout> payoutPublisher,
            IServiceBusPublisher<SignalCircleCard> cardPublisher,
            IServiceBusPublisher<SignalCircleChargeback> chargebackPublisher,
            ICircleBlockchainMapper circleBlockchainMapper,
            ICircleAssetMapper circleAssetMapper,
            Service.Blockchain.Wallets.Grpc.IWalletService walletService,
            Wallets.Grpc.ICircleBankAccountsService circleBankAccountsService,
            IClientWalletService clientWalletService,
            ICircleCardsService circleCardsService,
            ICirclePayoutsService circlePayoutsService)
        {
            subscriber.Subscribe(HandleSignal);
            _logger = logger;
            _circlePaymentsService = circlePaymentsService;
            _transferPublisher = transferPublisher;
            this._payoutPublisher = payoutPublisher;
            _cardPublisher = cardPublisher;
            _circleBlockchainMapper = circleBlockchainMapper;
            _circleAssetMapper = circleAssetMapper;
            _walletService = walletService;
            _circleBankAccountsService = circleBankAccountsService;
            _clientWalletService = clientWalletService;
            _circleCardsService = circleCardsService;
            _circlePayoutsService = circlePayoutsService;
            _chargebackPublisher = chargebackPublisher;
        }

        private async ValueTask HandleSignal(WebhookQueueItem webhook)
        {
            using var activity = MyTelemetry.StartActivity("Handle Circle Event WebhookQueueItem");
            var body = webhook.Data;

            _logger.LogInformation("Processing webhook queue item: {@context}", webhook);

            try
            {
                var dto = JsonConvert.DeserializeObject<NotificationDto>(body);
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

                                            var list = await _clientWalletService.GetWalletsByClient(new()
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

                                        if (payment.Data != null)
                                        {
                                            _logger.LogInformation("GetCirclePaymentInfo payment info {paymentInfo}",
                                                Newtonsoft.Json.JsonConvert.SerializeObject(payment.Data));
                                        }

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
                            case { NotificationType: "cards" }:
                                {
                                    //withdrawals
                                    bool isVerified = message.Card.Status == "complete";
                                    var circleCard = await _circleCardsService.GetCircleCard(new GetCardRequest
                                    {
                                        BrokerId = "jetwallet",
                                        CardId = message.Card.Id,
                                    });

                                    if (!circleCard.IsSuccess)
                                        throw new Exception($"Can'get card with id {message.Card.Id}");

                                    if (circleCard.Data == null)
                                        break;
                                    
                                    await _cardPublisher.PublishAsync(new SignalCircleCard
                                    {
                                        CircleCardId = message.Card.Id,
                                        Verified = isVerified,
                                        Status = circleCard.Data.Status,
                                        ErrorCode = circleCard.Data.ErrorCode,
                                        Bin = circleCard.Data.Bin,
                                        Fingerprint = circleCard.Data.Fingerprint,
                                        FundingType = circleCard.Data.FundingType,
                                        IssuerCountry = circleCard.Data.IssuerCountry,
                                        RiskEvaluation = circleCard.Data.RiskEvaluation,
                                        UpdateDate = circleCard.Data.UpdateDate,
                                    });

                                    break;
                                }
                            case { NotificationType: "chargebacks" }:
                                {
                                    var payment = await _circlePaymentsService.GetCirclePaymentInfo(
                                            new GetPaymentRequest
                                            {
                                                BrokerId = DomainConstants.DefaultBroker,
                                                PaymentId = message.Chargeback.PaymentId
                                            });

                                    if (payment.IsSuccess && payment.Data == null)
                                    {
                                        _logger.LogInformation("Chargeback has no payment {message}", dto.Message);
                                        break;
                                    }

                                    var (brokerId, clientId, walletId) = ParseDescription(payment.Data.Description);

                                    await _chargebackPublisher.PublishAsync(new SignalCircleChargeback
                                    {
                                        Chargeback = message.Chargeback,
                                        BrokerId = brokerId,
                                        ClientId = clientId,
                                        WalletId = walletId,
                                    });

                                    break;
                                }
                            case { NotificationType: "payouts" }:
                                {
                                    var payout = await _circlePayoutsService.GetCirclePayoutInfo(
                                            new ()
                                            {
                                                BrokerId = DomainConstants.DefaultBroker,
                                                PayoutId = message.Payout.Id
                                            });

                                    if (!payout.IsSuccess)
                                    {
                                        throw new Exception($"Can' get payout {dto.Message}");
                                    }

                                    if (payout.IsSuccess && payout.Data == null)
                                    {
                                        _logger.LogInformation("Payout has no data {message}", dto.Message);
                                        break;
                                    }

                                    await _payoutPublisher.PublishAsync(new SignalCirclePayout
                                    {
                                        PayoutInfo = message.Payout,
                                    });

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook queue {@context}", webhook);
                ex.FailActivity();
                throw;
            }
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
    }
}
