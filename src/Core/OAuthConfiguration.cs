namespace Delegateas.EnvironmentConfiguration.Core;

public record OAuthConfiguration(string ClientId, string Scope, string? ClientSecret = null);
