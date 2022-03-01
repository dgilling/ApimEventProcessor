using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ApimEventProcessor.Helpers;

namespace ApimEventProcessor
{
    public class MoesifRequest
    {
        public MoesifRequest(JObject request)
        {
            eventType = JObjectUtil.getString(request, MessageCommonParams.EVENT_TYPE);
            messageId = JObjectUtil.getString(request, MessageCommonParams.MESSAGE_ID);
            method = JObjectUtil.getString(request, MessageRequestParams.METHOD);
            uri = JObjectUtil.getString(request, MessageRequestParams.URI);
            ipAddress = JObjectUtil.getString(request, MessageRequestParams.IP_ADDR);
            userId = JObjectUtil.getString(request, MessageRequestParams.USER_ID);
            companyId = JObjectUtil.getString(request, MessageRequestParams.COMPANY_ID);
            requestHeaders = JObjectUtil.getString(request, MessageRequestParams.HEADERS);
            requestBody = JObjectUtil.getString(request, MessageRequestParams.BODY);
            metadata = JObjectUtil.getObjectDict(request, MessageRequestParams.METADATA);
            contextRequestUser = JObjectUtil.getStringDefaultVal(request, MessageRequestParams.CONTEXT_USER, null);
            contextTimestamp = JObjectUtil.getDatetimeDefaultUtcNow(request, MessageCommonParams.CONTEXT_TIMESTAMP);
        }

        public string eventType { get; set; }
        public string messageId { get; set; }
        public string method { get; set; }
        public string uri { get; set; }
        public string ipAddress { get; set; }
        public string userId { get; set; }
        public string companyId { get; set; }
        public string requestHeaders { get; set; }
        public string requestBody { get; set; }
        public Dictionary<string, object> metadata { get; set; }
        public string contextRequestUser { get; set; }
        public DateTime contextTimestamp { get; set; }
    }
    public class MoesifResponse
    {
        public MoesifResponse(JObject response)
        {
            eventType = JObjectUtil.getString(response, MessageCommonParams.EVENT_TYPE);
            messageId = JObjectUtil.getString(response, MessageCommonParams.MESSAGE_ID);
            statusCode = JObjectUtil.getString(response, MessageResponseParams.STATUS_CODE);
            responseHeaders = JObjectUtil.getString(response, MessageResponseParams.HEADERS);
            responseBody = JObjectUtil.getString(response, MessageResponseParams.BODY);
            contextTimestamp = JObjectUtil.getDatetimeDefaultUtcNow(response, MessageCommonParams.CONTEXT_TIMESTAMP);
        }

        public string eventType { get; set; }
        public string messageId { get; set; }
        public string statusCode { get; set; }
        public string responseHeaders { get; set; }
        public string responseBody { get; set; }
        public DateTime contextTimestamp { get; set; }
    }
 
    /// <summary>
    /// Parser for format being sent from APIM logtoeventhub policy that contains a complete HTTP request or response message.
    /// </summary>
    /// <remarks>
    ///     Might want to add a version number property to the format before actually letting it out
    ///     in the wild.
    /// </remarks>
    public class HttpMessage
    {
        public Guid MessageId { get; set; }
        public bool IsRequest { get; set; }
        public HttpRequestMessage HttpRequestMessage { get; set; }
        public HttpResponseMessage HttpResponseMessage { get; set; }
        public string ContextRequestUser {get; set;}
        public string ContextRequestIpAddress {get; set;}
        public DateTime ContextTimestamp {get; set;}

        public static HttpMessage Parse(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                return Parse(sr.ReadToEnd());
            }
        }

        private static void TransformResponseHeaders(HttpResponseMessage response, string headerString) 
        {
            Dictionary<string, string> headers = HeadersUtils.deSerializeHeaders(headerString);
            foreach (var h in headers)
            {
                string n = h.Key.Trim();
                string v = h.Value.Trim();
                if(HeadersUtils.isContentTypeHeader(n))
                {
                    try {
                        response.Content.Headers.ContentType = new MediaTypeHeaderValue(v);
                    }
                    catch (Exception){
                        // Some content headers throw exception eg
                        //The format of value 'text/plain; charset=utf-8' is invalid.
                        response.Headers.TryAddWithoutValidation(n, v);
                    }
                }
                else
                    response.Headers.TryAddWithoutValidation(n, v);
            }
            return ;
        }

