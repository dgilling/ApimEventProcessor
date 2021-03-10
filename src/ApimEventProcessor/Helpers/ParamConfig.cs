using System;

namespace ApimEventProcessor.Helpers
{
    // Environment varilables for configuring Moesif
    public static class MoesifAppParamNames
    {
        // Required
        public const string APP_ID = "APIMEVENTS-MOESIF-APPLICATION-ID";

        //Optional
        public const string SESSION_TOKEN = "APIMEVENTS-MOESIF-SESSION-TOKEN";

        //Optional
        public const string API_VERSION = "APIMEVENTS-MOESIF-API-VERSION";

        //Optional
        public const string CONFIG_FETCH_INTERVAL_MINS = "APIMEVENTS-MOESIF-CONFIG-FETCH-INTERVAL";
    }

    // Environment varilables for configuring Azure Eventhub and storage account
    public static class AzureAppParamNames
    {
        // Required
        public const string EVENTHUB_CONN_STRING = "APIMEVENTS-EVENTHUB-CONNECTIONSTRING";

        // Required
        public const string EVENTHUB_NAME = "APIMEVENTS-EVENTHUB-NAME";

        // Required
        public const string STORAGEACCOUNT_NAME = "APIMEVENTS-STORAGEACCOUNT-NAME";

        // Required
        public const string STORAGEACCOUNT_KEY = "APIMEVENTS-STORAGEACCOUNT-KEY";
    }

    public static class AppExecuteParams
    {
        // Optional:
        // This log level May be configured in azure web app config env vars
        public const string APIMEVENTS_LOG_LEVEL = "APIMEVENTS-LOG-LEVEL";
    }

    public static class RunParams
    {
        // Frequency at which events are checkpointed to Azure Storage.
        public const int CHECKPOINT_MINIMUM_INTERVAL_MINUTES = 5;
        
        // Frequency at which Moesif configuration is fetched.
        public const int CONFIG_FETCH_INTERVAL_MINUTES = 5;
        
    }    
    
    class ParamConfig
    {
        public static string load(string v)
        {
            return Environment.GetEnvironmentVariable(v,
                    EnvironmentVariableTarget.Process);
        }

        public static string loadDefaultEmpty(string v)
        {
            return loadWithDefault(v, "");
        }

        public static string loadWithDefault(string v, string defaultVal)
        {
            var val = load(v);
            if (string.IsNullOrWhiteSpace(val))
                val = defaultVal;
            return val.Trim();
        }

        public static int loadWithDefault(string v, int defaultVal)
        {
            int ival = defaultVal;
            try
            {
                var val = loadWithDefault(v, "");
                if (!string.IsNullOrWhiteSpace(val) && (Int32.Parse(val) > 0 ))
                    ival = Int32.Parse(val);
            }
            catch (Exception){}
            return ival;
        }

        public static string loadNonEmpty(string varName)
        {
            string val = loadDefaultEmpty(varName);
            if (string.IsNullOrWhiteSpace(val))
                throw new ArgumentException("Required parameter not found: " + varName);
            return val.Trim();
        }
    }
}