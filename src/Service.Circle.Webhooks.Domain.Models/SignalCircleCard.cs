using MyJetWallet.Circle.Models;
using MyJetWallet.Circle.Models.Cards;
using System;
using System.Runtime.Serialization;

namespace Service.Circle.Webhooks.Domain.Models
{
    [DataContract]
    public class SignalCircleCard
    {
        public const string ServiceBusTopicName = "circle-card-signal";

        [DataMember(Order = 1)] public string CircleCardId { get; set; }
        [DataMember(Order = 2), Obsolete] public bool Verified { get; set; }
        [DataMember(Order = 3)] public CardStatus Status { get; set; }
        [DataMember(Order = 4)] public CardVerificationError? ErrorCode { get; set; }
        [DataMember(Order = 5)] public string Bin { get; set; }
        [DataMember(Order = 6)] public string Fingerprint { get; set; }
        [DataMember(Order = 7)] public CardFundingType FundingType { get; set; }
        [DataMember(Order = 8)] public string IssuerCountry { get; set; }
        [DataMember(Order = 9)] public RiskEvaluation RiskEvaluation { get; set; }

        [DataMember(Order = 10)] public DateTime UpdateDate { get; set; }
    }
}