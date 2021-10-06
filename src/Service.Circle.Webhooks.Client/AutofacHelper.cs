using Autofac;
using DotNetCoreDecorators;
using MyServiceBus.Abstractions;
using MyServiceBus.TcpClient;
using Service.Circle.Webhooks.Domain.Models;

// ReSharper disable UnusedMember.Global

namespace Service.Circle.Webhooks.Client
{
    public static class AutofacHelper
    {
        public static void RegisterSignalCircleTransferSubscriber(this ContainerBuilder builder,
            MyServiceBusTcpClient client,
            string queueName,
            TopicQueueType queryType)
        {
            var subs = new SignalCircleTransferSubscriber(client, queueName, queryType);

            builder
                .RegisterInstance(subs)
                .As<ISubscriber<SignalCircleTransfer>>()
                .SingleInstance();
        }
    }
}