using System.Runtime.Serialization;
using MyJetWallet.Circle.Models.Payouts;

namespace Service.Circle.Webhooks.Domain.Models
{
    [DataContract]
    public class SignalCirclePayout
    {
        public const string ServiceBusTopicName = "circle-payout-signal";

        [DataMember(Order = 1)] public PayoutInfo PayoutInfo { get; set; }
    }
}