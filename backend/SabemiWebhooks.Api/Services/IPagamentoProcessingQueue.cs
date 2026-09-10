namespace SabemiWebhooks.Api.Services;

/// <summary>Um "job" enfileirado para processamento assíncrono após o webhook responder.</summary>
public record PagamentoJob(long EventoLogBrutoId);

public interface IPagamentoProcessingQueue
{
    /// <summary>Enfileira um evento para ser processado pelo worker em background.</summary>
    ValueTask EnfileirarAsync(PagamentoJob job, CancellationToken ct = default);

    /// <summary>Usado pelo worker para retirar o próximo job da fila (bloqueia até haver um).</summary>
    IAsyncEnumerable<PagamentoJob> LerTodosAsync(CancellationToken ct);
}
