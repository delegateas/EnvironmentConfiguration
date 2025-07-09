using System.Globalization;
using Azure.Core;
using Azure.Identity;
using Delegateas.EnvironmentConfiguration.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Delegateas.EnvironmentConfiguration.Core;

public static class EnvironmentConfigurationExtension
{
    /// <summary>
    /// Configures and adds the environment configuration to the service collection.
    /// </summary>
    /// <param name="services">DI Service Collection.</param>
    /// <param name="configuration">Configuration Manager.</param>
    /// <param name="applicationName">Application name.</param>
    /// <param name="applicationDescription">Application description.</param>
    /// <param name="managedIdentityResourceId">Resource Identifier if using user assigned managed identity.</param>
    /// <returns>Environment Configuration</returns>
    /// <exception cref="NotSupportedException">If RuntimeEnvironment is unsupported.</exception>
    /// <exception cref="ArgumentException">If AZ is not authenticated.</exception>
    public static EnvironmentConfiguration AddEnvironmentConfiguration(
        this IServiceCollection services,
        ConfigurationManager configuration,
        string applicationName,
        string applicationDescription = "N/A",
        ResourceIdentifier? managedIdentityResourceId = null)
    {
        var environmentConfiguration =
            GetEnvironmentConfiguration(configuration, applicationName, applicationDescription);

        TokenCredential credential = environmentConfiguration.RuntimeEnvironment switch
        {
            RuntimeEnvironment.LocalDeveloperMachine => new AzureCliCredential(new AzureCliCredentialOptions
            {
                TenantId = environmentConfiguration.TenantId,
            }),
            RuntimeEnvironment.Cloud when managedIdentityResourceId is not null => new ManagedIdentityCredential(
                managedIdentityResourceId),
            RuntimeEnvironment.Cloud when managedIdentityResourceId is null => new ManagedIdentityCredential(),
            RuntimeEnvironment.UnitTest => new TokenCredentialMock(),
            _ => throw new NotSupportedException(
                $"Runtime environment {environmentConfiguration.RuntimeEnvironment} is not supported"),
        };

        if (environmentConfiguration.RuntimeEnvironment is
            RuntimeEnvironment.LocalDeveloperMachine or RuntimeEnvironment.Cloud)
        {
            var keuVaultUri = configuration["ApplicationConfiguration:KeyVaultUri"];
            ArgumentNullException.ThrowIfNull(keuVaultUri);

            configuration.AddAzureKeyVault(new Uri(keuVaultUri), credential);

            services.AddAzureClients(configure => configure.UseCredential(credential));
        }

        if (environmentConfiguration.RuntimeEnvironment is RuntimeEnvironment.LocalDeveloperMachine)
        {
            var accessToken = credential.GetToken(
                new TokenRequestContext(["https://graph.microsoft.com/.default"]),
                CancellationToken.None);

            // Should be UPN, but guest accounts does not have UPN, so we use email instead.
            var developerUpn = new JsonWebToken(accessToken.Token).GetPayloadValue<string>("email");
            var developerInitials = developerUpn.Split('@').FirstOrDefault() ??
                                    throw new ArgumentException($"Invalid developer upn {developerUpn}", developerUpn);
            environmentConfiguration = environmentConfiguration with { ApplicationEnvironment = developerInitials };
        }

        environmentConfiguration = environmentConfiguration with { TokenCredential = credential };

        services.AddSingleton(environmentConfiguration)
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(configuration);

        return environmentConfiguration;
    }

    private static EnvironmentConfiguration GetEnvironmentConfiguration(
        ConfigurationManager configuration,
        string applicationName,
        string applicationDescription = "N/A")
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(applicationName);

        // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0&tabs=basicconfiguration#configuration-providers
        var applicationEnvironment =
            (configuration["ASPNETCORE_ENVIRONMENT"] ?? "development").ToLower(CultureInfo.InvariantCulture);

        Enum.TryParse<InfrastructureEnvironment>(
            configuration["INFRASTRUCTURE_ENVIRONMENT"],
            ignoreCase: true,
            out var infrastructureEnvironment); // Defaults to Dev

        Enum.TryParse<RuntimeEnvironment>(
            configuration["RUNTIME_ENVIRONMENT"],
            ignoreCase: true,
            out var runtimeEnvironment); // Defaults to LocalDeveloperMachine

#pragma warning disable CA1303
        Console.WriteLine("Environment configuration:");
#pragma warning restore CA1303
        Console.WriteLine($"   Application environment: {applicationEnvironment}");
        Console.WriteLine($"   Infrastructure environment: {infrastructureEnvironment}");
        Console.WriteLine($"   Runtime environment: {runtimeEnvironment}");

        var tenantId = configuration["ApplicationConfiguration:TenantId"];
        ArgumentNullException.ThrowIfNull(tenantId);

        var resourceGroupName = configuration["ApplicationConfiguration:ResourceGroupName"];
        ArgumentNullException.ThrowIfNull(resourceGroupName);

        configuration.AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{applicationEnvironment}.json", optional: true);

        configuration.AddEnvironmentVariables();
        var subscriptionId = configuration["ApplicationConfiguration:SubscriptionId"] ?? "N/A";

        var environmentConfiguration = new EnvironmentConfiguration(
            Name: applicationName,
            Description: applicationDescription,
            InfrastructureEnvironment: infrastructureEnvironment,
            ApplicationEnvironment: applicationEnvironment,
            RuntimeEnvironment: runtimeEnvironment,
            TenantId: tenantId,
            SubscriptionId: subscriptionId,
            ResourceGroupName: resourceGroupName);

        return environmentConfiguration;
    }
}
