using System.Threading.Tasks;
using DotNetCoreDecorators;
using MyServiceBus.TcpClient;
using Service.Circle.Webhooks.Domain.Models;
using SimpleTrading.ServiceBus.CommonUtils.Serializers;

namespace Service.Circle.Webhooks.Services
{
    public class SignalCircleTransferBusPublisher : IPublisher<SignalCircleTransfer>
    {
        private readonly MyServiceBusTcpClient _client;

        public SignalCircleTransferBusPublisher(MyServiceBusTcpClient client)
        {
            _client = client;
            _client.CreateTopicIfNotExists(SignalCircleTransfer.ServiceBusTopicName);
        }

        public async ValueTask PublishAsync(SignalCircleTransfer valueToPublish)
        {
            var bytesToSend = valueToPublish.ServiceBusContractToByteArray();
            await _client.PublishAsync(SignalCircleTransfer.ServiceBusTopicName, bytesToSend, true);
        }
    }
}