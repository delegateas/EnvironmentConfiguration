namespace Delegateas.DeveloperExperience.Core;

public record OAuthConfiguration(string ClientId, string Scope, string? ClientSecret = null);