        public static HttpMessage Parse(string data)
        {
            var httpMessage = new HttpMessage();
            var request = new HttpRequestMessage();
            var response = new HttpResponseMessage();

            // Convert the data into json object
            dynamic jsonObject  = JsonConvert.DeserializeObject(data); 

            if (JObjectUtil.containsKeyWithNonEmptyString(jsonObject, MessageCommonParams.EVENT_TYPE) && 
                    JObjectUtil.containsKeyWithNonEmptyString(jsonObject, MessageCommonParams.MESSAGE_ID)) 
            {
                if (jsonObject[MessageCommonParams.EVENT_TYPE] == MessageTypeParams.REQUEST) {
                    httpMessage.IsRequest = true;
                    MoesifRequest mo_req = new MoesifRequest(jsonObject);
                    httpMessage.MessageId = Guid.Parse(mo_req.messageId);
                    request.Method = new HttpMethod(mo_req.method.ToUpper());
                    request.RequestUri = new Uri(mo_req.uri);
                    request.Properties.Add("UserId", mo_req.userId);
                    request.Properties.Add("CompanyId", mo_req.companyId);
                    request.Properties.Add("ReqHeaders", mo_req.requestHeaders);
                    request.Properties.Add("Metadata", mo_req.metadata);
                    request.Content = new StringContent(mo_req.requestBody);
                    httpMessage.ContextRequestIpAddress = mo_req.ipAddress;
                    httpMessage.ContextRequestUser = mo_req.contextRequestUser;
                    httpMessage.ContextTimestamp = mo_req.contextTimestamp;
                } else {
                    httpMessage.IsRequest = false;
                    MoesifResponse mo_res = new MoesifResponse(jsonObject);
                    httpMessage.MessageId = Guid.Parse(mo_res.messageId);
                    httpMessage.ContextTimestamp = mo_res.contextTimestamp;
                    response.StatusCode = (HttpStatusCode) Convert.ToInt32(mo_res.statusCode);
                    response.Content = new StringContent(mo_res.responseBody);
                    TransformResponseHeaders(response, mo_res.responseHeaders);
                }
		    } else {
                throw new ArgumentException("Invalid formatted event :" + data);
            }

            if (httpMessage.IsRequest)
            {
                httpMessage.HttpRequestMessage = request;
            }
            else
            {
                httpMessage.HttpResponseMessage = response;
            }
            return httpMessage;
        }
    }

    public class MissingKeyException: Exception {
        public MissingKeyException(string message): base(message) {
        }
    }

    public class JObjectUtil
    {
        public static void ensureContainsKey(JObject j, String key)
        {
            if (!j.ContainsKey(key))
                throw new MissingKeyException("Required key missing: " + key);
        }

        public static String getString(JObject j, String key)
        {
            ensureContainsKey(j, key);
            return (string) j[key];
        }

        public static String getStringDefaultVal(JObject j, String key, String defaultVal)
        {
            String v = defaultVal;
            try
            {
                v = getString(j, key);
            }
            catch (Exception){}
            return v;
        }

        public static Dictionary<string, object> getObjectDict(JObject j, String key)
        {
            ensureContainsKey(j, key);
            Dictionary<string, object> o = null;
            try {
                o = j[key].ToObject<Dictionary<string, object>>();
            }
            catch (Exception){}
            return o;
        }

        public static DateTime getDatetimeDefaultUtcNow(JObject j, String key)
        {
            DateTime v = DateTime.UtcNow;
            try
            {
                ensureContainsKey(j, key);
                JToken dtVal = j[key];
                switch (dtVal.Type) {
                    case JTokenType.Date:
                        v = (DateTime) dtVal;
                        break;
                }
            }
            catch (Exception){}
            return v;
        }

        public static Boolean containsKeyWithNonEmptyString(JObject j, String key)
        {
            return j.ContainsKey(key) && !String.IsNullOrEmpty(JObjectUtil.getString(j, key));
        }
    }
}