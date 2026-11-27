# AGENTS.md — PharmazapPlus

## Branch Workflow

- **Sempre trabalhe apenas no branch `master`.**
- Antes de rodar `git commit`, sempre confira `git branch --show-current`.

## Build & Test

```bash
dotnet build src/PharmazapPlus.slnx
dotnet test src/PharmazapPlus.slnx --no-build
```

## ⚠️ Critical: Binary Cleanup Before Running

**ALWAYS clean `bin/` and `obj/` directories before running after code changes, or stale DLLs will execute instead of newly compiled code.**

```bash
# Clean all projects
rm -rf src/PharmazapPlus.API/bin src/PharmazapPlus.API/obj
rm -rf src/PharmazapPlus.Web/bin src/PharmazapPlus.Web/obj
rm -rf src/PharmazapPlus.Worker/bin src/PharmazapPlus.Worker/obj
rm -rf src/PharmazapPlus.Shared/bin src/PharmazapPlus.Shared/obj

# Then rebuild
dotnet build src/PharmazapPlus.slnx
```

**Why this matters:** After committing a bugfix, if you don't clean binaries and just restart the running process, it may load the old DLL from `bin/Release/net11.0/` instead of the newly compiled one, causing the fix to never take effect.

## Regra de estabilidade de portas em producao

- A porta da **Web** deve permanecer estavel em producao. Nao altere a porta publica da Web sem necessidade extrema, porque isso impacta clientes ativos.
- A porta da **API** pode ser alterada quando necessario, desde que a Web e os scripts de deploy/configuracao sejam atualizados para consumir a nova porta internamente.
- Em correcoes urgentes, priorize ajustes na API e na integracao interna; evite usar mudanca de porta da Web como solucao rapida.

## PostgreSQL Backup/Restore

Use os scripts versionados para backup e restore antes de qualquer upgrade de versao major do PostgreSQL.

Scripts:
- `scripts/postgres-backup.sh`
- `scripts/postgres-restore.sh`

Exemplos:

```bash
# Backup completo (roles + databases) via pg_dumpall
./scripts/postgres-backup.sh pharmazap-postgres postgres /tmp/backups

# Restore completo (exige --yes)
./scripts/postgres-restore.sh /tmp/backups/arquivo.sql pharmazap-postgres postgres --yes
```

### Upgrade 15 -> 16 sem perda de dados

Importante: trocar apenas a imagem de `postgres:15` para `postgres:16` no mesmo volume nao funciona sem migracao de dados.

Passo a passo recomendado:

```bash
# 1) Gerar backup completo
./scripts/postgres-backup.sh pharmazap-postgres postgres /tmp/backups

# 2) Parar stack/servicos que usam o banco
docker compose down

# 3) Atualizar imagem para postgres:16-alpine no compose
#    (este repositorio ja esta com postgres:16-alpine)

# 4) Remover volume antigo (somente apos backup validado)
docker volume rm <nome_do_volume_postgres>

# 5) Subir Postgres 16 limpo
docker compose up -d postgres

# 6) Restaurar dump
./scripts/postgres-restore.sh /tmp/backups/arquivo.sql pharmazap-postgres postgres --yes

# 7) Validar versao
docker exec pharmazap-postgres psql -U postgres -d postgres -c "select version();"
```

Observacoes:
- Em ambientes legados, o usuario pode ser diferente (`pharmazap`, por exemplo). Ajuste o segundo/terceiro argumento dos scripts.
- Se houver override por variavel de ambiente (`ConnectionStrings__DefaultConnection`), valide host/porta/usuario apos o restore.

## Solution Structure

```
PharmazapPlus.slnx
├── PharmazapPlus.API          # REST API (port 5051)
├── PharmazapPlus.Web          # Blazor Server + FluentUI (port 5160/7160)
├── PharmazapPlus.Shared      # Shared models
├── PharmazapPlus.Worker      # Background services
└── PharmazapPlus.API.Tests  # xUnit tests
```

## Dev Credentials

| Role | Email | Password |
|------|-------|----------|
| Desenvolvedor | dev@pharmazap.com.br | `Seed:DeveloperPassword` |
| Gestor | gestor@pharmazapplus.com | `Seed:GestorPassword` |
| Administrador | admin@pharmazapplus.com | `Seed:AdminPassword` |
| Atendente | atendente@farmaciateste.com.br | `Seed:AttendantPassword` |

