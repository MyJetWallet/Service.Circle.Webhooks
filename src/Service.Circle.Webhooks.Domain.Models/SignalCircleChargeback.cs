using System.Runtime.Serialization;
using MyJetWallet.Circle.Models.ChargeBacks;

namespace Service.Circle.Webhooks.Domain.Models
{
    [DataContract]
    public class SignalCircleChargeback
    {
        public const string ServiceBusTopicName = "circle-chargeback-signal";

        [DataMember(Order = 1)] public string BrokerId { get; set; }
        [DataMember(Order = 2)] public string ClientId { get; set; }
        [DataMember(Order = 3)] public string WalletId { get; set; }
        [DataMember(Order = 4)] public Chargeback Chargeback { get; set; }
    }
}