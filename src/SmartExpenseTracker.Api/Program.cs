using Microsoft.EntityFrameworkCore;
using SmartExpenseTracker.Application.Interfaces;
using SmartExpenseTracker.Application.Services;
using SmartExpenseTracker.Infrastructure.CurrencyProviders;
using SmartExpenseTracker.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<ExchangeRateApiSettings>(builder.Configuration.GetSection("ExchangeRateApi"));
builder.Services.AddHttpClient<IExchangeRateProvider, ExchangeRateApiProvider>();

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<ICurrencyConverter, CurrencyConverter>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // The Swagger page: open /swagger in the browser.
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "SmartExpenseTracker v1"));

    // Create or update the database when the app starts.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
