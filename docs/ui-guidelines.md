# UI Guidelines — PharmazapPlus

Guia de boas práticas para contribuições visuais e de UX no projeto. Seguir estas convenções garante consistência entre componentes e facilita manutenção.

---

## 1. Princípios gerais

- **Tokens, não hardcode** — use sempre variáveis CSS do design system (`var(--pz-*)` ou FluentUI direto)
- **FluentUI primeiro** — use os componentes nativos (`FluentButton`, `FluentTextField`, `FluentCard`, etc.) antes de criar CSS customizado
- **Tema em primeiro lugar** — teste toda mudança visual em tema claro **e** escuro antes de fazer commit
- **Menos é mais** — evite estilos `!important`; estruture o CSS para aproveitar a cascata

---

## 2. Cores

### ✅ Faça
```css
/* Use tokens semânticos */
color: var(--pz-text-primary);
background: var(--pz-surface-card);
border-color: var(--pz-border-subtle);

/* Use tokens de status com fallback */
color: var(--colorStatusSuccessForeground3, #107c10);
```

### ❌ Não faça
```css
/* Hardcode de cor sem fallback */
color: #1e293b;
background: white;
background: #f8f9fa;
```

### Paleta de status
| Estado | Background token | Foreground token |
|---|---|---|
| Sucesso | `--colorStatusSuccessBackground2` | `--colorStatusSuccessForeground3` |
| Aviso | `--colorStatusWarningBackground2` | `--colorStatusWarningForeground3` |
| Erro | `--colorStatusDangerBackground2` | `--colorStatusDangerForeground3` |
| Info | `--colorBrandBackground2` | `--colorBrandForeground2` |

---

## 2.1. Ícones dentro de botões coloridos (Accent)

**Problema**: `FluentIcon` com `Slot="start"` dentro de `FluentButton[Appearance.Accent]` permanece no light DOM e o SVG é renderizado dentro do shadow DOM do `fluent-icon`, impossibilitando que regras CSS externas o estilizem. Isso faz o ícone aparecer quase invisível (preto sobre fundo azul) em ambos os temas.

**Solução**: Sempre adicionar `Color="Color.Fill"` ao `FluentIcon`. Isso instrui o web component a usar `--fill-color` internamente, que mapeia para branco sobre fundo accent.

### ✅ Correto — sempre use `Color="Color.Fill"` em ícones de botões Accent
```razor
<FluentButton Appearance="Appearance.Accent" @onclick="NavigateToMessages">
    <FluentIcon Icon="@(Icons.Regular.Size16.Chat)" Slot="start" Color="Color.Fill" />
    Mensagens
</FluentButton>
```

### ❌ Sem `Color="Color.Fill"` o ícone fica invisível
```razor
<!-- Evite — o ícone fica quase invisível pois o SVG no shadow DOM não herda a cor do botão -->
<FluentIcon Icon="@(Icons.Regular.Size16.Chat)" Slot="start" />
```

### ❌ Não adicione cores hardcoded
```razor
<!-- Evite — hardcode que quebra no tema escuro -->
<FluentIcon Icon="@(Icons.Regular.Size16.Chat)" Slot="start" CustomColor="#ffffff" />
```

### Outros tipos de botão
Para botões com `Appearance.Outline`, `Appearance.Neutral` ou `Appearance.Lightweight`, o ícone herda a cor de texto neutra do documento, o que está correto. Não é necessária nenhuma regra adicional.

## 3. Tipografia

Use os tokens de tipografia do FluentUI ou os aliases `--pz-font-size-*` do design system:

```css
/* ✅ */
font-size: var(--pz-font-size-base);
font-weight: var(--pz-font-weight-semibold);

/* ❌ */
font-size: 14px;
font-weight: 600;
```

Hierarquia visual recomendada:
- Título de página: `--pz-font-size-2xl`, `--pz-font-weight-semibold`
- Título de seção: `--pz-font-size-xl`, `--pz-font-weight-semibold`
- Subtítulo: `--pz-font-size-lg`, `--pz-font-weight-medium`
- Corpo: `--pz-font-size-base`, `--pz-font-weight-regular`
- Secundário: `--pz-font-size-sm`, `--neutral-foreground-hint`

---

## 4. Espaçamento

Use as variáveis de espaçamento para padding, margin e gap:

```css
/* ✅ */
padding: var(--pz-space-16);
gap: var(--pz-space-8);

/* ❌ */
padding: 16px;
gap: 8px;
```

---

## 5. Componentes Blazor

### Criando um novo componente
1. Crie o arquivo `.razor` na pasta correta (`Components/Pages/`, `Components/Chat/`, etc.)
2. Use `@inject` para DI; nunca instancie serviços diretamente
3. Coloque estilos no bloco `<style>` do próprio componente ou em arquivo `.razor.css`
4. Chame `StateHasChanged()` somente quando necessário (eventos externos, callbacks)

### Estilos por componente vs. global
- Estilos **específicos do componente**: use `<style>` no próprio `.razor` ou `.razor.css`
- Estilos **globais / tokens**: use `app.css` ou `tokens.css`
- Não duplique regras — extraia para o global quando o mesmo padrão aparecer em ≥ 3 componentes

