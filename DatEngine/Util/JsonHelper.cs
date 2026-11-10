using System.Text.Json;
using System.Text.Json.Serialization;
using DatEngine.Util.Json.Generated;

namespace DatEngine.Util;

/// <summary>
/// A utility class with methods for using JSON
/// </summary>
public static class JsonHelper {
    /// <summary>
    /// A preconfigured <see cref="JsonSerializerOptions"/> containing the engine standard settings, and several
    /// converters for types used by the engine.
    /// </summary>
    public static JsonSerializerOptions EngineJsonOptions { get; } = CreateOptions();

    /// <summary>
    ///Serialize an object into a JSON string using the engine's preconfigured settings and converters
    /// </summary>
    /// <param name="obj">The object to serialize</param>
    /// <typeparam name="T">The type of the object being serialized</typeparam>
    /// <returns>The JSON String that represents the <paramref name="obj"/></returns>
    /// <seealso cref="JsonDeserialize{T}"/>
    /// <seealso cref="EngineJsonOptions"/>
    /// <exception cref="T:System.NotSupportedException">There is no compatible <see cref="JsonConverter" /> for <typeparamref name="T" /> or its serializable members.</exception>
    public static string JsonSerialize<T>(T obj) {
        return JsonSerializer.Serialize(obj, EngineJsonOptions);
    }

    /// <summary>
    /// Deserialize a JSON String using the engine's preconfigured settings and converters
    /// </summary>
    /// <param name="json">The JSON string to deserialize</param>
    /// <typeparam name="T">The type to convert the JSON string into</typeparam>
    /// <returns>A new instance of T containing the data from the JSON string</returns>
    ///
    /// <exception cref="T:System.ArgumentNullException">
    /// <paramref name="json" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="T:System.Text.Json.JsonException">The JSON is invalid.
    /// -or-
    /// <typeparamref name="T" /> is not compatible with the JSON.
    /// -or-
    /// There is remaining data in the string beyond a single JSON value.
    /// </exception>
    /// <exception cref="T:System.NotSupportedException">
    /// There is no compatible <see cref="JsonConverter" /> for <typeparamref name="T" /> or its serializable members.
    /// </exception>
    ///
    /// <seealso cref="JsonSerialize"/>
    /// <seealso cref="EngineJsonOptions"/>
    public static T? JsonDeserialize<T>(string json) {
        return JsonSerializer.Deserialize<T>(json, EngineJsonOptions);
    }

    /// <summary>
    /// Build the default options used by the engine
    /// <para>
    /// Sets the standard options used by the engine, and loads the custom converters
    /// </para>
    /// </summary>
    /// <returns>A new JsonSerializerOptions with all the config</returns>
    private static JsonSerializerOptions CreateOptions() {
        var options = new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        foreach (var brutalNumericConverter in BrutalNumericsJson.BrutalNumericConverters) {
            options.Converters.Add(brutalNumericConverter);
        }

        return options;
    }
}