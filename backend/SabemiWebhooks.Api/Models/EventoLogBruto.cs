namespace SabemiWebhooks.Api.Models;

/// <summary>
/// Registro imutável de CADA notificação recebida do banco, exatamente como chegou.
/// Serve como fonte de verdade / auditoria e é a base da idempotência
/// (id_transacao tem índice único).
/// </summary>
public class EventoLogBruto
{
    public long Id { get; set; }

    /// <summary>Chave de idempotência. Único no banco de dados.</summary>
    public string IdTransacao { get; set; } = default!;

    public string IdContrato { get; set; } = default!;

    public decimal Valor { get; set; }

    public DateTime DataPagamento { get; set; }

    /// <summary>Status conforme enviado pelo banco parceiro no payload original (string crua).</summary>
    public string StatusBanco { get; set; } = default!;

    /// <summary>Payload JSON bruto recebido, para auditoria/replay.</summary>
    public string PayloadJson { get; set; } = default!;

    /// <summary>Data/hora em que o webhook chegou no nosso servidor.</summary>
    public DateTime RecebidoEm { get; set; } = DateTime.UtcNow;

    /// <summary>Data/hora em que o processamento em background terminou (sucesso ou erro).</summary>
    public DateTime? ProcessadoEm { get; set; }

    /// <summary>Status do NOSSO processamento (Recebido/Processando/Sucesso/Erro).</summary>
    public StatusProcessamento StatusProcessamento { get; set; } = StatusProcessamento.Recebido;

    /// <summary>Mensagem de erro, preenchida somente se StatusProcessamento == Erro.</summary>
    public string? MensagemErro { get; set; }
}
