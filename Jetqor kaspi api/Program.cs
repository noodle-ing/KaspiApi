using Jetqor_kaspi_api;
using Jetqor_kaspi_api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin() // Allow all origins for testing
            .AllowAnyMethod() // Allow GET, POST, OPTIONS, etc.
            .AllowAnyHeader(); // Allow headers like X-Auth-Token
    });
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ReturnProductService>();
builder.Services.AddScoped<KaspiOrderService>();
builder.Services.AddScoped<ProductSyncService>();
builder.Services.AddScoped<OrderSyncService>();
builder.Services.AddScoped<StorageSyncService>();
builder.Services.AddScoped<AcceptanceStatusGiverService>();
builder.Services.AddHostedService<TimedHostedService>();
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions =>
        {
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null
            );
        });
    
    options.EnableSensitiveDataLogging();
});




var app = builder.Build();  

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();
app.UseCors("AllowAll"); // Enable CORS policy
app.MapControllers();
app.Run();