## Required UI Rules (from `.github/copilot-instructions.md`)

These rules must be preserved and followed in any UI changes:

### Phone Mask
- Use `StringExtension.MaskTelefoneInput()` while typing — always with `Immediate="true"` on `FluentTextField`
- Use `StringExtension.SanitizeDigits(11)` before sending to API
- Never send masked format to API
- All mask fields (phone, CPF, CEP, CNPJ) must use `Value` + `ValueChanged` + `Immediate="true"` so the mask applies on every keystroke, not only on blur
- Pattern: `<FluentTextField Value="@_telefone" ValueChanged="@(v => _telefone = v.MaskTelefoneInput())" Immediate="true" .../>`

### Real-time Field Validation
- For formatted fields (email/phone/CPF/CEP), use `Value` + `ValueChanged` + `Immediate="true"` to validate while typing
- Also validate on `@onblur` and show error immediately when leaving the field with invalid value
- Do not rely only on submit-time validation
- Keep submit validation reusing the same field validation methods (or equivalent behavior with FluentValidator)

### Value Formatting (currency)
- `15` (no separator) → `R$ 15,00` (integer, not cents)
- Use `CultureInfo("pt-BR")` for display

### FluentSelect z-index
- Container with select: `position: relative; z-index: 10;`
- Sibling cards below: `z-index: 1`
- Never `overflow: hidden` on dropdown parents

### FluentDataGrid columns
- Use `minmax(Xpx, auto)` — never fixed `1fr 2fr`
- Columns auto-adjust to content size
- Always add `Style="min-width: max-content;"` for horizontal scroll

### State field
- Always `FluentSelect` with 27 Brazilian UFs (AC, AL, AP, AM, BA, CE, DF, ES, GO, MA, MT, MS, MG, PA, PB, PR, PE, PI, RJ, RN, RS, RO, RR, SC, SP, SE, TO)

### Modal duplication trap
- Never render same modal twice on one page — causes focus locking

### Channel registration permission
- Only `Administrador` and `Desenvolvedor` can access `/channels/create`

### WhatsApp não oficial em `/channels/create`
- Não exibir campos de **API Key** e **URL da Evolution** no formulário; essas credenciais devem ser resolvidas no backend.
- O endpoint padrão para não-oficial deve ser interno (`http://localhost:8080`), com fallback por configuração (`WhatsApp:NonOfficialApiUrl`) e validação de URL base.
- O campo de instância deve ser orientado para uso de **somente dígitos** (ex.: `5562998808389`) e permitir fluxo de QR Code antes de salvar.
- O nome da instância salvo deve ser exatamente os dígitos informados (ex.: `5562998808389`), sem prefixo.
- O QR da Evolution deve ser salvo em `wwwroot/uploads/qrcodes` usando o identificador exato da instância como nome (ex.: `5562998808389.png`); uma nova geração substitui o arquivo da mesma instância.
- Para Evolution Go, iniciar a sessão com `POST /instance/connect` antes de solicitar autenticação; o código de pareamento usa `POST /instance/pair` com `{ "phone": "DDIDDDNUMERO" }`.
- Se a instância já existir e estiver conectada (`OPEN`/`CONNECTED`) no momento do cadastro, bloquear o salvamento de novo canal e exibir erro claro ao usuário.
- Na listagem de canais, permitir validar status da Evolution para canais WhatsApp não oficiais (conectado/desconectado/aguardando QR).

### Settings page access
- In `Configuracoes.razor`, keep `Aparência` as the first section on the page
- `Desenvolvedor` and `Atendente` may access `/configuracoes`, but they must see only `Aparência`
- Everything after `Aparência` on that page must remain hidden for `Desenvolvedor` and `Atendente`
- The remaining settings sections stay visible only for `Gestor` and `Administrador`

## Migrations

```bash
cd src/PharmazapPlus.API
dotnet ef migrations add NomeDaMigration
dotnet ef database update
```

## Servicios

Factory methods e criação de instâncias de entidades devem ser feitos via serviços, não métodos estáticos em modelos.

### Padrão
- Criar classes de serviço em `PharmazapPlus.Shared/Services/` (para domínios puros) ou `PharmazapPlus.API/Services/` (para domínios com dependências de infraestrutura)
- Registrar no DI em `Program.cs` com `AddScoped<T, TImpl>()`
- Injetar via construtor nos controllers

