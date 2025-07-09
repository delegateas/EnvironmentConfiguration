using Azure.Core;
using Delegateas.DeveloperExperience.Core.Enums;

namespace Delegateas.DeveloperExperience.Core;

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
