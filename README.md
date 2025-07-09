# Delegateas.EnvironmentConfiguration.Core

## Environment Configuration

Add this by:

```csharp
var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

var environmentConfiguration = services.AddEnvironmentConfiguration(
    configuration,
    applicationName: "CeAdapter",
    applicationDescription: "CE Adapter Service Bus Worker");
```

This is the core library for the KL.CEAdapter project, which provides functionality for configuring the
`EnvironmentConfiguration`, which is the base used to configure all services.

The `EnvironmentConfiguraiton`, and the setup, is controlled by three variables:

- **RuntimeEnvironment**: This is where the application is running, defaulting to `LocalDeveloperMachine`.

  This is used to configure how to, for example, get the token credentials for Azure services.

- **ASPNETCORE_ENVIRONMENT**: This is the ASP.NET Core environment, defaulting to `Development`.

  This is "environment", e.g. development, dev, test, staging, production, etc.

- **InfrastructureEnvironment**: This is infrastructure environment, defaulting to `Dev`.

  The intention is to support non-prod and prod infrastructure environments to better support shared resources.

These only have to be explicitly set for "live" environments, such as "developmnet", test, production, etc.
