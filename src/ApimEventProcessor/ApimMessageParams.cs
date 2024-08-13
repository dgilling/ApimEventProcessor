namespace ApimEventProcessor 
{
    public class MessageTypeParams
    {
        public static string REQUEST = "request";
        public static string RESPONSE = "response";
    }
 
    public class MessageCommonParams
    {
        public static string EVENT_TYPE = "event_type";
        public static string MESSAGE_ID = "message-id";
        public static string CONTEXT_TIMESTAMP = "contextTimestamp";
    }

    public class MessageRequestParams
    {
        public static string METHOD = "method";
        public static string URI = "uri";
        public static string IP_ADDR = "ip_address";
        public static string USER_ID = "user_id";
        public static string COMPANY_ID = "company_id";
        public static string HEADERS = "request_headers";
        public static string BODY = "request_body";
        public static string METADATA = "metadata";
        public static string CONTEXT_USER = "contextRequestUser";
    }

    public class MessageResponseParams
    {
        public static string STATUS_CODE = "status_code";
        public static string HEADERS = "response_headers";
        public static string BODY = "response_body";
    }
}