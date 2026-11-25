using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgroCore.Shared.Networking
{
    /// <summary>
    /// Mezuak JSON bihurtzen ditu eta JSONa mezu.
    /// Programa guztiek aukera berberak erabiltzen dituzte; horrela "hizkuntza" bera hitz egiten dute.
    /// </summary>
    public static class MessageSerializer
    {
        public static readonly JsonSerializerOptions Options = CreateOptions(false);
        public static readonly JsonSerializerOptions FileOptions = CreateOptions(true);

        private static JsonSerializerOptions CreateOptions(bool indented)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;          // "farmId" "FarmId"-ren ordez
            options.PropertyNameCaseInsensitive = true;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull; // eremu hutsak ez dira bidaltzen
            options.WriteIndented = indented;
            // Enum-ak testu gisa idazten dira: UserRole.Farmer -> "FARMER", MachineStatus.ReturningToBase -> "RETURNING_TO_BASE"
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
            return options;
        }

        /// <summary>Objektua -> JSON lerro bakarrean.</summary>
        public static string ToJson(object message)
        {
            return JsonSerializer.Serialize(message, message.GetType(), Options);
        }

        /// <summary>JSON -> T klaseko objektua.</summary>
        public static T FromJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        /// <summary>
        /// JSON mezu baten "type" eremua bakarrik irakurtzen du zein klase den jakiteko.
        /// "" itzultzen du testua JSON baliozkoa ez bada.
        /// </summary>
        public static string ReadType(string json)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement typeElement;
                    if (document.RootElement.TryGetProperty("type", out typeElement))
                    {
                        return typeElement.GetString() ?? "";
                    }
                }
            }
            catch (JsonException)
            {
                // ez da JSON baliozkoa
            }
            return "";
        }
    }
}
