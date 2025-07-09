namespace Delegateas.EnvironmentConfiguration.Core.Enums;

public enum RuntimeEnvironment
{
    LocalDeveloperMachine, // When starting from local development machine (F5)
    Cloud, // When running in any cloud environment (dev, test, prod)
    UnitTest, // Set when running unit tests locally or on build server, to avoid having to set up a TokenCredential
    AddMigration, // Set when running the add-migration.ps1 from the cli
    BuildServer, // Set when running on build server
}
