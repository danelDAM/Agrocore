using System;

namespace AgroCore.Shared.Messages
{
    /// <summary>
    /// Mezu GUZTIEN oinarrizko klasea. Mezu bakoitza JSON testu-lerro gisa bidaiatzen du, adibidez:
    ///
    ///   {"type":"LOGIN_REQUEST","username":"juan","password":"..."}
    ///
    /// "type" eremuak adierazten du zein mezu den. Jasotzen duenak lehenik "type" begiratzen du
    /// (MessageSerializer.ReadType) eta gero switch batekin klase egokira bihurtzen du.
    /// </summary>
    public class NetMessage
    {
        public string Type { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>Erantzunen oinarria: eragiketa ondo joan den eta azalpen-testu bat adierazten dute.</summary>
    public class ResponseMessage : NetMessage
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; }
    }

    /// <summary>
    /// "RESULT" erantzun generikoa. RequestType-k zein eskaerari erantzuten dion adierazten du
    /// (horrela bezeroak badaki zer joan den ondo edo gaizki).
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
