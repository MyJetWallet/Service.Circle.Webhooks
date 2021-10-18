using Autofac;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
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
            
            builder.RegisterCirclePaymentsClient(Program.Settings.CircleSignerGrpcServiceUrl); 
        }
    }
}