### Exemplo
```csharp
// ❌ Evitar: método estático em modelo
var pedido = Pedido.CriarDeAtendimento(atendimento, valor);

// ✅ Usar: serviço registrado no DI
var pedido = _pedidoService.CriarDeAtendimento(atendimento, valor);
```

### Excessões
- Métodos utilitários puros (stateless, sem dependências) podem permanecer estáticos
- Classes de constantes e enums

## Fixed Issues (Regression Prevention)

### EF Core Migrations
- When changing column types (e.g., enum string → int), use raw SQL with `USING` clause for PostgreSQL:
  ```csharp
  migrationBuilder.Sql(@"ALTER TABLE ""Pedidos"" ALTER COLUMN ""TipoPedido"" TYPE integer USING CASE WHEN ""TipoPedido"" = 'Delivery' THEN 0 ...");
  ```
- Never use `AlterColumn<int>()` directly for type changes that require casting
- After creating migration, verify with `dotnet ef database update` before committing

### Launch Configuration
- All paths in `.vscode/launch.json`, `start-dev.sh`, `start-dev.bat` must include `src/` prefix
- In `launch.json` for `coreclr` type:
  - Do NOT use `browserUrl`, `runtimeExecutable`, `runtimeArgs` (not allowed)
  - Use `"console": "integratedTerminal"` (valid values: `internalConsole`, `integratedTerminal`, `externalTerminal`)
  - Remove `/api-docs` from `serverReadyAction.uriFormat` for API profile

### Test Mock Returns
- When mocking methods returning tuples (e.g., `Task<(bool Success, string Message)>`), use:
  ```csharp
  _mockService.Setup(x => x.Method()).ReturnsAsync((true, string.Empty));
  // NOT: .ReturnsAsync(true) — causes CS1929
  ```

### Logo Upload (Web + API)
- Logos are uploaded and served by the **API** (port 5051), not Web
- API must have `app.UseStaticFiles()` in pipeline (serves wwwroot)
- Web `appsettings.Development.json` must point `Application:ApiUrl` to `https://localhost:5051`
- Logo URL returned from API is relative (`/uploads/logos/xxx.png`) and Web prepends ApiUrl

### Phone Number Re-formatting (9th digit doubling)
- `FormatPhoneNumberForSending()` in `WhatsAppService.cs` must NOT blindly add `9` before the local number for 10-digit inputs
- For 10-digit numbers without `55` prefix: check if `digits[2] == '9'` — if so, the number already has the mobile prefix (DDD with 8-digit plan), just prepend `"55"`. Otherwise, add `"55" + digits[..2] + "9" + digits[2..]`
- For 12-digit numbers with `55` prefix: check if `digits[4] == '9'` — if so, return as-is. Otherwise, add the `9`
- Reference test: `FormatPhoneNumberForSending_FormatsCorrectly` in `WhatsAppServiceConfigTests.cs`
- Example: `"6298808389"` (10 digits, DDD 62 with mobile prefix `9`) → `"556298808389"` (NOT `"5562998808389"`)

### WhatsApp Evolution base URL
- For non-official WhatsApp channels, keep `ApiUrl` as the Evolution base URL only; do not append `/webhook` when saving or syncing channel settings.
- The inbound webhook endpoint belongs to Pharmazap API (`/api/whatsapp/webhook` on port `5051`); if Evolution logs keep retrying `:6000`, the instance is still on stale config.

### CSS `::deep` em arquivos `.css` standalone
- `::deep` só funciona dentro de blocos `<style>` em arquivos `.razor` (CSS isolado do componente).
- Em arquivos `.css` avulsos (ex.: `Messages.razor.css`), usar `::deep` não produz escopo — o seletor se comporta como global.
- Sempre que precisar estilizar componentes filhos de um `FluentCard`/`FluentDialog` etc., prefira classes CSS globais sem `::deep` em arquivos `.css` avulsos, ou use `<style>` no `.razor` com `::deep` se o escopo for necessário.

### `application/octet-stream` em `IsAllowedMediaContentType`
- Não inclua `application/octet-stream` na lista de tipos de mídia permitidos em `PharmazapApiService.IsAllowedMediaContentType()`.
- Arquivos salvos com MIME type genérico (`octet-stream`) serão rejeitados no download, forçando o sistema a armazenar o tipo correto no momento do upload/webhook.
- Se um tipo de mídia não for reconhecido, prefira corrigir a origem (ex.: extrair `MediaType` correto do webhook) em vez de aceitar `octet-stream`.

