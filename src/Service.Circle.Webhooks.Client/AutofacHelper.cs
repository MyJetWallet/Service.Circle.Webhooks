using Autofac;

// ReSharper disable UnusedMember.Global

namespace Service.Circle.Webhooks.Client
{
    public static class AutofacHelper
    {
        public static void RegisterCircleWebhooksClient(this ContainerBuilder builder, string grpcServiceUrl)
        {
            var factory = new CircleWebhooksClientFactory(grpcServiceUrl);
        }
    }
}