using System.Collections.Generic;
using System;
using Newtonsoft.Json.Linq;

namespace ApimEventProcessor.Helpers
{
    public class HeadersUtils
    {
        public static Dictionary<string, string> deSerializeHeaders(object headerString)
        {
            string h = null;
            if ( null != headerString)
                h = (string) headerString;
            return deSerializeHeaders(h);
        }

        public static Dictionary<string, string> deSerializeHeaders(string headerString)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(headerString))
            {
                string[] splitHeaders = headerString.Split(new string[] {";;"},
                                                            StringSplitOptions.None);
                foreach (var h in splitHeaders)
                {
                    if (!string.IsNullOrWhiteSpace(h) && h.Contains(":"))
                    {
                        string[] kv = h.Split(new char[]{':'}, 2);
                        if (!string.IsNullOrWhiteSpace(kv[0]))
                            headers.Add(kv[0].Trim(), kv[1].Trim());
                    }
                }
            }
            return headers;
        }

        public static Boolean isContentTypeHeader(string h)
        {
            return !string.IsNullOrWhiteSpace(h)
                && (h.Trim().ToLower() == "content-type");
        }
    }

    public class BodyUtil
    {
        public static Tuple<object, string> Serialize(string data)
        {
            string encoding = "base64";
            object content = data;
            if (!string.IsNullOrWhiteSpace(data))
            {
                try 
                {
                    var jobj = TryParseToJson(b64Decode(data.Trim()));
                    if (null != jobj)
                    {
                        encoding = null;
                        content = jobj;
                    }
                }
                catch (Exception){}
            }
            return new Tuple<object, string>(content, encoding);
        }

        public static string b64Decode(string data){
            return System.Text.Encoding.UTF8.GetString(
                                    Convert.FromBase64String(data));
        }

        public static JToken TryParseToJson(string data) {
            JToken jt = null;
            try
            {
                data = data.Trim();
                if (isMaybeUnencodedJson(data))
                    jt = JToken.Parse(data);
            }
            catch (Exception) {
                // If the policy.xml truncated original content due to size limitations,
                // it is expected for parsing to fail.
             }
            return jt;
        }

        public static Boolean isMaybeUnencodedJson(string data)
        {
            return (data.StartsWith("{") && data.EndsWith("}"))
                || (data.StartsWith("[") && data.EndsWith("]"));
        }
    }
}