### DatabaseSeeder não pode sobrescrever ChaveWhatsApp
- `DatabaseSeeder.cs` **nunca** deve sobrescrever `FarmaciaIntegracoes.ChaveWhatsApp` com `"farmacia-teste-key"` em registros existentes.
- O seed só deve definir `ChaveWhatsApp` se estiver vazio ou ainda for o seed value (`"farmacia-teste-key"`). Se o usuário já criou um canal WhatsApp Não Oficial, `ChaveWhatsApp` já contém o instance name real (ex.: `"5562998808389"`).
- **Impacto do bug:** A cada restart/deploy, o seeder sobrescrevia `ChaveWhatsApp`, o que quebrava o matching de instância no webhook da Evolution (`GetComparableInstanceNames` vs `ChaveWhatsApp`). Todas as mensagens recebidas após o restart eram descartadas silenciosamente com `"Instância não vinculada a farmácia"`.
- **Correção:** Nos 3 blocos de update (`else` de `ChaveWhatsApp == SeedChaveWhatsApp`, `else` de `FarmaciaId == farmacia.Id`, e `else` final), usar:
  ```csharp
  if (string.IsNullOrWhiteSpace(integracoes.ChaveWhatsApp) || integracoes.ChaveWhatsApp == SeedChaveWhatsApp)
      integracoes.ChaveWhatsApp = SeedChaveWhatsApp;
  ```
- `WhatsAppApiUrl` e `WhatsAppApiKey` continuam sendo sobrescritos (vêm de env vars), o que é seguro.

### Mídia inline da Evolution Go e fallback `/message/downloadimage`
- Quando o webhook da Evolution Go trouxer mídia inline em base64 (`base64` ou `data.base64`, inclusive dentro de `imageMessage`/`videoMessage`/`audioMessage`/`documentMessage`), a API deve persistir o arquivo localmente via `SaveBase64Media()` e reutilizar a URL local resultante.
- Nesses casos, **não** disparar download externo nem criar `HttpClient` para buscar a mesma mídia em CDN/URL remota.
- Se o base64 vier como data URL (`data:image/jpeg;base64,...`), remover o prefixo antes de decodificar/salvar.
- No fallback para `/message/downloadimage`, o payload enviado para a Evolution Go deve permanecer no formato documentado `{"message":{"<mediaWrapper>":{...}}}`.
- Campos como `directPath`, `mediaKey`, `fileSHA256`, `fileEncSHA256` e `mimetype` devem ficar aninhados dentro do wrapper (`imageMessage`, `audioMessage`, etc.), **nunca** na raiz do JSON.

## Key Documentation

- `README.md` — Full system docs
- `.github/copilot-instructions.md` — UI implementation rules (must preserve)
- `TESTES_RESUMO.md` — Test coverage summary

## Instruções completas replicadas de .github/copilot-instructions.md

## Regras de Encerramento de Atendimento

### Permissão para encerrar
- **Somente o atendente ao qual o atendimento está atribuído pode encerrá-lo.**
- A mesma regra de bloqueio de "em andamento por outro atendente" se aplica ao botão "Encerrar Atendimento": se o atendimento estiver atribuído a outro atendente, o botão de encerrar deve estar desabilitado ou oculto para o usuário atual.
- O backend deve validar essa permissão e retornar `403 Forbidden` caso o atendente que tenta encerrar não seja o responsável pelo atendimento.

### Campo monetário / valor — formatação obrigatória

**Regra:** Todo campo de valor monetário da aplicação deve aplicar máscara BRL **em tempo real**, no exato momento da digitação.

**Comportamento esperado:**
- Se o usuário digitar `1` → `R$ 0,01`
- Se depois digitar `0` → `R$ 0,10`
- Se depois digitar `0` novamente → `R$ 1,00`
- Se digitar `1500` → `R$ 15,00`
- O placeholder deve ser `R$ 0,00`.
- O campo nunca deve esperar blur/tab para formatar.

**Mudança de comportamento:** campos monetários devem seguir o padrão **cents-first**. Ou seja, `15` durante a digitação agora representa `R$ 0,15`, e não mais `R$ 15,00`.