---

## 5.1. Campos com máscara de entrada (input masks)

Todo campo com máscara (moeda, telefone, CNPJ, CEP, etc.) deve seguir estas regras:

1. **Sempre ter um `Placeholder`** que demonstre o formato esperado (ex.: `R$ 0,00`, `(11) 99999-9999`, `00.000.000/0001-00`).
2. **Formatação em tempo real** — conforme o usuário digita, o campo deve já exibir o valor formatado. Use `ValueChanged` em vez de `@bind-Value` para acionar o handler de máscara a cada keystroke.
3. **Nunca usar `@bind-Value` puro** em campos com máscara — isso formata apenas no `onchange` (saída do campo), o que causa experiência inconsistente.
4. **Padrão cents-first para moeda BRL**: ao digitar, interpretar os dígitos como centavos (ex.: digitar "1500" exibe `R$ 15,00`). Usar o helper `FormatBrCurrencyInput()` de `StringExtension`.
5. **Mudança global de comportamento**: qualquer campo monetário novo ou ajustado deve seguir este padrão; não manter variações antigas que só formatam no blur ou tratam `15` como `R$ 15,00` durante a digitação.

### ✅ Correto — campo moeda com formatação em tempo real
```razor
<FluentTextField Value="@_valorVendaStr"
                 ValueChanged="@OnValorVendaInput"
                 Placeholder="R$ 0,00"
                 Style="width: 100%;"
                 MaxLength="15" />
```

```csharp
private void OnValorVendaInput(string? value)
{
    _valorVendaStr = value.FormatBrCurrencyInput();
}
```

### ❌ Errado — formata apenas no blur (saída do campo)
```razor
<!-- Evite — o usuário vê "15" até sair do campo; experiência inconsistente -->
<FluentTextField @bind-Value="_valorVendaStr"
                 @onblur="FormatValorVendaOnBlur"
                 Placeholder="R$ 0,00" />
```

### Máscaras padrão do projeto

| Campo | Placeholder | Max dígitos | MaxLength |
|-------|------------|-------------|-----------|
| Moeda BRL | `R$ 0,00` | — | 15 |
| CNPJ | `00.000.000/0001-00` | 14 | 18 |
| Telefone / Celular | `(11) 99999-9999` | 11 | 16 |
| Data | `DD/MM/AAAA` | 8 | 10 |
| CEP | `00000-000` | 8 | 9 |

---

## 6. Tema claro / escuro — checklist

Antes de fazer merge, verifique:

- [ ] Textos têm contraste mínimo WCAG AA (4.5:1 para texto normal, 3:1 para texto grande)
- [ ] Badges e labels são legíveis nos dois temas
- [ ] **`FluentLabel` de texto secundário (ex.: nome da farmácia, subtítulos)** usa `Style="color: var(--neutral-foreground-rest);"` ou `Style="color: var(--neutral-foreground-hint);"` — evite `Color="Color.Lightweight"` pois pode renderizar como azul/accent no tema escuro
- [ ] **Ícones dentro de botões Accent (`Appearance.Accent`) têm `Color="Color.Fill"`** — ver seção 2.1 acima
- [ ] Backgrounds de card/seção mudam com o tema (não estão fixos como `white` ou `#f8f9fa`)
- [ ] Bordas usam `--neutral-stroke-rest` e não cor fixa
- [ ] Sombras usam `var(--pz-shadow-*)` para adaptar corretamente ao tema escuro
- [ ] Estados hover/focus/active visíveis nos dois temas

---

## 7. Acessibilidade básica

- Use `aria-label` em ícones sem texto
- Não confie apenas em cor para transmitir informação (adicione ícone ou texto)
- Mantenha `tabindex` e ordem de foco lógica
- Use `role` e atributos ARIA quando o FluentUI não for suficiente

---

## 8. Performance de renderização

- Evite `StateHasChanged()` em loops ou timers frequentes
- Use `@key` em listas renderizadas dinamicamente
- Prefira componentes `@rendermode InteractiveServer` com cautela em listas grandes
- Implemente paginação em listagens com mais de 50 itens

---

## 9. Nomeação de classes CSS

Use kebab-case e prefixo do componente para evitar conflitos:

```css
/* ✅ */
.clientes-header { }
.chat-message-outbound { }
.atendentes-stat-icon { }

/* ❌ — genérico demais, pode conflitar */
.header { }
.message { }
.icon { }
```

---

## 10. Débito técnico conhecido

| Item | Localização | Prioridade |
|---|---|---|
| Componentes `FluentMenuTrigger`/`FluentMenuList` sem namespace importado | `ChatWindow.razor`, `ImportExport.razor` | Média |
| Variáveis nullable sem tratamento em `ClienteImporter.cs` | `Services/CRM/` | Baixa |
| Bindings inline com `null` literal em `ImportExport.razor` | `Components/CRM/` | Baixa |
| Testes de integração para endpoints WhatsApp | `API.Tests/` | Média |
