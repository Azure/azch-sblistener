using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.ServiceBus;

namespace sblistener
{
    class Program
    {
        // Connection String for the namespace can be obtained from the Azure portal under the 
        // 'Shared Access policies' section.
        private static string ServiceBusConnectionString = "<your_connection_string>";
        private static string ChallengeAppInsightsKey;
        private static string AppInsightsKey;
        private static string TeamName;

        private static string ProcessEndpoint;

        private static IQueueClient queueClient;

        private static TelemetryClient telemetryClient;
        private static TelemetryClient challengeTelemetryClient;

        private static readonly HttpClient httpClient = new HttpClient();


        static async Task Main(string[] args)
        {
            InitEnvVars();
            InitAppInsights();

            var csb = new ServiceBusConnectionStringBuilder(ServiceBusConnectionString);
            queueClient = new QueueClient(csb);

            // Register QueueClient's MessageHandler and receive messages in a loop
            RegisterOnMessageHandlerAndReceiveMessages();

            Console.WriteLine("======================================================");
            Console.WriteLine("Listening to messages from Service Bus Queue.");
            Console.WriteLine("======================================================");

            await Task.Delay(Timeout.Infinite);

            await queueClient.CloseAsync();

        }

        static void InitEnvVars()
        {
            ServiceBusConnectionString = Environment.GetEnvironmentVariable("SERVICEBUSCONNSTRING");
            if (string.IsNullOrEmpty(ServiceBusConnectionString))
            {
                throw new ArgumentNullException("SERVICEBUSCONNSTRING is empty");
            }

            ChallengeAppInsightsKey = Environment.GetEnvironmentVariable("CHALLENGEAPPINSIGHTS_KEY");
            if (string.IsNullOrEmpty(ChallengeAppInsightsKey))
            {
                throw new ArgumentNullException("CHALLENGEAPPINSIGHTS_KEY is empty");
            }

            AppInsightsKey = Environment.GetEnvironmentVariable("APPINSIGHTS_KEY");

            TeamName = Environment.GetEnvironmentVariable("TEAMNAME");
            if (string.IsNullOrEmpty(TeamName))
            {
                throw new ArgumentNullException("TEAMNAME is empty");
            }

            ProcessEndpoint = Environment.GetEnvironmentVariable("PROCESSENDPOINT");
            if (string.IsNullOrEmpty(ProcessEndpoint))
            {
                throw new ArgumentNullException("PROCESSENDPOINT is empty");
            }
        }

        static void InitAppInsights()
        {
            if (!string.IsNullOrEmpty(AppInsightsKey))
            {
                telemetryClient = new TelemetryClient(new TelemetryConfiguration(AppInsightsKey));
            }
            challengeTelemetryClient = new TelemetryClient(new TelemetryConfiguration(ChallengeAppInsightsKey));
        }

        static void RegisterOnMessageHandlerAndReceiveMessages()
        {
            // Configure the MessageHandler Options in terms of exception handling, number of concurrent messages to deliver etc.
            var messageHandlerOptions = new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                // Maximum number of Concurrent calls to the callback `ProcessMessagesAsync`, set to 1 for simplicity.
                // Set it according to how many messages the application wants to process in parallel.
                MaxConcurrentCalls = 10,

                // Indicates whether MessagePump should automatically complete the messages after returning from User Callback.
                // False below indicates the Complete will be handled by the User Callback as in `ProcessMessagesAsync` below.
                AutoComplete = false
            };

            // Register the function that will process messages
            queueClient.RegisterMessageHandler(ProcessMessagesAsync, messageHandlerOptions);
        }

        static async Task ProcessMessagesAsync(Message message, CancellationToken token)
        {
            try
            {
                // Process the message
                var body = Encoding.UTF8.GetString(message.Body);
                dynamic obj = Newtonsoft.Json.Linq.JObject.Parse(body);
                var orderId = obj.order.ToString();
                var orderObject = new { OrderId = orderId };
                var orderMessage = Newtonsoft.Json.JsonConvert.SerializeObject(orderObject);

                var eventTelemetry = new EventTelemetry();
                eventTelemetry.Name = $"ServiceBusListener";
                eventTelemetry.Properties.Add("team", TeamName);
                eventTelemetry.Properties.Add("sequence", "3");
                eventTelemetry.Properties.Add("type", "servicebus");
                eventTelemetry.Properties.Add("service", "servicebuslistener");
                eventTelemetry.Properties.Add("orderId", orderId);


                Console.WriteLine($"Order received {orderId}. Attempting to send request to process endpoint: {ProcessEndpoint}");

                var result = await SendRequest(orderMessage);

                if (result)
                {
                    Console.WriteLine($"Sent order to fulfillment {orderId}");
                    eventTelemetry.Properties.Add("status", "sent to fulfillment service");

                    // Complete the message so that it is not received again.
                    // This can be done only if the queueClient is created in ReceiveMode.PeekLock mode (which is default).
                    await queueClient.CompleteAsync(message.SystemProperties.LockToken);
                }
                else
                {
                    Console.WriteLine($"Couldn't send to process endpoint");
                    eventTelemetry.Properties.Add("status", "failed to send to fulfillment service");
                    // This will make the message available again for processing
                    await queueClient.AbandonAsync(message.SystemProperties.LockToken);
                }
                trackEvent(eventTelemetry);
                // Note: Use the cancellationToken passed as necessary to determine if the queueClient has already been closed.
                // If queueClient has already been Closed, you may chose to not call CompleteAsync() or AbandonAsync() etc. calls 
                // to avoid unnecessary exceptions.
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if(telemetryClient!=null) 
                    telemetryClient.TrackException(ex);
            }
        }
        static void trackEvent(EventTelemetry eventTelemetry)
        {
            // Due to a bug in app insights, each telemetry client should send a different event instance otherwise it will be sent to the same app. 
            var eventTelemetryCopy = (EventTelemetry) eventTelemetry.DeepClone();

            if(telemetryClient!=null) 
                    telemetryClient.TrackEvent(eventTelemetry);

            challengeTelemetryClient.TrackEvent(eventTelemetryCopy);

        }
        static Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            Console.WriteLine($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
            Console.WriteLine("Exception context for troubleshooting:");
            Console.WriteLine($"- Endpoint: {context.Endpoint}");
            Console.WriteLine($"- Entity Path: {context.EntityPath}");
            Console.WriteLine($"- Executing Action: {context.Action}");

            var exceptionTelemetry = new ExceptionTelemetry();
            exceptionTelemetry.Exception = exceptionReceivedEventArgs.Exception;
            exceptionTelemetry.Properties.Add("Endpoint", context.Endpoint);
            exceptionTelemetry.Properties.Add("Entity Path", context.EntityPath);
            exceptionTelemetry.Properties.Add("Executing Action", context.Action);

            if(telemetryClient!=null) 
                    telemetryClient.TrackException(exceptionTelemetry);
                    
            return Task.CompletedTask;
        }

        static async Task<bool> SendRequest(string message)
        {
            var result = await httpClient.PostAsync(ProcessEndpoint, new StringContent(message, Encoding.UTF8, "application/json"));
            if(!result.IsSuccessStatusCode)
                Console.WriteLine($"HTTP Request to {ProcessEndpoint} failed: {result.StatusCode} {result.ReasonPhrase} {result.Content}");
            return result.IsSuccessStatusCode;
        }

    }
}
