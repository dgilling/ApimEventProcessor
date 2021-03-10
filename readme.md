#Azure API Management Event Processor

This sample application demonstrates using the `logtoeventhub` policy in the Azure API Management service to send events containing HTTP messages to EventHub, consume those events and forward to Moesif, a third party HTTP logging and analytics tool.

In order to run this sample you will need a number Environment variables configured with accounts and keys.

| Key Name | Purpose |
|----------|---------|
| APIMEVENTS-EVENTHUB-NAME  | Azure Event hub name configured to receive events from API Management service|
| APIMEVENTS-EVENTHUB-CONNECTIONSTRING | Azure Event hub configuration string eg: `Endpoint=sb://<sb-url>/;SharedAccessKeyName=<name>;SharedAccessKey=<key>` |
| APIMEVENTS-STORAGEACCOUNT-NAME | Azure Storage Account used for keeping track of what events have been read |
| APIMEVENTS-STORAGEACCOUNT-KEY | Key for Azure Storage Account|
| APIMEVENTS-MOESIF-APPLICATION-ID | Your Moesif Application Id(aka Collector Application Id) can be found in the Moesif Portal.<br> After signing up for a Moesif account, your Moesif Application Id will be displayed during the onboarding steps<br> Or any time by logging into the Moesif Portal, click on the top right menu, and then clicking ApiKeys or Installation. |  
| APIMEVENTS-MOESIF-SESSION-TOKEN | Request Header Key containing user's API Token such as "Authorization" or "X-Api-Token"|
| APIMEVENTS-MOESIF-API-VERSION | API Version to tag the request with such as "v1" or "1.2.1" |
| APIMEVENTS-LOG-LEVEL | Optional: default log level is `warn`. Set this env varilable to `info` or `debug` to see more logs. Set either in `run.bat` or `Azure app service setting configuration` |
|  |  |

The sample, as is, writes the HTTP messages to the Moesif API Analytics, however, by creating a new implementation of `IHttpMessageProcessor` it is trivial to change where the HTTP messages are relayed.

### How to use this app in Azure App Services


This project can be launched in `Azure App Services` as a `Azure WebJob` app.
First, download and modify the `azure-app-service-webjobs/run.bat` file in this repo.

1. Log into Azure portal, and create/open an Azure Web Services console.
    Tested under Stack: `.NET` `.NET 5 (Early Access)` using pricing tier `f1 - free - dev / test workloads`
2. Under Settings / Configuration, add the above specified Environment variables as 
   `Application settings` > "name" "value"
3. Under Settings panel on left, click on `WebJobs`
4. Select `+ Add` to add this job
5. In the `Add WebJob` panel, fill out:
  - Name: enter any name for this job
  - File Upload: Upload the `run.bat` file (`azure-app-service-webjobs/run.bat`)
  - Type: "continuous"
6. Save

The job should immediately begin. View logs to see output.

Troubleshooting:
1. The maximum size of data sent to log-to-eventhub is limited by azure to approx 200000 chars. So the body size in policy.xml must be limited to below that limit. Log-to-Eventhub truncates at that limit.
2. Ensure the `policy.xml` matches the correct version of ApimEventProcessor. 
3. The Checkpoint to azure storage occurs only after CHECKPOINT_MINIMUM_INTERVAL_MINUTES (5 mins) has elapsed and after new event is received. If this program is restarted prior to checkpoint, all events after last checkpoint are sent to Moesif. This may lead to duplicate events in Moesif.
4. Enable more detailed logs by setting `APIMEVENTS-LOG-LEVEL` environment variable to `debug` or `info` from default `warn`