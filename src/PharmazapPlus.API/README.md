# PharmazapPlus API

API REST em ASP.NET Core responsável por autenticação, atendimentos, CRM, campanhas, canais e integrações.

## Executar

```bash
dotnet run --project src/PharmazapPlus.API
```

Porta padrão de desenvolvimento: `5051`.

## Configuração

Valores sensíveis vêm de variáveis de ambiente ou de arquivos locais ignorados pelo git.

| Variável | Uso |
|---|---|
| `ConnectionStrings__DefaultConnection` | Conexão com PostgreSQL |
| `ConnectionStrings__Redis` | Conexão com Redis |
| `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience` | Autenticação JWT |
| `WhatsApp__AccessToken` / `WhatsApp__PhoneNumberId` | WhatsApp oficial |
| `WhatsApp__WebhookVerifyToken` | Verificação do webhook |
| `PHARMAZAP_ENCRYPTION_KEY` | Criptografia de campos sensíveis |
| `Seed__DeveloperPassword` etc. | Senha dos usuários de seed |

Para desenvolvimento, copie `appsettings.Development.example.json` para `appsettings.Development.json` e preencha os valores localmente.

## Swagger

Em desenvolvimento: `https://localhost:5051/api-docs`.

## Postman

Importe `docs/PharmazapPlus.postman_collection.json` e configure no environment:

- `baseUrl` — URL da API
- `email` / `password` — credenciais do usuário de teste

## Principais grupos de endpoints

- `/api/account` — login, sessão e dados da farmácia
- `/api/atendimentos` — fila, assumir, transferir e encerrar
- `/api/clientes` — CRM e importação/exportação
- `/api/campanhas` — campanhas e mensagens pré-definidas
- `/api/channels` — canais oficiais e não oficiais
- `/api/relatorios` — indicadores e exportações
- `/api/admin` — farmácias, usuários e atendentes

## Migrations

```bash
cd src/PharmazapPlus.API
dotnet ef migrations add NomeDaMigration
dotnet ef database update
```
