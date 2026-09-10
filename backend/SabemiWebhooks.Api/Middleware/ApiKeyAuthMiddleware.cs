namespace SabemiWebhooks.Api.Middleware;

/// <summary>
/// Validação simples de segurança para o endpoint de webhook: o banco parceiro
/// deve enviar o header "X-Api-Key" com um valor pré-combinado (configurado em
/// appsettings.json / variável de ambiente / secret manager).
///
/// Em produção real isso poderia evoluir para HMAC-SHA256 sobre o corpo da
/// requisição (header "X-Signature"), mas para o escopo deste desafio uma
/// ApiKey estática já cumpre o requisito de "validação simples".
/// </summary>
public class ApiKeyAuthMiddleware
{
    private const string HeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Só protege o endpoint de webhook; deixa o resto (dashboard API, swagger) passar.
        if (!context.Request.Path.StartsWithSegments("/webhooks"))
        {
            await _next(context);
            return;
        }

        var configuredKey = _configuration["Webhook:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            _logger.LogError("Webhook:ApiKey não configurada no servidor.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { erro = "Configuração de segurança ausente no servidor." });
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            string.IsNullOrWhiteSpace(providedKey) ||
            !SlowEquals(providedKey.ToString(), configuredKey))
        {
            _logger.LogWarning("Requisição de webhook rejeitada: {Header} ausente ou inválido.", HeaderName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { erro = "ApiKey ausente ou inválida." });
            return;
        }

        await _next(context);
    }

    // Comparação em tempo constante para evitar timing attacks triviais.
    private static bool SlowEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }
}

public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app)
        => app.UseMiddleware<ApiKeyAuthMiddleware>();
}
