# 🎭 WhatsApp Mock — Guia de Uso

Este documento explica como utilizar o serviço mock do WhatsApp para desenvolvimento e testes, sem precisar de credenciais reais da Meta/WhatsApp Business.

---

## O que é o Mock?

O `MockWhatsAppService` é uma implementação simulada do serviço WhatsApp. Ele **não envia mensagens reais** — apenas registra as chamadas no log do console e retorna respostas simuladas com sucesso. É ideal para:

- Desenvolvimento local
- Testes automatizados
- Demos sem conta Meta

---

## Como ativar o Mock

O mock continua sendo ativado pela configuração `WhatsApp:UseRealApi`, mas as **credenciais de produção** agora ficam por farmácia na tabela **`FarmaciaIntegracoes`** (relação 1:1 com `Farmacia`).

No arquivo `appsettings.Development.json`, o valor padrão já é `false`, ou seja, **o mock está ativo por padrão no ambiente de desenvolvimento**.

```json
// appsettings.Development.json
"WhatsApp": {
  "UseRealApi": false,
  "DevelopmentMode": true
}
```

Para usar API real em produção, altere para `true`. As credenciais oficiais (Meta/Instagram) devem ser lidas da `FarmaciaIntegracoes`:

```json
// appsettings.json (configuração global)
"WhatsApp": {
  "UseRealApi": true,
  "DevelopmentMode": false
}
```

---

## 🧩 Nova arquitetura de integrações

Com a arquitetura atual:

- **Credenciais por farmácia no banco**: ficam em `FarmaciaIntegracoes` (`WhatsAppPhoneNumberId`, `WhatsAppAccessToken`, `WhatsAppBusinessAccountId`, `ChaveWhatsApp`, `InstagramAccountId`, `PageId`, `InstagramAccessToken`).
- **Configuração global em `appsettings`**: usada para comportamento de ambiente/mock (`UseRealApi`, `DevelopmentMode`, `MockMessages`, etc.).
- **Configuração de produção via tela admin (futuro)**: gerenciamento centralizado de canais por farmácia.

---

## 🌐 Cenários de integração de canais

| # | Canal | Tipo | Descrição | Credenciais |
|---|-------|------|-----------|-------------|
| 1 | WhatsApp | Não-oficial | Integração sem API oficial da Meta (Evolution/Baileys). Permite até 4 atendentes no mesmo número, sem conta Meta. | URL da API, API Key |
| 2 | WhatsApp | Oficial (Meta) | Integração via WhatsApp Business API oficial da Meta. Requer conta Meta Business verificada. | `PhoneNumberId`, `AccessToken`, `BusinessAccountId` |
| 3 | Instagram | Direct | Recebimento e resposta de Direct Messages via Instagram Graph API. | `InstagramAccountId`, `PageId`, `InstagramAccessToken` |

---

## URLs disponíveis

### Desenvolvimento local (sem Docker)

| Serviço      | URL                               |
|--------------|-----------------------------------|
| API          | `https://localhost:5051`          |
| Web (Blazor) | `https://localhost:8080`          |
| Swagger UI   | `https://localhost:5051/api-docs` |

### Com Docker Compose

| Serviço      | URL                              |
|--------------|----------------------------------|
| API          | `http://localhost:5000`          |
| Web (Blazor) | `http://localhost:8080`          |
| Swagger UI   | `http://localhost:5000/api-docs` |

> **Dependências locais:** Os endpoints que persistem dados no banco (simular mensagem, listar atendimentos) requerem PostgreSQL e Redis em execução.
> Com Docker Compose (`docker-compose up`), tudo sobe automaticamente.

---

## Testando via Swagger UI

1. Acesse preferencialmente `https://localhost:5051/api-docs` (ou `http://localhost:5000/api-docs` com Docker).
2. Utilize os endpoints do grupo **WhatsApp** para testar as funcionalidades.
3. Os endpoints de **webhook** não requerem autenticação (`AllowAnonymous`).
4. Os endpoints de **envio de mensagem** requerem um JWT com role `Administrador` ou `Atendente`.

---

## Fluxo completo: cliente envia mensagem → atendente responde

