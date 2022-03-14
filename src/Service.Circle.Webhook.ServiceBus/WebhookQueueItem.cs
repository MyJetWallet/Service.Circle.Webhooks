using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Service.Circle.Webhook.ServiceBus
{
    [DataContract]
    public class WebhookQueueItem
    {
        [DataMember(Order = 1)]
        public string Data { get; set; }
    }
}
