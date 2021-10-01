using JetBrains.Annotations;
using MyJetWallet.Sdk.Grpc;

namespace Service.Circle.Webhooks.Client
{
    [UsedImplicitly]
    public class CircleWebhooksClientFactory: MyGrpcClientFactory
    {
        public CircleWebhooksClientFactory(string grpcServiceUrl) : base(grpcServiceUrl)
        {
        }
    }
}