O diagrama abaixo ilustra o ciclo completo testável no ambiente mock:

```
[Cliente externo]                    [Atendente logado]
      │                                      │
      │ 1. POST /api/whatsapp/mock/mensagem  │
      │──────────────────────────────────►   │
      │    (cria Atendimento "Pendente"       │
      │     + Mensagem Inbound no banco)      │
      │                                      │
      │                   2. GET /api/atendimentos?status=Pendente
      │                      ◄──────────────────────────────────│
      │                      (vê o atendimento recém-criado)     │
      │                                      │
      │                   3. POST /api/atendimentos/{id}/assumir │
      │                      ◄──────────────────────────────────│
      │                      (status muda para "EmAndamento")    │
      │                                      │
      │                   4. GET /api/atendimentos/{id}/mensagens│
      │                      ◄──────────────────────────────────│
      │                      (lê as mensagens do atendimento)    │
      │                                      │
      │                   5. POST /api/whatsapp/send/text        │
      │  ◄────────────────────────────────────────────────────── │
      │    (resposta do atendente, simulada no mock)             │
```

---

### Passo 1 — Simular mensagem de um cliente (sem autenticação)

Disponível apenas quando `WhatsApp:DevelopmentMode = true`.

```
POST /api/whatsapp/mock/mensagem
Content-Type: application/json
```

**Corpo:**
```json
{
  "from": "5562998808389",
  "farmaciaId": 1,
  "text": "Olá, preciso renovar minha receita de metformina"
}
```

**Resposta:**
```json
{
  "success": true,
  "atendimentoId": 42,
  "mensagemId": "a1b2c3d4-...",
  "novoAtendimento": true,
  "status": "Pendente"
}
```

> Se o número `from` já tiver um atendimento `Pendente` aberto para a mesma farmácia, a mensagem é adicionada a ele (sem criar novo atendimento).

---

### Passo 2 — Listar atendimentos pendentes (requer JWT)

```
GET /api/atendimentos?status=Pendente
Authorization: Bearer <seu_token_jwt>
```

**Resposta:**
```json
{
  "success": true,
  "total": 1,
  "data": [
    {
      "id": 42,
      "status": "Pendente",
      "whatsAppId": "5562998808389",
      "canal": "WhatsApp",
      "dataInicio": "2024-02-05T10:00:00Z",
      "clienteNome": null,
      "atendenteNome": null,
      "totalMensagens": 1,
      "ultimaMensagem": "Olá, preciso renovar minha receita de metformina",
      "ultimaMensagemEm": "2024-02-05T10:00:00Z"
    }
  ]
}
```

Para listar todos os atendimentos (qualquer status), omita o parâmetro `status`:
```
GET /api/atendimentos
```

---

### Passo 3 — Atendente assume o atendimento (requer JWT com role `Atendente` ou `Administrador`)

```
POST /api/atendimentos/42/assumir
Authorization: Bearer <seu_token_jwt>
```

**Resposta:**
```json
{
  "success": true,
  "atendimentoId": 42,
  "status": "EmAndamento",
  "atendenteId": 7
}
```

---

### Passo 4 — Ler mensagens do atendimento (requer JWT)

```
GET /api/atendimentos/42/mensagens
Authorization: Bearer <seu_token_jwt>
```

**Resposta:**
```json
{
  "success": true,
  "atendimentoId": 42,
  "status": "EmAndamento",
  "whatsAppId": "5562998808389",
  "data": [
    {
      "id": "a1b2c3d4-...",
      "messageId": "mock-...",
      "direcao": "Inbound",
      "tipo": "Text",
      "status": "Recebida",
      "text": "Olá, preciso renovar minha receita de metformina",
      "mediaUrl": null,
      "receivedAt": "2024-02-05T10:00:00Z"
    }
  ]
}
```

---

### Passo 5 — Atendente responde (requer JWT)

```
POST /api/whatsapp/send/text
Authorization: Bearer <seu_token_jwt>
Content-Type: application/json
```

**Corpo:**
```json
{
  "to": "5562998808389",
  "text": "Olá! Já identificamos sua receita. Em breve entraremos em contato."
}
```

