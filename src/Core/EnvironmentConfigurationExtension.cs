using System.Globalization;
using Azure.Core;
using Azure.Identity;
using Delegateas.DeveloperExperience.Core.Enums;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Delegateas.DeveloperExperience.Core;

public static class EnvironmentConfigurationExtension
{
    /// <summary>
    /// Configures and adds the environment configuration to the service collection.
    /// This expects the following values to be present in the ConfigurationManager, preferably automatically through
    /// appsettings.*.json.
    ///   * ApplicationConfiguration.TenantId: Used to populate TenantId in EnvironmentConfiguration
    ///   * ApplicationConfiguration.SubscriptionId: Used to populate TenantId in EnvironmentConfiguration
    /// Neither of the two values can be calculated.
    /// </summary>
    /// <param name="services">DI Service Collection.</param>
    /// <param name="configuration">Configuration Manager.</param>
    /// <param name="applicationName">Application name.</param>
    /// <param name="resourceGroupNameFunc">Function to build resource group name based in contemporary environment configuration.</param>
    /// <param name="applicationDescription">Application description.</param>
    /// <param name="managedIdentityResourceIdFunc">Function to build managed identity resource id based in contemporary environment configuration.</param>
    /// <param name="keyVaultUriFuncs">Function to build key vault URIs based in contemporary environment configuration.
    ///                                Secrets with the same key is overwritten by later URIs.</param>
    /// <returns>Environment Configuration</returns>
    /// <exception cref="NotSupportedException">If RuntimeEnvironment is unsupported.</exception>
    /// <exception cref="ArgumentException">If AZ is not authenticated.</exception>
    /// <exception cref="InvalidOperationException">When environment variables are missing.</exception>
    public static EnvironmentConfiguration AddEnvironmentConfiguration(
        this IServiceCollection services,
        ConfigurationManager configuration,
        string applicationName,
        Func<EnvironmentConfiguration, string> resourceGroupNameFunc,
        string applicationDescription = "N/A",
        Func<EnvironmentConfiguration, ResourceIdentifier>? managedIdentityResourceIdFunc = null,
        params ICollection<Func<EnvironmentConfiguration, Uri>>? keyVaultUriFuncs)
    {
        ArgumentNullException.ThrowIfNull(resourceGroupNameFunc);

        var environmentConfiguration =
            GetEnvironmentConfiguration(configuration, applicationName, applicationDescription);

        environmentConfiguration = environmentConfiguration with
        {
            ResourceGroupName = resourceGroupNameFunc(environmentConfiguration),
        };

        TokenCredential credential = environmentConfiguration.RuntimeEnvironment switch
        {
            RuntimeEnvironment.LocalDeveloperMachine => new AzureCliCredential(new AzureCliCredentialOptions
            {
                TenantId = environmentConfiguration.TenantId,
            }),
            RuntimeEnvironment.Cloud when managedIdentityResourceIdFunc is not null => new ManagedIdentityCredential(
                managedIdentityResourceIdFunc(environmentConfiguration)),
            RuntimeEnvironment.Cloud when managedIdentityResourceIdFunc is null => new ManagedIdentityCredential(),
            RuntimeEnvironment.UnitTest => new TokenCredentialMock(),
            RuntimeEnvironment.BuildServer => new ClientSecretCredential(
                tenantId: Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? throw new InvalidOperationException("AZURE_TENANT_ID environment variable is not set."),
                clientId: Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? throw new InvalidOperationException("AZURE_CLIENT_ID environment variable is not set."),
                clientSecret: Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? throw new InvalidOperationException("AZURE_CLIENT_SECRET environment variable is not set.")),
            _ => throw new NotSupportedException(
                $"Runtime environment {environmentConfiguration.RuntimeEnvironment} is not supported"),
        };

        if (environmentConfiguration.RuntimeEnvironment is
            RuntimeEnvironment.LocalDeveloperMachine or RuntimeEnvironment.Cloud
            && keyVaultUriFuncs is not null)
        {
            foreach (var keyVaultUriFunc in keyVaultUriFuncs)
            {
                configuration.AddAzureKeyVault(keyVaultUriFunc(environmentConfiguration), credential);
            }

            services.AddAzureClients(configure => configure.UseCredential(credential));
        }

        if (environmentConfiguration.RuntimeEnvironment is RuntimeEnvironment.LocalDeveloperMachine)
        {
            var accessToken = credential.GetToken(
                new TokenRequestContext(["https://graph.microsoft.com/.default"]),
                CancellationToken.None);

            var developerInitials = ExtractDeveloperInitials(accessToken);

            environmentConfiguration = environmentConfiguration with { ApplicationEnvironment = developerInitials };
        }

        environmentConfiguration = environmentConfiguration with { TokenCredential = credential };

        services.AddSingleton(environmentConfiguration)
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(configuration);

        return environmentConfiguration;
    }

    private static string ExtractDeveloperInitials(AccessToken accessToken)
    {
        // Should be UPN, but guest accounts does not have UPN, so we use email instead.
        var webToken = new JsonWebToken(accessToken.Token);
        string developerInitials;

        if (webToken.TryGetPayloadValue("upn", out string upn))
        {
            developerInitials = upn.Split('@').FirstOrDefault() ??
                                throw new ArgumentException($"Invalid developer upn {upn}", upn);
        }
        else if (webToken.TryGetPayloadValue("email", out string email))
        {
            developerInitials = email.Split('@').FirstOrDefault() ??
                                throw new ArgumentException($"Invalid developer email {email}", email);
        }
        else
        {
            throw new InvalidOperationException("JWT token does not contain either UPN or email...");
        }

        return developerInitials;
    }

    private static EnvironmentConfiguration GetEnvironmentConfiguration(
        ConfigurationManager configuration,
        string applicationName,
        string applicationDescription = "N/A")
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(applicationName);

        // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0&tabs=basicconfiguration#configuration-providers
        // The ASPNETCORE_ENVIRONMENT variable is set by the ASP.NET Core hosting environment.
        var applicationEnvironment =
            (configuration["ASPNETCORE_ENVIRONMENT"] ?? "local").ToLower(CultureInfo.InvariantCulture);

        configuration
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{applicationEnvironment}.json", optional: true);

        configuration.AddEnvironmentVariables();

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

        var tenantId = configuration["ApplicationConfiguration:TenantId"]
                       ?? throw new InvalidOperationException("ApplicationConfiguration:TenantId is not set.");

        var subscriptionId = configuration["ApplicationConfiguration:SubscriptionId"] ?? "N/A";

        var environmentConfiguration = new EnvironmentConfiguration(
            Name: applicationName,
            Description: applicationDescription,
            InfrastructureEnvironment: infrastructureEnvironment,
            ApplicationEnvironment: applicationEnvironment,
            RuntimeEnvironment: runtimeEnvironment,
            TenantId: tenantId,
            SubscriptionId: subscriptionId,
            ResourceGroupName: null!);

        return environmentConfiguration;
    }
}
