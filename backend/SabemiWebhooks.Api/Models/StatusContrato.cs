namespace SabemiWebhooks.Api.Models;

/// <summary>
/// Estado "atual" (projeção) de cada contrato, derivado do processamento dos eventos.
/// Uma linha por contrato — é a tabela que o painel administrativo consulta
/// para saber "qual a situação do contrato X agora".
/// </summary>
public class StatusContrato
{
    public string IdContrato { get; set; } = default!; // chave primária

    public decimal UltimoValorPago { get; set; }

    public DateTime UltimaDataPagamento { get; set; }

    public string UltimoIdTransacao { get; set; } = default!;

    public StatusProcessamento StatusAtual { get; set; }

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
