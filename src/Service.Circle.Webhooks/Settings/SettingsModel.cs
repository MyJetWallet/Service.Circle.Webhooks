﻿using MyJetWallet.Sdk.Service;
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
    }
}
