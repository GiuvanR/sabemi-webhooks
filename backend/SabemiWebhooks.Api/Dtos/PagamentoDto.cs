namespace SabemiWebhooks.Api.Dtos;

/// <summary>Formato usado para listar eventos no dashboard.</summary>
public class PagamentoDto
{
    public long Id { get; set; }
    public string IdTransacao { get; set; } = default!;
    public string IdContrato { get; set; } = default!;
    public decimal Valor { get; set; }
    public DateTime DataPagamento { get; set; }
    public string StatusBanco { get; set; } = default!;
    public string StatusProcessamento { get; set; } = default!;
    public string? MensagemErro { get; set; }
    public DateTime RecebidoEm { get; set; }
    public DateTime? ProcessadoEm { get; set; }
}
