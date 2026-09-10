namespace SabemiWebhooks.Api.Models;

/// <summary>
/// Status do PROCESSAMENTO interno do evento (log bruto).
/// Não confundir com o "status" enviado pelo banco no payload (ex: "PAGO", "FALHA"),
/// que é armazenado como string em StatusPagamentoBanco.
/// </summary>
public enum StatusProcessamento
{
    Recebido = 0,   // chegou, ainda na fila para processar
    Processando = 1,
    Sucesso = 2,
    Erro = 3        // falhou na validação ou na regra de negócio
}
