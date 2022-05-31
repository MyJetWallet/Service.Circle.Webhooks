using Autofac;
using MyJetWallet.Circle.Settings.Ioc;
using MyJetWallet.Sdk.NoSql;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
using Service.Bitgo.DepositDetector.Client;
using Service.Blockchain.Wallets.Client;
using Service.Circle.Signer.Client;
using Service.Circle.Wallets.Client;
using Service.Circle.Webhook.ServiceBus;
using Service.Circle.Webhooks.Domain.Models;
using Service.Circle.Webhooks.Subscribers;
using Service.ClientWallets.Client;

namespace Service.Circle.Webhooks.Modules
{
    public class ServiceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var serviceBusClient = builder.RegisterMyServiceBusTcpClient(
                Program.ReloadedSettings(e => e.SpotServiceBusHostPort),
                Program.LogFactory);

            builder
                .RegisterMyServiceBusPublisher<SignalCircleTransfer>(serviceBusClient,
                    SignalCircleTransfer.ServiceBusTopicName, true);

            builder
                .RegisterMyServiceBusPublisher<SignalCircleCard>(serviceBusClient,
                    SignalCircleCard.ServiceBusTopicName, true);

            builder
               .RegisterMyServiceBusPublisher<WebhookQueueItem>(
                   serviceBusClient,
                   Topics.CircleWebhookInternalTopic, 
                   true);

            builder.RegisterMyServiceBusSubscriberSingle<WebhookQueueItem>(
                serviceBusClient,
                Topics.CircleWebhookInternalTopic,
                "service-circle-webhook", 
                MyServiceBus.Abstractions.TopicQueueType.Permanent);

            var myNoSqlClient = builder.CreateNoSqlClient(Program.Settings.MyNoSqlReaderHostPort, Program.LogFactory);
            
            builder.RegisterCirclePaymentsClient(Program.Settings.CircleSignerGrpcServiceUrl); 
            builder.RegisterBitgoDepositAddressClient(Program.Settings.BitgoDepositServiceGrpcUrl, myNoSqlClient);
            builder.RegisterCircleSettingsReader(myNoSqlClient);
            builder.RegisterBlockchainWalletsClient(Program.Settings.BlockchainWalletsGrpcServiceUrl, myNoSqlClient);
            builder.RegisterCircleWalletsClient(myNoSqlClient, Program.Settings.CircleWalletsGrpcServiceUrl);
            builder.RegisterClientWalletsClientsWithoutCache(Program.Settings.ClientWalletsGrpcServiceUrl);

            builder
                .RegisterType<CircleWebhookInternalSubscriber>()
                .SingleInstance()
                .AutoActivate();
        }
    }
}