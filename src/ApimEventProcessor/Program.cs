using System;
using ApimEventProcessor.Helpers;
using Microsoft.ServiceBus.Messaging;

namespace ApimEventProcessor
{
/*
    The purpose of this Application is to read request/response events from Eventhub
    and send them to Moesif.com for API Analytics
    The events from Azure Api Management are sent to Eventhub using Log-to-Eventhub policy
    This app uses Azure Storage to save checkpoints of Eventhub events consumption.
*/
    class Program
    {
        static void Main(string[] args)
        {
            var logger = GetLogger();
            logger.LogInfo("STARTING Moesif API Management Event Processor. Reading environment variables");
            // Load configuration paramaters from Environment
            string eventHubConnectionString = ParamConfig.loadNonEmpty(AzureAppParamNames.EVENTHUB_CONN_STRING);
            string eventHubName = ParamConfig.loadNonEmpty(AzureAppParamNames.EVENTHUB_NAME); 
            string storageAccountName = ParamConfig.loadNonEmpty(AzureAppParamNames.STORAGEACCOUNT_NAME); 
            string storageAccountKey = ParamConfig.loadNonEmpty(AzureAppParamNames.STORAGEACCOUNT_KEY);
            // This App utilizes the "$Default" Eventhub consumer group
            // In future, we should make this configurable via environment variables
            string eventHubConsumerGroupName = EventHubConsumerGroup.DefaultGroupName;
            // Create connection string for azure storage account to store checkpoints
            string storageConnectionString = makeStorageAccountConnString(storageAccountName,
                                                                            storageAccountKey);
            string eventProcessorHostName = Guid.NewGuid().ToString();
            var eventProcessorHost = new EventProcessorHost(
                                                eventProcessorHostName,
                                                eventHubName,
                                                eventHubConsumerGroupName,
                                                eventHubConnectionString,
                                                storageConnectionString);
            logger.LogDebug("Registering EventProcessor...");
            var httpMessageProcessor = new MoesifHttpMessageProcessor(logger);
            eventProcessorHost.RegisterEventProcessorFactoryAsync(
                new ApimHttpEventProcessorFactory(httpMessageProcessor, logger));
            
            logger.LogInfo("Process is running. Press enter key to end...");
            Console.ReadLine();
            logger.LogInfo("STOPPING Moesif API Management Event Processor");
            eventProcessorHost.UnregisterEventProcessorAsync().Wait();
        }

        // Build Azure Storage Account Connection String
        public static string makeStorageAccountConnString(string storageAccountName,
                                                            string storageAccountKey)
        {
            return string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                storageAccountName, storageAccountKey);
        }

        // Read loglevel from environment, else use system default.
        // APIMEVENTS_LOG_LEVEL takes priority over DEFAULT_LOG_LEVEL
        // if neither are configured, use warning as log level.
        public static ConsoleLogger GetLogger()
        {
            var aloglevelparam = ParamConfig.loadDefaultEmpty(AppExecuteParams.APIMEVENTS_LOG_LEVEL);
            var logLevel = LogLevelUtil.getLevelFromString(aloglevelparam, LogLevel.Warning);
            var infoLogger = new ConsoleLogger(LogLevel.Info);
            infoLogger.LogInfo("Setting loglevel to: [ " 
                                + logLevel.ToString() 
                                + " ] | '" + AppExecuteParams.APIMEVENTS_LOG_LEVEL + "': [ "
                                + aloglevelparam 
                                + " ]"
                                );
            var logger = new ConsoleLogger(logLevel);
            return logger;
        }
    }
}