**Implementação obrigatória:**
- Usar `Value` + `ValueChanged` + `Immediate="true"` em vez de `@bind-Value` puro.
- Aplicar helper compartilhado para máscara imediata (ex.: `FormatBrCurrencyInput()`), sanitizando para dígitos e interpretando a digitação como centavos.
- Continuar convertendo o valor para `decimal` antes de enviar à API, reutilizando helper centralizado de parse.
- Esta regra vale para **todo campo de valor** do sistema, não apenas para "Valor da venda".

**Exemplo de implementação:**
```csharp
private void OnValorChanged(string? value)
{
    _valorFormatado = value.FormatBrCurrencyInput();
}
```

```razor
<FluentTextField Value="@_valorFormatado"
                 ValueChanged="@OnValorChanged"
                 Immediate="true"
                 Label="Valor"
                 Placeholder="R$ 0,00"
                 Style="width: 100%;" />

@code {
    private string _valorFormatado = "";
}
```

| ❌ ERRADO | ✅ CORRETO |
|-----------|-----------|
| Digitar `15` e o campo exibir `15` até sair do campo | Digitar `15` e o campo exibir `R$ 0,15` imediatamente |
| Usar `@bind-Value` puro e formatar apenas no blur/tab | Usar `Value` + `ValueChanged` + `Immediate="true"` |
| Implementar lógica local diferente em cada tela | Reutilizar helper compartilhado de máscara monetária |
| Enviar string para a API sem converter para `decimal` | Enviar `decimal` corretamente parseado |

## Armadilha Blazor — Modal/Diálogo duplicado na mesma página

- **Nunca renderize o mesmo modal de edição duas vezes** (mesmos parâmetros de `Visible` e entidade), mesmo que um esteja fora do bloco principal.
- Em componentes com `TrapFocus="true"`, modal duplicado pode causar disputa de foco/eventos e deixar campos aparentemente "travados" (ex.: não permite editar Sobrenome, Tipo, etc.).
- Garanta **instância única** de componentes como `ClienteFormDialog` por página.
- Antes de concluir alterações de UI, revisar o `.razor` para blocos duplicados de `<FluentDialog ...>` e `<ClienteFormDialog ...>`.

## Regra de contador de caracteres em tempo real (FluentTextArea)

- Sempre que houver contador de caracteres associado a um `FluentTextArea`, o contador deve atualizar enquanto o usuário digita (sem depender de blur/tab).
- Para isso, usar `Value` + `ValueChanged` com `Immediate="true"` (evitar depender apenas de `@bind-Value` sem atualização imediata).
- O comportamento de referência é o mesmo do cadastro de Campanhas e deve ser replicado em formulários equivalentes, como Mensagem Pré-Definida.

## Regra de máscara e validação para placeholders tipados

- Todo `FluentTextField` com placeholder de formato tipado (ex.: telefone, CPF, CEP, CNPJ, valores numéricos estruturados) deve validar e formatar **em tempo de digitação** (não apenas no blur).
- Não permitir caracteres inválidos para o formato (ex.: telefone não aceita letras; manter apenas dígitos e pontuação de máscara).
- Implementar com `Value` + `ValueChanged` + **`Immediate="true"`** e helper de máscara (evitar depender só de `@bind-Value` sem sanitização).
- O `Immediate="true"` é obrigatório para que o `ValueChanged` dispare a cada tecla digitada, e não apenas ao sair do campo (blur).
- Persistir o valor já sanitizado/formatado antes de enviar à API.

## Regra de validação em tempo real para campos (`FluentTextField`)

- Para campos com validação de formato (ex.: e-mail, telefone, CPF, CEP), usar sempre `Value` + `ValueChanged` + `Immediate="true"` para validar enquanto o usuário digita.
- Além da validação em tempo real, executar validação no `@onblur` para mostrar erro imediatamente ao sair do campo (tab/blur) quando o valor estiver inválido.
- Não depender apenas da validação no submit.
- O submit deve reaproveitar o mesmo método de validação dos campos para evitar divergência.
- Se adotado `FluentValidator` no componente/página, manter este mesmo comportamento: validação em tempo de digitação + validação no blur + bloqueio de envio com valor inválido.

**Padrão recomendado:**
```razor
<FluentTextField Value="@_email"
                 ValueChanged="@OnEmailChanged"
                 @onblur="@OnEmailBlur"
                 Immediate="true"
                 Label="Email *"
                 Placeholder="email@dominio.com" />
```

