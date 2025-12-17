using System.Globalization;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using BH.DIS.Core.Logging;
using BH.DIS.SDK;
using BH.DIS.SDK.Logging;
using Delegateas.DeveloperExperience.Core;
using Delegateas.DeveloperExperience.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ESB.DeveloperExperience;

public static class ExtensionMethods
{
    /// <summary>
    /// Add DIS logging and health checks to the host application builder.
    /// </summary>
    /// <param name="builder">Host application builder.</param>
    /// <param name="configuration">Configuration manager.</param>
    /// <returns>Modified host application builder.</returns>
    public static IHostApplicationBuilder AddServiceDefaults(
        this IHostApplicationBuilder builder,
        ConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddLogging(configuration);

        builder.Services.AddHealthChecks()
            .AddCheck<ServiceBusHealthCheck>("liveness")
            .AddCheck("startup", () => HealthCheckResult.Healthy());

        return builder;
    }

    private static IHostApplicationBuilder AddLogging(
        this IHostApplicationBuilder builder,
        ConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        var eipLogger = new LoggerConfiguration()
            .WriteTo.ApplicationInsights(
                instrumentationKey: configuration.GetValue<string>(
                    "ApplicationConfiguration:ApplicationInsightsInstrumentationKey"),
                telemetryConverter: TelemetryConverter.Traces)
            .MinimumLevel.Information()
            .WriteTo.Console(formatProvider: new CultureInfo("da-dk"))
            .Enrich.FromLogContext()
            .CreateLogger();

        services.AddTransient(_ =>
        {
            ILoggerProvider loggerProvider = new LoggerProvider(eipLogger);
            return loggerProvider;
        });

        services.AddSingleton(_ => Log.Logger);

        return builder;
    }

    /// <summary>
    /// Required for either publishing or subscribing to the ESB.
    /// This also handles added the administration client for local development environments.
    /// </summary>
    /// <param name="services">DI Service Collection.</param>
    /// <param name="environmentConfiguration">Environment Configuration.</param>
    /// <returns>Modified DI Service Collection.</returns>
    public static IServiceCollection AddEsbServiceBusClient(
        this IServiceCollection services,
        EnvironmentConfiguration environmentConfiguration)
    {
        services.AddAzureClients(b =>
        {
            ArgumentNullException.ThrowIfNull(b);

            b.AddServiceBusClientWithNamespace(
                $"sb-esb-{environmentConfiguration.InfrastructureEnvironment}.servicebus.windows.net");

            if (environmentConfiguration.RuntimeEnvironment == RuntimeEnvironment.LocalDeveloperMachine)
            {
                b.AddServiceBusAdministrationClientWithNamespace(
                    $"sb-esb-{environmentConfiguration.InfrastructureEnvironment}.servicebus.windows.net");
            }

            b.UseCredential(environmentConfiguration.TokenCredential);
        });

        return services;
    }

    /// <summary>
    /// Adds the ESB Subscriber Client and background service to the DI Service Collection.
    /// This also creates a development subscription for the endpoint if running on a local developer machine.
    /// </summary>
    /// <param name="services">DI Service Collection.</param>
    /// <param name="environmentConfiguration">Environment Configuration.</param>
    /// <returns>Modified DI Service Collection.</returns>
    public static IServiceCollection AddEsbSubscriberClient(
        this IServiceCollection services,
        EnvironmentConfiguration environmentConfiguration)
    {
        services.AddSingleton<ISubscriberClient>(sp =>
        {
            var topicName = $"{environmentConfiguration.Name}Endpoint";

            if (environmentConfiguration.RuntimeEnvironment == RuntimeEnvironment.LocalDeveloperMachine)
            {
                var admin = sp.GetRequiredService<ServiceBusAdministrationClient>();

                var subscriptionName =
                    $"{environmentConfiguration.Name}Endpoint-{environmentConfiguration.ApplicationEnvironment}";

                var existsAsync = admin.SubscriptionExistsAsync(topicName, subscriptionName)
                    .GetAwaiter().GetResult().Value;
                if (!existsAsync)
                {
                    admin.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, subscriptionName)
                    {
                        DefaultMessageTimeToLive = TimeSpan.FromMinutes(5),
                        DeadLetteringOnMessageExpiration = false,
                        MaxDeliveryCount = 1,
                        RequiresSession = true,
                    }).GetAwaiter();
                }
            }

            return new SubscriberClient(
                sp.GetRequiredService<ServiceBusClient>(),
                topicName,
                sp.GetRequiredService<ILoggerProvider>());
        });

        services.AddSingleton<IEsbWorker, EsbWorker>();
        services.AddHostedService<EsbService>();

        return services;
    }

    /// <summary>
    /// Adds the ESB Publisher Client to the DI Service Collection.
    /// </summary>
    /// <param name="services">DI Service Collection.</param>
    /// <param name="environmentConfiguration">Environment Configuration - used to set the endpoint name.</param>
    /// <returns>Modified DI Service Collection.</returns>
    public static IServiceCollection AddEsbPublisherClient(
        this IServiceCollection services,
        EnvironmentConfiguration environmentConfiguration)
    {
        services.AddSingleton<IPublisherClient>(sp =>
            new PublisherClient(
                sp.GetRequiredService<ServiceBusClient>(),
                $"{environmentConfiguration.Name}Endpoint",
                sp.GetRequiredService<ILoggerProvider>()));

        return services;
    }
}
