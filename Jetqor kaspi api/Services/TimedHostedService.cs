using Jetqor_kaspi_api.Services;
using Microsoft.Extensions.Hosting;

public class TimedHostedService : BackgroundService
{
    private readonly IServiceProvider _services;

    public TimedHostedService(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _services.CreateScope())
            {
                var kaspiOrderService = scope.ServiceProvider.GetRequiredService<KaspiOrderService>();
                await kaspiOrderService.CheckAndSaveOrdersOnceAsync();
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}