No mock, a resposta é apenas simulada (nenhuma mensagem real é enviada). O log da API mostrará:
```
📱 MOCK WhatsApp: Sending text message
   Farmácia: 1
   Para: 5562998808389
   Mensagem: Olá! Já identificamos sua receita...
   ✅ Success simulated
```

---

## Outros endpoints de webhook (compatíveis com Meta)

### Verificação de Webhook (GET — sem autenticação)

```
GET /api/whatsapp/webhook?hub_mode=subscribe&hub_verify_token=pharmazap_dev_verify_token_2024&hub_challenge=1234567890
```

**Resposta:**
```
HTTP 200 OK
Content-Type: text/plain

1234567890
```

---

## 🎭 Instagram Mock (desenvolvimento)

Além do mock do WhatsApp, o sistema também possui endpoint de simulação para Instagram Direct:

```http
POST /api/instagram/mock/mensagem
Content-Type: application/json
```

Disponível quando `Instagram:DevelopmentMode = true`.

---

### Receber Mensagem via Webhook padrão Meta (POST — sem autenticação)

Simula o payload que a Meta envia ao webhook. Não persiste no banco (apenas processa via `MockWhatsAppService`).

```
POST /api/whatsapp/webhook
Content-Type: application/json
```

**Corpo:**
```json
{
  "object": "whatsapp_business_account",
  "entry": [
    {
      "id": "123456789",
      "changes": [
        {
          "value": {
            "messaging_product": "whatsapp",
            "metadata": { "phone_number_id": "987654321" },
            "messages": [
              {
                "from": "5562998808389",
                "id": "wamid.teste123",
                "timestamp": "1706975400",
                "text": { "body": "Olá, preciso de ajuda" },
                "type": "text"
              }
            ]
          },
          "field": "messages"
        }
      ]
    }
  ]
}
```

---

## Como obter o token JWT para os testes

1. Faça login na interface web em `https://localhost:8080`.
2. Abra as ferramentas de desenvolvedor do navegador (F12) → aba **Application** → **Local Storage** ou **Session Storage**.
3. Copie o valor do token JWT armazenado.
4. Nas chamadas à API (Postman, cURL, etc.), envie o token no cabeçalho HTTP:

   ```http
   Authorization: Bearer SEU_TOKEN_JWT_AQUI
   ```

Alternativamente, use a collection do Postman disponível em `PharmazapPlus.API/docs/PharmazapPlus.postman_collection.json`:

1. Importe a collection no Postman.
2. Configure a variável `baseUrl` como `https://localhost:5051`.
3. Faça login e cole o token JWT na variável `token`.
4. Execute as requisições dos grupos **WhatsApp** e **Atendimentos**.

---

## Número de telefone para testes

Qualquer número no formato internacional pode ser usado no mock (ex: `+55 62998808389`).  
O número de exemplo padrão no projeto é `+55 62998808389`.

---

## Resumo rápido

| Objetivo                                | Endpoint                                                    | Auth       |
|-----------------------------------------|-------------------------------------------------------------|------------|
| Ativar o mock                           | `"UseRealApi": false` em `appsettings.Development.json` (credenciais de produção em `FarmaciaIntegracoes`) | —          |
| Acessar a documentação interativa       | `https://localhost:5051/api-docs`                           | —          |
| **Simular cliente enviando mensagem**   | `POST /api/whatsapp/mock/mensagem`                          | Nenhuma    |
| **Listar atendimentos pendentes**       | `GET /api/atendimentos?status=Pendente`                     | JWT        |
| **Assumir um atendimento**              | `POST /api/atendimentos/{id}/assumir`                       | JWT        |
| **Ler mensagens de um atendimento**     | `GET /api/atendimentos/{id}/mensagens`                      | JWT        |
| Atendente responder ao cliente          | `POST /api/whatsapp/send/text`                              | JWT        |
| Testar webhook sem autenticação         | `GET /api/whatsapp/webhook?hub_mode=subscribe&...`          | Nenhuma    |
| Simular Direct do Instagram (mock)      | `POST /api/instagram/mock/mensagem`                         | Nenhuma    |
| Ver log das mensagens mock              | Console/log da API (emojis 📱 ✅ 🎭)                        | —          |
