namespace Jetqor_kaspi_api.Services;

public class KaspiOrderWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<KaspiOrderWorker> _logger;

    public KaspiOrderWorker(IServiceProvider services, ILogger<KaspiOrderWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _services.CreateScope())
            {
                var kaspiService = scope.ServiceProvider.GetRequiredService<KaspiOrderService>();
                await kaspiService.FetchAndSaveOrdersAsync();
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}