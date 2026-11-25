using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgroCore.Shared.Networking
{
    /// <summary>
    /// Convierte mensajes a JSON y de JSON a mensajes.
    /// Todos los programas usan estas mismas opciones, así hablan exactamente el mismo "idioma".
    /// </summary>
    public static class MessageSerializer
    {
        public static readonly JsonSerializerOptions Options = CreateOptions(false);
        public static readonly JsonSerializerOptions FileOptions = CreateOptions(true);

        private static JsonSerializerOptions CreateOptions(bool indented)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;          // "farmId" en vez de "FarmId"
            options.PropertyNameCaseInsensitive = true;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull; // no se envían los campos vacíos
            options.WriteIndented = indented;
            // Los enums se escriben como texto: UserRole.Farmer -> "FARMER", MachineStatus.ReturningToBase -> "RETURNING_TO_BASE"
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
            return options;
        }

        /// <summary>Objeto -> JSON en una sola línea.</summary>
        public static string ToJson(object message)
        {
            return JsonSerializer.Serialize(message, message.GetType(), Options);
        }

        /// <summary>JSON -> objeto de la clase T.</summary>
        public static T FromJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        /// <summary>
        /// Lee solo el campo "type" de un mensaje JSON para saber qué clase es.
        /// Devuelve "" si el texto no es un JSON válido.
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
                // no es JSON válido
            }
            return "";
        }
    }
}
