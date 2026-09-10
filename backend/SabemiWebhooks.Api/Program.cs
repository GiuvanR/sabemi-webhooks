using Microsoft.EntityFrameworkCore;

using Microsoft.OpenApi.Models;

using SabemiWebhooks.Api.Data;
using SabemiWebhooks.Api.Middleware;
using SabemiWebhooks.Api.Models;
using SabemiWebhooks.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Serviços ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    // Faz o Swagger mostrar o botão "Authorize" e enviar o header X-Api-Key
    // em toda requisição feita pela UI (necessário para testar /webhooks/*).
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Chave exigida nas rotas /webhooks/*. Valor configurado em appsettings.json (Webhook:ApiKey)."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Fila em memória + worker de background (ver Services/ para detalhes e limitações).
builder.Services.AddSingleton<IPagamentoProcessingQueue, PagamentoProcessingQueue>();
builder.Services.AddHostedService<PagamentoBackgroundService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Dashboard", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? new[] { "http://127.0.0.1:3000" };
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// --- Migração automática + catch-up de eventos que ficaram pendentes ---
// (ex.: o processo caiu depois de gravar o log bruto mas antes de terminar o
// processamento em background — na próxima subida, reenfileiramos.)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var queue = scope.ServiceProvider.GetRequiredService<IPagamentoProcessingQueue>();
    var pendentes = db.EventosLogBruto
        .Where(e => e.StatusProcessamento == StatusProcessamento.Recebido
                 || e.StatusProcessamento == StatusProcessamento.Processando)
        .Select(e => e.Id)
        .ToList();

    foreach (var id in pendentes)
        await queue.EnfileirarAsync(new PagamentoJob(id));

    if (pendentes.Count > 0)
        app.Logger.LogInformation("{Count} evento(s) pendente(s) reenfileirado(s) na inicialização.", pendentes.Count);
}

// --- Pipeline ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Dashboard");
app.UseApiKeyAuth(); // valida X-Api-Key só em /webhooks/*
app.UseAuthorization();
app.MapControllers();

app.Run();
