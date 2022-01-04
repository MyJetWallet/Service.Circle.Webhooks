using Autofac;
using MyJetWallet.Circle.Settings.Ioc;
using MyJetWallet.Sdk.NoSql;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
using Service.Bitgo.DepositDetector.Client;
using Service.Blockchain.Wallets.Client;
using Service.Circle.Signer.Client;
using Service.Circle.Webhooks.Domain.Models;

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
            
            var myNoSqlClient = builder.CreateNoSqlClient(Program.ReloadedSettings(e => e.MyNoSqlReaderHostPort));
            
            builder.RegisterCirclePaymentsClient(Program.Settings.CircleSignerGrpcServiceUrl); 
            builder.RegisterBitgoDepositAddressClient(Program.Settings.BitgoDepositServiceGrpcUrl, myNoSqlClient);
            builder.RegisterCircleSettingsReader(myNoSqlClient);
            builder.RegisterBlockchainWalletsClient(Program.Settings.BlockchainWalletsGrpcServiceUrl, myNoSqlClient);
        }
    }
}