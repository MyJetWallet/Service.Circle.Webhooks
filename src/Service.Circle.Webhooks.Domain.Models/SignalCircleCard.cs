using System.Runtime.Serialization;

namespace Service.Circle.Webhooks.Domain.Models
{
    [DataContract]
    public class SignalCircleCard
    {
        public const string ServiceBusTopicName = "circle-card-signal";

        [DataMember(Order = 1)] public string CircleCardId { get; set; }
        [DataMember(Order = 2)] public bool Verified { get; set; }
    }
}