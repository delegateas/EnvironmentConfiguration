using Azure.Core;
using Delegateas.EnvironmentConfiguration.Core.Enums;

namespace Delegateas.EnvironmentConfiguration.Core;

public record EnvironmentConfiguration(
    string Name,
    string Description,
    InfrastructureEnvironment InfrastructureEnvironment,
    string ApplicationEnvironment,
    RuntimeEnvironment RuntimeEnvironment,
    string TenantId,
    string SubscriptionId,
    string ResourceGroupName,
    OAuthConfiguration? OAuth = null,
    TokenCredential? TokenCredential = null);