```csharp
private string _email = "";
private string _emailValidationMessage = "";
private bool _emailInteragido;

private void OnEmailChanged(string? value)
{
    _email = value ?? "";
    ValidateEmail(showRequired: _emailInteragido);
}

private void OnEmailBlur(FocusEventArgs _)
{
    _emailInteragido = true;
    ValidateEmail(showRequired: true);
}
```

## Regra de alinhamento para campos de senha com toggle de visibilidade

- Sempre que um campo de senha exibir botão de mostrar/ocultar, use um wrapper `position: relative` e posicione o botão um pouco abaixo do centro do campo.
- O ícone deve ficar alinhado com a linha visual dos `*******`, não centralizado demais.
- Manter o mesmo padrão visual em todos os campos de password com toggle de visibilidade.
- Exemplo de referência: botão absoluto à direita com `top: calc(50% + 0.75rem)` e `transform: translateY(-50%)`.

## Regra de máscara de Telefone — obrigatória em todos os formulários

**Regra:** Todo campo `Telefone` em qualquer formulário da aplicação (cadastro, edição, filtro) **deve** aplicar a máscara `(XX) XXXXX-XXXX` em tempo real, durante a digitação, e exibir o valor já formatado ao abrir o formulário de edição.

**Helpers disponíveis em `PharmazapPlus.Shared.Extensions.StringExtension`** (namespace já importado globalmente via `_Imports.razor`):

| Helper | Uso |
|--------|-----|
| `v.MaskTelefoneInput()` | Aplica máscara `(XX) XXXXX-XXXX` enquanto digita |
| `v.SanitizeDigits(11)` | Remove a máscara antes de enviar à API (apenas dígitos, máx. 11) |
| `v.FormatTelefone()` | Formata número já salvo (ex.: vindo do banco) para exibição |

**Implementação obrigatória no componente Razor:**

```razor
<FluentTextField Value="@_telefone"
                 ValueChanged="@(v => _telefone = v.MaskTelefoneInput())"
                 Immediate="true"
                 Label="Telefone"
                 Placeholder="(11) 99999-9999"
                 Style="width: 100%;" />
```

```csharp
// OnParametersSet — ao carregar dado existente
_telefone = entidade.Telefone.MaskTelefoneInput();

// HandleSave — ao enviar para a API
Telefone = string.IsNullOrWhiteSpace(_telefone) ? null : _telefone.SanitizeDigits(11),
```

**Formulários onde esta regra deve ser aplicada (lista não exaustiva):**
- `AtendenteFormDialog.razor` ✅ (já implementado)
- `AdministradorFormDialog.razor` ✅ (já implementado)
- `ClienteFormDialog.razor` ✅ (já implementado)
- `FarmaciaFormDialog.razor` ✅ (já implementado)
- `Configuracoes.razor`
- Qualquer novo formulário com campo Telefone

| ❌ ERRADO | ✅ CORRETO |
|-----------|-----------|
| `@bind-Value="@_telefone"` sem máscara | `Value="@_telefone" ValueChanged="@(v => _telefone = v.MaskTelefoneInput())" Immediate="true"` |
| `ValueChanged` sem `Immediate="true"` | Sempre usar `Immediate="true"` para máscara em tempo real |
| Exibir `27998803214` no campo | Exibir `(27) 99880-3214` no campo |
| Enviar `(27) 99880-3214` para a API | Enviar `27998803214` (dígitos limpos via `SanitizeDigits(11)`) |
| Carregar dado do banco sem formatar | Chamar `.MaskTelefoneInput()` em `OnParametersSet` |

## Regra de z-index para FluentSelect / dropdowns em Blazor (FluentUI)

**Problema recorrente:** O popup/dropdown do `FluentSelect` abre **atrás** de componentes posicionados abaixo no DOM, tornando as opções invisíveis/inacessíveis.

**Causa:** O `FluentSelect` renderiza o popup como overlay, mas se o card pai não tiver `position: relative` + `z-index` elevado, e os cards irmãos abaixo tiverem `z-index` igual ou maior, o dropdown fica oculto.

**Regra obrigatória:** Sempre que houver um `FluentSelect` (ou qualquer dropdown) dentro de um `FluentCard` ou container:
1. O container que contém o `FluentSelect` deve ter `position: relative; z-index: 10; overflow: visible;` — tanto no atributo `Style` inline quanto na classe CSS correspondente.
2. Os containers irmãos/seguintes no DOM devem ter `position: relative; z-index: 1;` (z-index menor), para garantir a sobreposição correta.

