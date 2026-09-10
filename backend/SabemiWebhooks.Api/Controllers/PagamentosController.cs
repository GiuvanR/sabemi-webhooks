using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SabemiWebhooks.Api.Data;
using SabemiWebhooks.Api.Dtos;

namespace SabemiWebhooks.Api.Controllers;

/// <summary>API de leitura consumida pelo dashboard (não faz parte do fluxo do webhook).</summary>
[ApiController]
[Route("api/pagamentos")]
public class PagamentosController : ControllerBase
{
    private readonly AppDbContext _db;

    public PagamentosController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lista eventos recebidos, mais recentes primeiro, com filtros opcionais
    /// por status de processamento e por id de contrato.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PagamentoDto>>> Listar(
        [FromQuery] string? status,
        [FromQuery] string? idContrato,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 50,
        CancellationToken ct = default)
    {
        var query = _db.EventosLogBruto.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<Models.StatusProcessamento>(status, true, out var statusEnum))
                return BadRequest(new { erro = $"Status inválido: '{status}'. Use Recebido, Processando, Sucesso ou Erro." });

            query = query.Where(e => e.StatusProcessamento == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(idContrato))
            query = query.Where(e => e.IdContrato.Contains(idContrato));

        var total = await query.CountAsync(ct);

        var itens = await query
            .OrderByDescending(e => e.RecebidoEm)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(e => new PagamentoDto
            {
                Id = e.Id,
                IdTransacao = e.IdTransacao,
                IdContrato = e.IdContrato,
                Valor = e.Valor,
                DataPagamento = e.DataPagamento,
                StatusBanco = e.StatusBanco,
                StatusProcessamento = e.StatusProcessamento.ToString(),
                MensagemErro = e.MensagemErro,
                RecebidoEm = e.RecebidoEm,
                ProcessadoEm = e.ProcessadoEm
            })
            .ToListAsync(ct);

        Response.Headers.Append("X-Total-Count", total.ToString());
        return Ok(itens);
    }

    /// <summary>Status atual (projeção) de todos os contratos — útil para uma visão resumida.</summary>
    [HttpGet("contratos")]
    public async Task<IActionResult> ListarContratos(CancellationToken ct)
    {
        var contratos = await _db.StatusContratos
            .AsNoTracking()
            .OrderByDescending(c => c.AtualizadoEm)
            .Select(c => new
            {
                c.IdContrato,
                c.UltimoValorPago,
                c.UltimaDataPagamento,
                c.UltimoIdTransacao,
                StatusAtual = c.StatusAtual.ToString(),
                c.AtualizadoEm
            })
            .ToListAsync(ct);

        return Ok(contratos);
    }
}
