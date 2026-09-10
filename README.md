# Sabemi · Webhooks de Pagamento

Serviço que recebe notificações de pagamento (webhooks) de um banco parceiro,
garante idempotência, processa em background e exibe tudo em um painel
administrativo.

## Arquitetura

```  
          banco parceiro
               │  POST /webhooks/pagamento  (header X-Api-Key)
               ▼
┌───────────────────────────────┐
│  WebhookController            │  1. valida ApiKey (middleware)
│                               │  2. checa idempotência (id_transacao)
│                               │  3. grava "eventos_log_bruto" (status=Recebido)
│                               │  4. enfileira job                → 202 Accepted (rápido)
└──────────────┬────────────────┘
               │ Channel<PagamentoJob> (fila em memória)
               ▼
┌───────────────────────────────┐
│  PagamentoBackgroundService   │  BackgroundService que roda a vida toda
│                               │  - Task.Delay(2s) simulando regra pesada
│                               │  - valida regra de negócio
│                               │  - atualiza "eventos_log_bruto" (Sucesso/Erro)
│                               │  - upsert em "status_contrato"
└──────────────┬────────────────┘
               ▼
        PostgreSQL (2 tabelas)
               ▲
               │ GET /api/pagamentos?status=&idContrato=
               │
      Dashboard (Next.js, polling a cada 5s)
```

### Por que essa modelagem de tabelas

- **`eventos_log_bruto`**: log imutável de cada notificação recebida (auditoria +
  chave de idempotência via índice único em `id_transacao`). Nunca é sobrescrita,
  só tem seu `status_processamento` atualizado.
- **`status_contrato`**: projeção "estado atual" por contrato — o que o painel
  precisa para responder rapidamente "como está o contrato X agora", sem
  varrer todo o histórico.

### Idempotência

- Índice único em `eventos_log_bruto.id_transacao`.
- Caminho feliz: `SELECT` antes de inserir — se já existe, responde `200 OK`
  informando que é duplicado, sem reprocessar nada.
- Corrida de concorrência (dois retries do banco chegando ao mesmo tempo): o
  `INSERT` do segundo falha por violação de constraint única; o controller
  detecta esse erro específico (`SqlState 23505` no Postgres) e também responde
  como duplicado — não é tratado como erro genérico 500.

### Resiliência / processamento assíncrono

- O endpoint só grava o log bruto e enfileira um job num `Channel<T>` em
  memória, respondendo `202 Accepted` imediatamente ao banco.
- Um `BackgroundService` (`PagamentoBackgroundService`) consome a fila e faz o
  trabalho pesado (`Task.Delay(2s)` simulando a regra de negócio real).
- **Limitação conhecida**: a fila é em memória, então uma instância única do
  processo. Se o processo cair entre o `INSERT` do log e o fim do
  processamento, o evento fica com `status_processamento = Recebido` ou
  `Processando`. Por isso, na inicialização (`Program.cs`) o serviço varre e
  reenfileira tudo que ficou pendente ("catch-up"). Em produção com múltiplas
  instâncias, o ideal é trocar o `Channel` por um message broker real
  (RabbitMQ / SQS / Azure Service Bus).

### Segurança do webhook

- Middleware `ApiKeyAuthMiddleware` exige o header `X-Api-Key` em qualquer rota
  sob `/webhooks/*`, comparando com o valor configurado em
  `Webhook:ApiKey` (appsettings / variável de ambiente) usando comparação em
  tempo constante. Requisição sem a chave certa recebe `401`.
- Evoluções possíveis (fora do escopo do desafio): assinatura HMAC-SHA256 do
  corpo (`X-Signature`), rotação de chave, IP allowlist.

### Regras de negócio simuladas (para gerar o caminho de erro no painel)

Em `PagamentoBackgroundService.ValidarRegraDeNegocio`:
- `valor <= 0` → erro.
- `status` fora de um conjunto conhecido (`PAGO`, `CONFIRMADO`, `LIQUIDADO`,
  `FALHA`, `CANCELADO`) → erro.
