using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Banking.Infrastructure.Messaging;

// Süreç boyunca tek AMQP bağlantısı cache'lenir; lazy bağlanma broker kapalıyken de açılışa izin verir
internal sealed class RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not { IsOpen: true })
            {
                var factory = new ConnectionFactory
                {
                    HostName = options.Value.HostName,
                    Port = options.Value.Port,
                    UserName = options.Value.UserName,
                    Password = options.Value.Password,
                    VirtualHost = options.Value.VirtualHost,
                };

                if (options.Value.UseTls)
                {
                    factory.Ssl = new SslOption { Enabled = true, ServerName = options.Value.HostName };
                }

                _connection = await factory.CreateConnectionAsync(cancellationToken);
            }

            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
