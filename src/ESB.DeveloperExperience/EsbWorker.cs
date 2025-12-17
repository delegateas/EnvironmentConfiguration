using System.Diagnostics.Metrics;
using Azure.Messaging.ServiceBus;
using BH.DIS.SDK;
using Delegateas.DeveloperExperience.Core;
using Microsoft.Extensions.Logging;

namespace ESB.DeveloperExperience;

public class EsbWorker(
    ILogger<EsbWorker> logger,
    ISubscriberClient subscriberClient,
    ServiceBusClient client,
    EnvironmentConfiguration environmentConfiguration
) : IEsbWorker
{
    private static readonly Meter Meter = new("Core.Subscriber", "1.0.0");

    private static readonly Counter<long> ProcessedEventCounter = Meter.CreateCounter<long>(
        "events_processed",
        description: "Total number of processed events");

    public async Task ProcessSession(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Attempting to accept next session for endpoint {ApplicationName}Endpoint",
                environmentConfiguration.Name);

            var receiver = await client.AcceptNextSessionAsync(
                $"{environmentConfiguration.Name}Endpoint",
                $"{environmentConfiguration.Name}Endpoint",
                cancellationToken: cancellationToken);

            if (receiver == null)
            {
                logger.LogWarning("No session receiver obtained. Waiting before retry...");
                return;
            }

            try
            {
                var message = await receiver.ReceiveMessageAsync(
                    TimeSpan.FromSeconds(10),
                    cancellationToken: cancellationToken);

                if (message == null)
                {
                    logger.LogDebug("No more messages in session");
                }

                await subscriberClient.Handle(message, receiver);

                ProcessedEventCounter.Add(1, KeyValuePair.Create<string, object?>("endpoint", $"{environmentConfiguration.Name}Endpoint"));
            }
            finally
            {
                await receiver.CloseAsync(cancellationToken: cancellationToken);
            }
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
        {
            logger.LogInformation(ex, "Service Bus session timeout - this is expected");
        }
        catch (ServiceBusException ex)
        {
            logger.LogError(ex, "Service Bus error occurred");
        }
    }
}