**Exemplo correto (padrão obrigatório):**
```razor
<FluentCard Style="position: relative; z-index: 10; overflow: visible;">
    <FluentSelect TOption="string" @bind-Value="@Filtro" />
</FluentCard>

<FluentCard Style="position: relative; z-index: 1;">
    ...
</FluentCard>
```

**Referência de implementação correta:** `Atendentes.razor` linha 38:
```razor
<FluentCard Style="padding: 1.5rem; width: 100%; position: relative; z-index: 10;">
```

❌ **NUNCA** deixe um card com `FluentSelect` sem `position: relative` e `z-index` explícito.  
❌ **NUNCA** use `overflow: hidden` em containers pai de dropdowns.  
✅ **SEMPRE** aplique z-index hierárquico: card de filtros = 10, cards de conteúdo abaixo = 1.

## Regra de best-fit para colunas FluentDataGrid

- **Nunca use `GridTemplateColumns` com frações fixas** (ex: `1fr 2fr`) que causem truncamento de cabeçalhos.
- **SEMPRE use `minmax(Xpx, auto)`** — as colunas crescem automaticamente conforme o conteúdo.
- O `Style` da `FluentDataGrid` deve incluir `min-width: max-content;` para permitir scroll horizontal quando necessário.
- Os títulos das colunas NUNCA devem aparecer truncados com "..." — se isso ocorrer, ajustar os valores mínimos.
- Exemplo correto: `GridTemplateColumns="minmax(100px,auto) minmax(90px,auto) minmax(150px,auto) minmax(80px,auto) minmax(70px,auto) minmax(80px,auto)"`
- Para grids com pocas colunas grandes: `GridTemplateColumns="minmax(120px,auto) minmax(150px,auto) minmax(100px,auto) minmax(100px,auto)"`

## Regra de campo "Estado" — sempre FluentSelect com 27 UFs brasileiras

- **Nunca use `FluentTextField` para campo de Estado/UF.** Sempre use `FluentSelect` com as 27 unidades federativas brasileiras como opções.
- Lista de UFs: AC, AL, AP, AM, BA, CE, DF, ES, GO, MA, MT, MS, MG, PA, PB, PR, PE, PI, RJ, RN, RS, RO, RR, SC, SP, SE, TO.
- O `FluentSelect` de Estado deve ter `position: relative; z-index: 30; overflow: visible;` para garantir que o dropdown apareça visível, especialmente dentro de modais/diálogos.
- Em formulários de filtro, incluir opção vazia "Todos os estados" como primeira opção.
- Em formulários de cadastro/edição, incluir opção vazia "Selecione" como primeira opção.
- Esta regra se aplica a TODOS os formulários do sistema (Clientes, Farmácias, Endereços, etc.).

## Regra de z-index para FluentSelect dentro de FluentDialog (modal)

- Dentro de modais (`FluentDialog`), o `FluentDialogBody` tem overflow que pode cortar dropdowns.
- Sempre que houver `FluentSelect` dentro de um `FluentDialogBody`, o container pai (ex: `FluentStack`) do select deve ter `Style="overflow: visible; position: relative; z-index: 30;"`.
- O próprio `FluentSelect` deve ter `Style="... position: relative; z-index: 30; overflow: visible;"`.

## Regra de permissão para Cadastro de Canais (`/channels/create`)

- O acesso ao cadastro de canais deve ser restrito a **Administrador** e **Desenvolvedor**.
- **Gestor** e **Atendente** não devem visualizar nem acessar a rota de cadastro de canais.
- A regra deve ser aplicada em três níveis:
  1. Página Web (`[Authorize(Roles = "...")]`)
  2. Endpoint API correspondente (`[Authorize(Roles = "...")]`)
  3. Visibilidade do item no menu lateral (por `AuthorizeView` com roles equivalentes)

## Regra de propriedades de navegação virtual (required virtual)

- **NUNCA use `required` em propriedades de navegação virtual.**
- Sempre use `virtual X? Y` (anulável) em vez de `required virtual X Y`.
- **Por quê?** `System.Text.Json` exige propriedades `required` no JSON, mas a API não serializa nav properties (são carregadas separadamente via Include). Isso causa `JsonRequiredPropertyMissing` no cliente Web.
- **Referência correta:** `Campanha.cs` linha 13-14 usa `[JsonIgnore]` + `virtual Farmacia? Farmacia`.
- **Modelos afetados:** Cliente, Atendimento, MotivoEncerramento, Etiqueta — todos com nav properties devem ser `virtual X? Y` (anulável, sem `required`).

