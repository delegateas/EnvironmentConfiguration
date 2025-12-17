# ESB.DeveloperExperience

This package is built on-top of [`Delegateas.DeveloperExperience`](https://github.com/delegateas/EnvironmentConfiguration/), which streamlines the process of configuring .NET Web Apps and unify the experience across .NET projects.
(_It was decided to move this into separate projects, this might make changes less quickly since two repos must be updated. There is no harm taking the code and host it in K&L in the future, since its using MIT license._)

What this does:
- Unifies how `TokenCredential` are configured across environments. Locally it uses `AzCliCredential`, in the cloud it uses `ManagedIdentityCredential`.
- Load secrets from Azure Key Vault into configuration, see [Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-10.0#secret-storage-in-the-production-environment-with-azure-key-vault).
  
  We use Azure Key Vault for development also, to avoid passing secrets around.

`Delegateas.DeveloperExperience` is configured as:
```csharp
var environmentConfiguration = services.AddEnvironmentConfiguration(
    configuration,
    applicationName: "CEAdapter",
    resourceGroupNameFunc: ec => $"dg-ceadapter-{ec.InfrastructureEnvironment}-rg",
    applicationDescription: "CE Adapter Service Bus Worker",
    managedIdentityResourceIdFunc: ec =>
        ResourceIdentifier.Parse(
            $"/subscriptions/{ec.SubscriptionId}/resourceGroups/{ec.ResourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/ceadapter-{ec.InfrastructureEnvironment}-uami-01"),
    ec => new Uri($"https://ceadapter-{ec.InfrastructureEnvironment}-kv-01.vault.azure.net/"));
```

This projects builds on `Delegateas.DeveloperExperience` and adds extension methods for ESB Service Bus integration.

This streamlines the process of setting up publisher and subscribers for the ESB.

Before adding either a publisher or subscriber, you need to add the Azure Clients by `.AddEsbServiceBusClient(environmentConfiguration)`. The EnvironmentConfiguration is from `Delegateas.EnvironmentConfiguration.Core`, and created as:
the `managedIdentityResourceIdFunc` is optional, and only "required" if you use a user assigned managed identity.


## Publisher

Add the `IPublisherClient` by `.AddEsbPublisherClient(environmentConfiguration)`. This uses an "Endpoint" determined by `environmentConfiguration.Name` and appends `Endpoint`. 

## Subscriber

Add the `ISubscriberClient` by `.AddEsbSubscriberClient(environmentConfiguration)`. This uses an "Endpoint" determined by `environmentConfiguration.Name` and appends `Endpoint`. If the application is running in `LocalDeveloperMachine`, it will create a new subscription appended with `-<your-email-prefix>` (E.g., `CEAdapterEndpoint-tst`) and use it to consume events, to not interfere with other developers or the live dev environment. 

## Example

See the example in `samples/` on how it is used.