# PharmazapPlus Web

Interface Blazor Server (Fluent UI) da plataforma de atendimento.

## Executar

```bash
dotnet run --project src/PharmazapPlus.Web
```

Portas padrão de desenvolvimento: `5160` / `7160`.

## Configuração

No `appsettings.Development.json` (local, ignorado pelo git):

```json
{
  "Application": {
    "ApiUrl": "https://localhost:5051",
    "SignalRUrl": "https://localhost:5051/hubs/pharma"
  }
}
```

## Perfis e telas

- **Gestor** — farmácias, usuários, relatórios e configurações globais.
- **Administrador** — equipe, clientes, canais, campanhas e relatórios da unidade.
- **Atendente** — fila de mensagens, clientes e mensagens pré-definidas.
- **Desenvolvedor** — validações técnicas e diagnóstico.

## Estrutura

```text
Components/
├── Account/      # login e autenticação
├── Layout/       # layout e navegação
└── Pages/        # páginas da aplicação
Services/         # clientes HTTP e serviços de UI
Hubs/             # integração SignalR
```

## Observações

- O acesso à API é feito pelo `HttpClient` configurado em `Program.cs`.
- Em desenvolvimento, o Web expõe proxies para logos e mídias armazenadas na API.
- Regras de UI e máscaras estão documentadas em `AGENTS.md`.
