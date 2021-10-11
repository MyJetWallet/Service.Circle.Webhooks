using System.Runtime.Serialization;
using MyJetWallet.Circle.Models.Payments;
using Service.Circle.Signer.Grpc.Models;

namespace Service.Circle.Webhooks.Domain.Models
{
    [DataContract]
    public class SignalCircleTransfer
    {
        public const string ServiceBusTopicName = "circle-transfer-signal";
        
        [DataMember(Order = 1)] public string BrokerId { get; set; }
        [DataMember(Order = 2)] public string ClientId { get; set; }
        [DataMember(Order = 3)] public string WalletId { get; set; }
        [DataMember(Order = 4)] public CirclePaymentInfo PaymentInfo { get; set; }
    }
}