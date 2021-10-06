using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetCoreDecorators;
using MyServiceBus.Abstractions;
using MyServiceBus.TcpClient;
using Service.Circle.Webhooks.Domain.Models;
using SimpleTrading.ServiceBus.CommonUtils.Serializers;

namespace Service.Circle.Webhooks.Client
{
    public class SignalCircleTransferSubscriber : ISubscriber<SignalCircleTransfer>
    {
        private readonly List<Func<SignalCircleTransfer, ValueTask>> _list = new List<Func<SignalCircleTransfer, ValueTask>>();

        public SignalCircleTransferSubscriber(
            MyServiceBusTcpClient client,
            string queueName,
            TopicQueueType queryType)
        {
            client.Subscribe(SignalCircleTransfer.ServiceBusTopicName, queueName, queryType, Handler);
        }

        private async ValueTask Handler(IMyServiceBusMessage data)
        {
            var item = Deserializer(data.Data);

            if (!_list.Any())
            {
                throw new Exception("Cannot handle event. No subscribers");
            }

            foreach (var callback in _list)
            {
                await callback.Invoke(item);
            }
        }


        public void Subscribe(Func<SignalCircleTransfer, ValueTask> callback)
        {
            this._list.Add(callback);
        }

        private SignalCircleTransfer Deserializer(ReadOnlyMemory<byte> data) => data.ByteArrayToServiceBusContract<SignalCircleTransfer>();
    }
}