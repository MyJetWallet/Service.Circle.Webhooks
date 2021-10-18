using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyJetWallet.Sdk.Service;
using MyJetWallet.Sdk.ServiceBus;
using MyServiceBus.TcpClient;

namespace Service.Circle.Webhooks
{
    public class ApplicationLifetimeManager : ApplicationLifetimeManagerBase
    {
        private readonly ILogger<ApplicationLifetimeManager> _logger;
        private readonly ServiceBusLifeTime _busTcpClient;

        public ApplicationLifetimeManager(
            IHostApplicationLifetime appLifetime,
            ILogger<ApplicationLifetimeManager> logger, 
            ServiceBusLifeTime busTcpClient)
            : base(appLifetime)
        {
            _logger = logger;
            _busTcpClient = busTcpClient;
        }

        protected override void OnStarted()
        {
            _logger.LogInformation("OnStarted has been called");
            _busTcpClient.Start();
            _logger.LogInformation("MyServiceBusTcpClient is started");
        }

        protected override void OnStopping()
        {
            _logger.LogInformation("OnStopping has been called");
            _busTcpClient.Stop();
            _logger.LogInformation("MyServiceBusTcpClient is stop");
        }

        protected override void OnStopped()
        {
            _logger.LogInformation("OnStopped has been called");
        }
    }
}