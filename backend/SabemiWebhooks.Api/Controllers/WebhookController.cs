using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SabemiWebhooks.Api.Data;
using SabemiWebhooks.Api.Dtos;
using SabemiWebhooks.Api.Models;
using SabemiWebhooks.Api.Services;

namespace SabemiWebhooks.Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhookController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPagamentoProcessingQueue _queue;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(AppDbContext db, IPagamentoProcessingQueue queue, ILogger<WebhookController> logger)
    {
        _db = db;
        _queue = queue;
        _logger = logger;
    }

    /// <summary>
    /// Recebe a notificação de pagamento do banco parceiro.
    /// Responde rápido (202) e delega o processamento pesado para o background.
    /// A ApiKey já foi validada pelo ApiKeyAuthMiddleware antes de chegar aqui.
    /// </summary>
    [HttpPost("pagamento")]
    [Consumes("application/json")]
    public async Task<IActionResult> ReceberPagamento([FromBody] WebhookPagamentoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdTransacao) || string.IsNullOrWhiteSpace(request.IdContrato))
            return BadRequest(new { erro = "id_transacao e id_contrato são obrigatórios." });

        // --- Idempotência ---
        // Verifica primeiro por leitura simples (caminho rápido / comum: retry do banco).
        var jaExiste = await _db.EventosLogBruto
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdTransacao == request.IdTransacao, ct);

        if (jaExiste is not null)
        {
            _logger.LogInformation("Webhook duplicado ignorado: id_transacao={IdTransacao}", request.IdTransacao);
            return Ok(new
            {
                mensagem = "Evento já recebido anteriormente. Nenhuma ação adicional foi tomada.",
                id_transacao = request.IdTransacao,
                status_processamento = jaExiste.StatusProcessamento.ToString()
            });
        }

        var evento = new EventoLogBruto
        {
            IdTransacao = request.IdTransacao,
            IdContrato = request.IdContrato,
            Valor = request.Valor,
            DataPagamento = request.DataPagamento,
            StatusBanco = request.Status ?? string.Empty,
            PayloadJson = JsonSerializer.Serialize(request),
            RecebidoEm = DateTime.UtcNow,
            StatusProcessamento = StatusProcessamento.Recebido
        };

        _db.EventosLogBruto.Add(evento);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (EhViolacaoDeChaveUnica(ex))
        {
            // Corrida entre duas requisições simultâneas com o mesmo id_transacao:
            // a constraint UNIQUE do banco garante que só uma sobrevive; a outra
            // cai aqui e é tratada como duplicata (mesmo comportamento do caminho normal).
            _logger.LogInformation("Corrida de concorrência detectada para id_transacao={IdTransacao}, tratando como duplicado.", request.IdTransacao);
            return Ok(new { mensagem = "Evento já recebido (concorrência).", id_transacao = request.IdTransacao });
        }

        // Enfileira para processamento pesado em background; NÃO aguarda o resultado.
        await _queue.EnfileirarAsync(new PagamentoJob(evento.Id), ct);

        // Responde rápido ao banco parceiro — o processamento real acontece depois.
        return Accepted(new
        {
            mensagem = "Webhook recebido e enfileirado para processamento.",
            id_transacao = evento.IdTransacao,
            evento_id = evento.Id
        });
    }

    /// <summary>
    /// Detecta se a DbUpdateException foi causada especificamente por violação da
    /// constraint única de id_transacao (e não por outro erro de banco).
    /// Cobre Npgsql (SqlState 23505) e SQL Server (Number 2601/2627).
    /// </summary>
    private static bool EhViolacaoDeChaveUnica(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null) return false;

        var sqlState = inner.GetType().GetProperty("SqlState")?.GetValue(inner)?.ToString();
        if (sqlState == "23505") return true; // Npgsql / PostgreSQL

        var number = inner.GetType().GetProperty("Number")?.GetValue(inner);
        if (number is int n && (n == 2601 || n == 2627)) return true; // SQL Server

        return false;
    }
}
