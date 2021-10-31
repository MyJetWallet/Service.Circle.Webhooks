using MyJetWallet.Sdk.Service;
using MyYamlParser;

namespace Service.Circle.Webhooks.Settings
{
    public class SettingsModel
    {
        [YamlProperty("CircleWebhooks.SeqServiceUrl")]
        public string SeqServiceUrl { get; set; }

        [YamlProperty("CircleWebhooks.ZipkinUrl")]
        public string ZipkinUrl { get; set; }

        [YamlProperty("CircleWebhooks.ElkLogs")]
        public LogElkSettings ElkLogs { get; set; }

        [YamlProperty("CircleWebhooks.SpotServiceBusHostPort")]
        public string SpotServiceBusHostPort { get; set; }

        [YamlProperty("CircleWebhooks.MyNoSqlReaderHostPort")]
        public string MyNoSqlReaderHostPort { get; set; }

        [YamlProperty("CircleWebhooks.BitgoDepositServiceGrpcUrl")]
        public string BitgoDepositServiceGrpcUrl { get; set; }

        [YamlProperty("CircleWebhooks.CircleSignerGrpcServiceUrl")]
        public string CircleSignerGrpcServiceUrl { get; set; }

        [YamlProperty("CircleWebhooks.WebHooksCheckerIntervalMSec")]
        public long WebHooksCheckerIntervalMSec { get; set; }

        [YamlProperty("CircleWebhooks.WebhooksUrl")]
        public string WebhooksUrl { get; set; }
    }
}
