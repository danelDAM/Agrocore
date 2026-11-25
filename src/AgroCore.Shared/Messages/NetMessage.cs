using System;

namespace AgroCore.Shared.Messages
{
    /// <summary>
    /// Clase base de TODOS los mensajes. Cada mensaje viaja como una línea de texto en JSON, por ejemplo:
    ///
    ///   {"type":"LOGIN_REQUEST","username":"juan","password":"..."}
    ///
    /// El campo "type" dice qué mensaje es. Quien lo recibe mira primero el "type"
    /// (MessageSerializer.ReadType) y después lo convierte a la clase correcta con un switch.
    /// </summary>
    public class NetMessage
    {
        public string Type { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>Base de las respuestas: dicen si la operación fue bien y un texto explicativo.</summary>
    public class ResponseMessage : NetMessage
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; }
    }

    /// <summary>
    /// Respuesta genérica "RESULT". RequestType indica a qué petición responde
    /// (así el cliente sabe qué ha ido bien o mal).
    /// </summary>
    public class OperationResult : ResponseMessage
    {
        public string RequestType { get; set; }

        public OperationResult()
        {
            Type = MessageTypes.Result;
        }

        public static OperationResult Ok(string requestType, string message)
        {
            OperationResult result = new OperationResult();
            result.RequestType = requestType;
            result.Success = true;
            result.Message = message;
            return result;
        }

        public static OperationResult Fail(string requestType, string message)
        {
            OperationResult result = new OperationResult();
            result.RequestType = requestType;
            result.Success = false;
            result.Message = message;
            return result;
        }
    }
}
