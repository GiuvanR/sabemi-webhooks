using System.Threading.Channels;

namespace SabemiWebhooks.Api.Services;

/// <summary>
/// Fila em memória (Channel) que desacopla o endpoint HTTP (que precisa responder
/// rápido ao banco) do processamento pesado da regra de negócio, que roda em um
/// BackgroundService separado. Isso é o que garante a "resiliência" pedida:
/// o endpoint só grava o log bruto e enfileira, sem esperar os 2s de processamento.
///
/// Observação de produção: para múltiplas instâncias do serviço (escala horizontal)
/// isso deveria ser um message broker real (RabbitMQ, Azure Service Bus, SQS etc.),
/// já que um Channel in-memory se perde se o processo cair antes de drenar a fila.
/// Para mitigar isso aqui, há um "catch-up" na inicialização que reprocessa qualquer
/// evento que ficou com StatusProcessamento = Recebido/Processando (ver Program.cs).
/// </summary>
public class PagamentoProcessingQueue : IPagamentoProcessingQueue
{
    private readonly Channel<PagamentoJob> _channel;

    public PagamentoProcessingQueue()
    {
        var options = new BoundedChannelOptions(capacity: 1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<PagamentoJob>(options);
    }

    public async ValueTask EnfileirarAsync(PagamentoJob job, CancellationToken ct = default)
        => await _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<PagamentoJob> LerTodosAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
