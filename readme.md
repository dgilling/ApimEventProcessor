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

The sample, as is, writes the HTTP messages to the Moesif API Analytics, however, by creating a new implementation of `IHttpMessageProcessor` it is trivial to change where the HTTP messages are relayed.
