# Service Bus Queue Listener

A containerised .NET Core event listener that listens to Azure Service Bus Queues and passes requests on to an internal endpoint

[![Build Status](https://dev.azure.com/theazurechallenge/Kubernetes/_apis/build/status/Code/Azure.azch-sblistener)](https://dev.azure.com/theazurechallenge/Kubernetes/_build/latest?definitionId=15)

## Environment Variables

The following environment variables need to be passed to the container:

### Logging

```
ENV TEAMNAME=[YourTeamName]
ENV APPINSIGHTS_KEY=[YourCustomApplicationInsightsKey] # Optional, create your own App Insights resource
ENV CHALLENGEAPPINSIGHTS_KEY=[Challenge Application Insights Key] # Override, if given one by the proctors
```

### Service Bus

```
ENV SERVICEBUSCONNSTRING="Endpoint=sb://[yourservicebusname].servicebus.windows.net/;SharedAccessKeyName=[keyname];SharedAccessKey=[key]"
ENV SERVICEBUSQUEUENAME="[YourServiceBusQueueName]"
```

### For Process Endpoint

```
ENV PROCESSENDPOINT=http://fulfillorder.[namespace].svc.cluster.local:8080/v1/order/
```

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
