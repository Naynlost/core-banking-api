using System.Text;
using Banking.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Shouldly;

namespace Banking.Api.IntegrationTests.Messaging;

// Sürekli başarısız (poison) mesaj sonsuz döngüye girmez: tek requeue'dan sonra dead-letter'a düşer
[Collection(IntegrationCollection.Name)]
public sealed class DeadLetterTests(IntegrationInfrastructure infrastructure)
{
    [Fact]
    public async Task PoisonMessage_EndsUpInTheDeadLetterQueue()
    {
        await using var provider = await IntegrationTestServices.CreateProviderAsync(infrastructure);
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        foreach (var service in hostedServices)
        {
            await service.StartAsync(CancellationToken.None);
        }

        try
        {
            var connections = provider.GetRequiredService<RabbitMqConnectionProvider>();
            var connection = await connections.GetConnectionAsync(CancellationToken.None);
            await using var channel = await connection.CreateChannelAsync();
            await MessageTopology.DeclareExchangeAsync(channel, CancellationToken.None);

            // MoneyTransferred gibi görünür ama payload deserialize edilemez, her consumer her denemede başarısız olur
            var messageId = Guid.NewGuid();
            await channel.BasicPublishAsync(
                MessageTopology.Exchange,
                MessageTopology.MoneyTransferredRoutingKey,
                mandatory: false,
                basicProperties: new BasicProperties
                {
                    MessageId = messageId.ToString(),
                    Type = "MoneyTransferred",
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                },
                body: Encoding.UTF8.GetBytes("this is not json"));

            BasicGetResult? deadLetter = null;
            await TestBank.WaitUntilAsync(
                async () =>
                {
                    var delivery = await channel.BasicGetAsync(MessageTopology.DeadLetterQueue, autoAck: true);
                    if (delivery is null || delivery.BasicProperties.MessageId != messageId.ToString())
                    {
                        return false;
                    }

                    deadLetter = delivery;
                    return true;
                },
                "the poison message to arrive in the dead-letter queue");

            deadLetter.ShouldNotBeNull();
        }
        finally
        {
            foreach (var service in hostedServices)
            {
                await service.StopAsync(CancellationToken.None);
            }
        }
    }
}
