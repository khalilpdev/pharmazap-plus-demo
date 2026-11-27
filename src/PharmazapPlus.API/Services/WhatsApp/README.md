# WhatsApp Service - Mock vs Real API

Este diretório contém a implementação dos serviços WhatsApp para o PharmazapPlus.

## 📱 Serviços Disponíveis

### 1. **WhatsAppService** (API Real da Meta)
Implementação completa que se conecta à API oficial do WhatsApp Business da Meta.

**Quando usar:**
- Produção
- Quando você tem credenciais válidas da Meta
- Para enviar mensagens reais aos clientes

**Requisitos:**
- Conta WhatsApp Business verificada
- `PhoneNumberId` válido
- `AccessToken` da Meta
- `VerifyToken` configurado

### 2. **MockWhatsAppService** (Simulação para Desenvolvimento)
Implementação mock que simula todas as funcionalidades do WhatsApp sem fazer chamadas reais à API.

**Quando usar:**
- Desenvolvimento local
- Testes
- Demos
- Quando você NÃO tem credenciais da Meta
- CI/CD pipelines

**Vantagens:**
- ✅ Não requer conta WhatsApp Business
- ✅ Não requer tokens da Meta
- ✅ Funciona offline
- ✅ Logs detalhados no console
- ✅ Respostas instantâneas
- ✅ Não consome quota da API da Meta

## 🔧 Como Alternar Entre Mock e API Real

### Opção 1: Via `appsettings.json` ou `appsettings.Development.json`

**Para usar o Mock (Desenvolvimento):**
```json
{
  "WhatsApp": {
    "UseRealApi": false,
    "PhoneNumberId": "mock-phone-id",
    "AccessToken": "mock-access-token",
    "VerifyToken": "mock-verify-token"
  }
}
```

**Para usar a API Real (Produção):**
```json
{
  "WhatsApp": {
    "UseRealApi": true,
    "PhoneNumberId": "SEU_PHONE_NUMBER_ID_REAL",
    "AccessToken": "SEU_ACCESS_TOKEN_REAL",
    "VerifyToken": "SEU_VERIFY_TOKEN_REAL",
    "ApiVersion": "v18.0",
    "ApiBaseUrl": "https://graph.facebook.com"
  }
}
```

> **`ApiVersion`** é opcional — padrão `v18.0`. Altere para atualizar a versão da API Meta sem redeploy.  
> **`ApiBaseUrl`** é a URL base sem versão (`https://graph.facebook.com`). A versão é composta automaticamente pelo serviço usando `VersionedApiBaseUrl = $"{ApiBaseUrl}/{ApiVersion}"`.

### Opção 2: Via Variável de Ambiente

```bash
export WhatsApp__UseRealApi=false  # Para Mock
export WhatsApp__UseRealApi=true   # Para API Real
```

## 📝 Logs do Mock Service

Quando o MockWhatsAppService está ativo, você verá logs como:

```
🎭 MOCK WhatsApp Service initialized
⚠️  This is a MOCK service - no real messages will be sent!

📱 MOCK WhatsApp: Sending text message
   Farmácia: 1
   Para: +55 62998808389
   Mensagem: Olá! Sua receita está pronta
   MessageId: 550e8400-e29b-41d4-a716-446655440000
   ✅ Success simulated

📷 MOCK WhatsApp: Sending media message
   Farmácia: 1
   Para: +55 62998808389
   Media URL: https://example.com/image.jpg
   Media Type: image
   Caption: Imagem de exemplo
   MessageId: 660f9511-f39c-52e5-b827-557766551111
   ✅ Success simulated
```

## 🧪 Testando o Mock Service

### 1. Via Swagger UI

1. Acesse `https://localhost:5051/api-docs`
2. Clique em **"Authorize"** (🔒) no topo e insira o token JWT
3. Navegue até os endpoints `/api/whatsapp/send/*`
4. Execute requisições de teste
5. Veja os logs no console da aplicação

> Os endpoints de webhook (`GET /api/whatsapp/webhook` e `POST /api/whatsapp/webhook`) são `[AllowAnonymous]` e não exibem o cadeado na Swagger UI.

### 2. Via Postman/cURL

```bash
# Enviar mensagem de texto
curl -X POST https://localhost:5051/api/whatsapp/send/text \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "to": "+5562998808389",
    "text": "Teste de mensagem mock"
  }'
```

### 3. Via Testes Unitários

```bash
dotnet test --filter "FullyQualifiedName~MockWhatsAppServiceTests"
```

## 📊 Diferenças entre Mock e Real

| Característica | MockWhatsAppService | WhatsAppService |
|---------------|---------------------|-----------------|
| Envia mensagens reais | ❌ Não | ✅ Sim |
| Requer credenciais Meta | ❌ Não | ✅ Sim |
| Consome quota API | ❌ Não | ✅ Sim |
| Funciona offline | ✅ Sim | ❌ Não |
| Logs detalhados | ✅ Sim | ✅ Sim |
| Retorna sucesso | ✅ Sempre | ⚠️ Depende da API |
| Armazena histórico | ✅ Em memória | ✅ No banco |
| Performance | ⚡ Instantâneo | 🌐 Depende da rede |

## 🎯 Número de Teste Padrão

Para facilitar testes e desenvolvimento, use o número:

**+55 62998808389**

Este número aparece nos logs do mock e pode ser usado em exemplos.

## 🔍 Depuração

### Ver mensagens enviadas pelo Mock

O MockWhatsAppService mantém uma lista interna de mensagens enviadas:

```csharp
var mockService = serviceProvider.GetService<IWhatsAppService>() as MockWhatsAppService;
if (mockService != null)
{
    var messages = mockService.GetSentMessages();
    foreach (var msg in messages)
    {
        Console.WriteLine($"[{msg.Timestamp}] {msg.Type} para {msg.To}: {msg.Text}");
    }
}
```

## ⚠️ Importante para Produção

Antes de fazer deploy para produção:

1. ✅ Configure `UseRealApi: true`
2. ✅ Adicione credenciais reais da Meta
3. ✅ Teste com números reais (mas poucos)
4. ✅ Configure webhooks corretamente
5. ✅ Monitore logs de erro

## 📚 Referências

- [WhatsApp Business API Documentation](https://developers.facebook.com/docs/whatsapp)
- [Meta Graph API](https://developers.facebook.com/docs/graph-api)
- [WhatsApp Cloud API](https://developers.facebook.com/docs/whatsapp/cloud-api)

## 🤝 Contribuindo

Ao adicionar novos métodos à interface `IWhatsAppService`:

1. Implemente no `WhatsAppService.cs` (API real)
2. Implemente no `MockWhatsAppService.cs` (mock)
3. Adicione testes em `MockWhatsAppServiceTests.cs`
4. Atualize este README

---

**Desenvolvido para PharmazapPlus** 💊💬