## Regra anti-duplicidade de cliente por identificador de canal

- Em mensagens inbound, o sistema deve sempre fazer **upsert** de cliente por identificador único do canal antes de criar atendimento.
- Para **WhatsApp**, comparar e persistir apenas dígitos, removendo prefixo `55` quando presente, ignorando máscara/pontuação e aplicando equivalência pelos **últimos 8 dígitos** do número normalizado.
- Exemplo: `+55 (11) 91234-5678` e `+55 (31) 91234-5678` devem ser tratados como o mesmo cliente (chave `12345678`).
- Para **Instagram**, usar o identificador do remetente (IGSID) como chave única por farmácia, sem criar duplicados.
- Se o cliente já existir, **não criar novo**: apenas reutilizar e vincular o atendimento ao `ClienteId` existente.

## Regra de reaproveitamento de atendimento ativo no inbound

- Após o upsert/reuso de cliente, mensagens inbound do WhatsApp (webhook e mock) devem **reaproveitar atendimento ativo** (`StatusEnum != Finalizado`) antes de tentar criar novo atendimento.
- O match deve priorizar `ClienteId`, com fallback por `WhatsAppId` normalizado quando necessário, para evitar duplicidade por variações de número.
- A regra vale para atendimento em `Pendente` e também `EmAndamento`.

## Regra de mensagens de grupo WhatsApp

- Mensagens inbound de grupo WhatsApp (`remoteJid`/identificador terminando em `@g.us`) **não criam Cliente** e não entram no fluxo operacional de atendimento.
- Devem aparecer apenas na aba/lista de **Grupos** em `Messages.razor`, como visualização.
- Conversas de grupo não podem ser assumidas, transferidas, respondidas ou encerradas como atendimento.
- No `/tests`, a flag de mock de WhatsApp para grupo deve simular o mesmo comportamento do webhook Evolution Go: persistir a conversa/mensagem para visualização, sem criar cliente individual.

## Regra de mensagens de canal/newsletter WhatsApp

- Mensagens inbound de canal/newsletter WhatsApp (`remoteJid`/identificador terminando em `@newsletter`) **devem ser descartadas** e não podem criar `Cliente`, `Atendimento` ou `Mensagem`.
- Se a Evolution expuser o identificador do canal sem sufixo (ex.: `120363401648797715`), ele continua sendo tratado como newsletter/canal e também deve ser descartado.
- Esses eventos não entram no fluxo operacional de atendimento e não devem aparecer na UI como conversa pendente.

## Regra de nome de contato (username) no webhook oficial da Meta WhatsApp Cloud API

- A Meta passou a expor `value.contacts[].profile.name` no payload oficial do webhook (`whatsapp_business_account`), refletindo inclusive o nome de usuário (`username`/handle) que o contato configurou no app do WhatsApp.
- `WhatsAppService.ProcessIncomingMessage` deve extrair esse nome via `ExtractMetaContactName(value, fromRaw)`, casando o `wa_id` do contato com o número remetente (`from`) da mensagem, e repassá-lo para `GetOrCreateCliente(farmaciaId, whatsappId, contactName)`.
- `GetOrCreateCliente` usa o nome de contato para:
  - Preencher `Cliente.Nome` na criação de um cliente novo (em vez do placeholder `"Cliente WhatsApp {id}"`).
  - Atualizar `Cliente.Nome` de um cliente existente somente quando o nome atual for vazio ou ainda for o placeholder padrão (`"Cliente WhatsApp ..."`) — nunca sobrescrever um nome já editado manualmente pelo atendente.
- Esse comportamento espelha o que já existe no fluxo Evolution Go (`ExtractEvolutionGoContactName` / `pushName`), unificando a captura de nome entre a integração oficial (Meta Cloud API) e a não oficial (Evolution Go).
- Ao alterar a extração de nome de contato em qualquer um dos dois fluxos, mantenha os testes de `WhatsAppServiceConfigTests` (`ProcessWebhookPayload_UsesMetaContactProfileName_ForNewCliente`, `ProcessWebhookPayload_UpdatesPlaceholderName_WithMetaContactProfileName`) e `WhatsAppControllerTests` (`ProcessWebhook_WithEvolutionGoPayload_UsesContactNameWhenAvailable`) atualizados.
