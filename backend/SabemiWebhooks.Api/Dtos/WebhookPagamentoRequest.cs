using System.Text.Json.Serialization;

namespace SabemiWebhooks.Api.Dtos;

/// <summary>
/// Corpo esperado do POST /webhooks/pagamento, enviado pelo banco parceiro.
/// </summary>
public class WebhookPagamentoRequest
{
    [JsonPropertyName("id_transacao")]
    public string IdTransacao { get; set; } = default!;

    [JsonPropertyName("id_contrato")]
    public string IdContrato { get; set; } = default!;

    [JsonPropertyName("valor")]
    public decimal Valor { get; set; }

    [JsonPropertyName("data_pagamento")]
    public DateTime DataPagamento { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = default!;
}
