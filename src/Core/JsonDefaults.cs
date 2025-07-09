using System.Text.Json;
using System.Text.Json.Serialization;

namespace Delegateas.DeveloperExperience.Core;

public static class JsonDefaults
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        return options.ConfigureDefaults();
    }

    public static JsonSerializerOptions Options { get; } = Create();

    public static JsonSerializerOptions ConfigureDefaults(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.WriteIndented = true;

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
