using FCG.Catalog.Application.Abstractions;
using FCG.Catalog.Application.Services;
using FCG.Catalog.Application.Services.Interfaces;
using FCG.Catalog.Application.Validators;
using FCG.Catalog.Domain.Repositories;
using FCG.Catalog.Infrastructure.DbContexts;
using FCG.Catalog.Infrastructure.Messaging;
using FCG.Catalog.Infrastructure.Messaging.Consumers;
using FCG.Catalog.Infrastructure.Repositories;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CatalogDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DbConnection"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 10, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);
            });
        });

        services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>();

        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUserGameRepository, UserGameRepository>();

        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IUserGameService, UserGameService>();

        services.AddValidatorsFromAssemblyContaining<CreateGameDtoValidator>();

        services.AddMessaging(configuration);

        return services;
    }

    private static void AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>() ?? new RabbitMqSettings();

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.AddConsumer<PaymentProcessedConsumer>()
                .Endpoint(endpoint => endpoint.Name = settings.PaymentProcessedQueue);

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(settings.Host, settings.VirtualHost, host =>
                {
                    host.Username(settings.Username);
                    host.Password(settings.Password);
                });

                cfg.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(5)));
                cfg.ConfigureEndpoints(context);
            });
        });
    }
}
