using System.Text.Json;
using DatEngine.Util.Json.Generated;

namespace DatEngine.Util;

public static class JsonHelper {
    public static JsonSerializerOptions GetEngineJsonOptions() {
        var jsonSerializerOptions = new JsonSerializerOptions();
        jsonSerializerOptions.Converters.Add(new float4x4JsonConverter());

        return jsonSerializerOptions;
    }
}