- `status` = `FALHA` ou `CANCELADO` → erro (o banco está avisando que o
  pagamento não se confirmou).

## Como rodar

### 1. Banco de dados

```bash
docker compose up -d
```

Sobe um Postgres em `127.0.0.1:5432` (db `sabemi_webhooks`, usuário/senha
`postgres`/`postgres` — ver `appsettings.json`).

### 2. Backend (.NET 8)

```bash
cd backend/SabemiWebhooks.Api
dotnet restore
dotnet tool install --global dotnet-ef   # se ainda não tiver
dotnet ef migrations add InitialCreate
dotnet run
```

> As migrations não estão pré-geradas neste pacote (dependem da toolchain
> local). `dotnet ef migrations add` cria os arquivos a partir do
> `AppDbContext`; `Program.cs` já chama `db.Database.Migrate()` na
> inicialização, então basta rodar `dotnet run` depois — não precisa de
> `database update` manual.

A API sobe em `http://127.0.0.1:5080` (Swagger em `/swagger`).

Configure a ApiKey em `appsettings.json` → `Webhook:ApiKey` (ou via variável de
ambiente `Webhook__ApiKey`).

### 3. Frontend (Next.js)

```bash
cd frontend
cp .env.local.example .env.local
npm install
npm run dev
```

Abre em `http://127.0.0.1:3000`.

## Testando o webhook manualmente

```bash
curl -i -X POST http://127.0.0.1:5080/webhooks/pagamento \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: TROQUE_ESTA_CHAVE_EM_PRODUCAO" \
  -d '{
    "id_transacao": "TXN-0001",
    "id_contrato": "CT-1029",
    "valor": 452.30,
    "data_pagamento": "2026-09-08T10:00:00Z",
    "status": "PAGO"
  }'
```

Resposta imediata (`202`), o `status_processamento` fica `Recebido` por ~2s e
depois vira `Sucesso`. Repetir a mesma requisição (mesmo `id_transacao`) deve
retornar `200` com "Evento já recebido anteriormente".

Para forçar o caminho de erro (aparece com alerta visual no painel):

```bash
curl -i -X POST http://127.0.0.1:5080/webhooks/pagamento \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: TROQUE_ESTA_CHAVE_EM_PRODUCAO" \
  -d '{
    "id_transacao": "TXN-0002",
    "id_contrato": "CT-1029",
    "valor": -10,
    "data_pagamento": "2026-09-08T10:05:00Z",
    "status": "PAGO"
  }'
```

Sem a `X-Api-Key` correta, a chamada recebe `401 Unauthorized`.

## Estrutura de pastas

```
backend/SabemiWebhooks.Api/
  Controllers/     WebhookController (recepção) e PagamentosController (leitura p/ dashboard)
  Middleware/       ApiKeyAuthMiddleware
  Models/           EventoLogBruto, StatusContrato, StatusProcessamento (enum)
  Dtos/             WebhookPagamentoRequest, PagamentoDto
  Data/             AppDbContext (EF Core)
  Services/         Fila (Channel) + BackgroundService de processamento

frontend/
  app/page.tsx              Dashboard (client component, polling 5s)
  components/Filtros.tsx    Filtro por status + id do contrato
  components/PagamentosTable.tsx  Tabela + alerta visual de erro
  lib/api.ts                 Cliente HTTP para a API
```

## O que eu faria a seguir com mais tempo

- Testes automatizados (xUnit para a regra de idempotência e para o worker;
  Playwright/Cypress para o dashboard).
- Trocar a fila em memória por um broker real para suportar múltiplas
  instâncias do backend.
- SSE/WebSocket no lugar de polling para atualização "de verdade" em tempo
  real no dashboard.
- Assinatura HMAC do payload em vez de ApiKey estática.
- Paginação de verdade na UI (a API já suporta `pagina`/`tamanhoPagina`).
