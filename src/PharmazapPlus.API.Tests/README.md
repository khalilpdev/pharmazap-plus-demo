# PharmazapPlus.API.Tests

Projeto de testes automatizados da API do PharmazapPlus.

## Estrutura atual

```text
PharmazapPlus.API.Tests/
├── Controllers/
│   ├── AccountControllerTests.cs
│   ├── AtendentesControllerTests.cs
│   ├── AtendimentosControllerTests.cs
│   ├── ClientesControllerTests.cs
│   ├── DtoContractTests.cs
│   ├── EtiquetasControllerTests.cs
│   ├── MultiTenantAuthorizationTests.cs
│   ├── RelatoriosControllerTests.cs
│   ├── UsuariosControllerTests.cs
│   └── WhatsAppControllerTests.cs
├── Services/
│   ├── CrmServiceTests.cs
│   ├── CryptoServiceTests.cs
│   ├── MockWhatsAppServiceTests.cs
│   ├── ServerMonitorNotificationServiceTests.cs
│   └── WhatsAppServiceConfigTests.cs
├── BackgroundServices/
│   ├── MessageQueueProcessorTests.cs
│   └── ServerHealthMonitorServiceTests.cs
├── Filters/
│   └── BearerAuthOperationFilterTests.cs
└── README.md
```

## Tecnologias

- xUnit
- Moq
- FluentAssertions
- EF Core InMemory

## Como executar

> Na raiz do repositório:

### Executar todos os testes do projeto de API

```bash
dotnet test PharmazapPlus.API.Tests/PharmazapPlus.API.Tests.csproj
```

### Executar validação da solução completa

```bash
dotnet test PharmazapPlus.slnx --no-build
```

### Executar uma classe de testes específica

```bash
dotnet test PharmazapPlus.API.Tests/PharmazapPlus.API.Tests.csproj --filter "FullyQualifiedName~RelatoriosControllerTests"
```

### Executar um teste específico

```bash
dotnet test PharmazapPlus.API.Tests/PharmazapPlus.API.Tests.csproj --filter "FullyQualifiedName~Finalizar_WithValidData_ReturnsOk"
```

## Cobertura atual (high level)

A suíte cobre os principais fluxos da API, incluindo novos endpoints e cenários de autorização multi-tenant:

- **AccountController**
  - login, userinfo, dashboard stats e atualização de tema (`PUT /api/account/theme`)
- **AtendimentosController**
  - listagem, mensagens, assumir, transferir, finalizar (`POST /api/atendimentos/{id}/finalizar`) e etiqueta (`PUT /api/atendimentos/{id}/etiqueta`)
- **AtendentesController**
  - CRUD, ativação e reset de senha (`PUT /api/atendentes/{id}/reset-senha`)
- **RelatoriosController**
  - relatórios `gestor`, `administrador`, `atendente` e exportação `GET /api/relatorios/exportar`
- **ClientesController**, **EtiquetasController**, **UsuariosController** e **WhatsAppController**
- **Services**, **BackgroundServices** e **Swagger filter** (`BearerAuthOperationFilter`)
- Cobertura atualizada para arquitetura de integrações 1:1 por farmácia via **`FarmaciaIntegracoes`** (em substituição aos campos de credencial diretos em `Farmacia`)
  - validação da relação 1:1 `Farmacia` ↔ `FarmaciaIntegracoes`
  - leitura de credenciais de canais por farmácia (WhatsApp e Instagram)

Status atual validado localmente:

- ✅ Executar `dotnet test PharmazapPlus.API.Tests/PharmazapPlus.API.Tests.csproj` para obter o total de testes da branch atual.

## Padrão de escrita de testes

- Nomenclatura: `Metodo_Cenario_ResultadoEsperado`
- Estrutura: **Arrange / Act / Assert**
- Testes independentes e focados
- Uso de claims (`FarmaciaId`, `Role`, `NameIdentifier`) para simular contexto autenticado

## Ao adicionar novos endpoints

1. Criar/atualizar testes do controller correspondente
2. Cobrir cenários felizes + validação + autorização
3. Atualizar:
   - `PharmazapPlus.API/docs/PharmazapPlus.postman_collection.json` (quando aplicável)
   - documentação da API (`PharmazapPlus.API/README.md`) quando houver mudança funcional relevante
4. Executar os testes antes de abrir PR

## Referências

- [xUnit](https://xunit.net/)
- [Moq](https://github.com/moq/moq4)
- [FluentAssertions](https://fluentassertions.com/)
- [ASP.NET Core Testing](https://learn.microsoft.com/aspnet/core/test/)
