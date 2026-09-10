using Microsoft.EntityFrameworkCore;
using SabemiWebhooks.Api.Data;
using SabemiWebhooks.Api.Models;

namespace SabemiWebhooks.Api.Services;

/// <summary>
/// Worker que roda durante toda a vida da aplicação, consumindo a fila de jobs
/// e executando a regra de negócio "pesada" (simulada com Delay de 2s), fora
/// da thread que respondeu ao webhook.
/// </summary>
public class PagamentoBackgroundService : BackgroundService
{
    private readonly IPagamentoProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PagamentoBackgroundService> _logger;

    public PagamentoBackgroundService(
        IPagamentoProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<PagamentoBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PagamentoBackgroundService iniciado.");

        await foreach (var job in _queue.LerTodosAsync(stoppingToken))
        {
            try
            {
                await ProcessarAsync(job.EventoLogBrutoId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha inesperada processando EventoLogBrutoId={Id}", job.EventoLogBrutoId);
            }
        }
    }

    private async Task ProcessarAsync(long eventoId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var evento = await db.EventosLogBruto.FirstOrDefaultAsync(e => e.Id == eventoId, ct);
        if (evento is null)
        {
            _logger.LogWarning("EventoLogBrutoId={Id} não encontrado para processar.", eventoId);
            return;
        }

        // Evita reprocessar algo que outro worker/instância já concluiu.
        if (evento.StatusProcessamento is StatusProcessamento.Sucesso or StatusProcessamento.Erro)
            return;

        evento.StatusProcessamento = StatusProcessamento.Processando;
        await db.SaveChangesAsync(ct);

        // --- Simulação da regra de negócio pesada (ex: chamada a outro sistema,
        // baixa de título, cálculo atuarial, etc.) ---
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        try
        {
            ValidarRegraDeNegocio(evento);

            // Upsert da projeção "status do contrato".
            var statusContrato = await db.StatusContratos.FirstOrDefaultAsync(s => s.IdContrato == evento.IdContrato, ct);
            if (statusContrato is null)
            {
                statusContrato = new StatusContrato { IdContrato = evento.IdContrato };
                db.StatusContratos.Add(statusContrato);
            }

            // Só atualiza a projeção se este evento for mais recente que o último processado.
            if (statusContrato.UltimaDataPagamento == default || evento.DataPagamento >= statusContrato.UltimaDataPagamento)
            {
                statusContrato.UltimoValorPago = evento.Valor;
                statusContrato.UltimaDataPagamento = evento.DataPagamento;
                statusContrato.UltimoIdTransacao = evento.IdTransacao;
                statusContrato.StatusAtual = StatusProcessamento.Sucesso;
                statusContrato.AtualizadoEm = DateTime.UtcNow;
            }

            evento.StatusProcessamento = StatusProcessamento.Sucesso;
            evento.MensagemErro = null;
        }
        catch (RegraDeNegocioException rex)
        {
            evento.StatusProcessamento = StatusProcessamento.Erro;
            evento.MensagemErro = rex.Message;
            _logger.LogWarning("Evento {IdTransacao} falhou na regra de negócio: {Erro}", evento.IdTransacao, rex.Message);
        }
        finally
        {
            evento.ProcessadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Regras de negócio mínimas para demonstrar o caminho de erro no dashboard.
    /// Num cenário real aqui entrariam as regras da Sabemi (validar contrato existente,
    /// valor esperado da parcela, contrato ativo, etc.).
    /// </summary>
    private static void ValidarRegraDeNegocio(EventoLogBruto evento)
    {
        if (evento.Valor <= 0)
            throw new RegraDeNegocioException("Valor do pagamento deve ser maior que zero.");

        var statusValidos = new[] { "PAGO", "CONFIRMADO", "LIQUIDADO", "FALHA", "CANCELADO" };
        if (!statusValidos.Contains(evento.StatusBanco.ToUpperInvariant()))
            throw new RegraDeNegocioException($"Status '{evento.StatusBanco}' não reconhecido.");

        if (evento.StatusBanco.Equals("FALHA", StringComparison.OrdinalIgnoreCase) ||
            evento.StatusBanco.Equals("CANCELADO", StringComparison.OrdinalIgnoreCase))
            throw new RegraDeNegocioException($"Banco reportou pagamento com status '{evento.StatusBanco}'.");
    }
}

public class RegraDeNegocioException : Exception
{
    public RegraDeNegocioException(string message) : base(message) { }
}
