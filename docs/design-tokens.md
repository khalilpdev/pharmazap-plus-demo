# Design Tokens — PharmazapPlus

Centraliza todos os tokens visuais do sistema (cores, tipografia, espaçamento e elevação) para garantir consistência visual e suporte a tema claro/escuro.

## Onde estão os tokens

Arquivo principal: `PharmazapPlus.Web/wwwroot/tokens.css`

Importado automaticamente no `app.css`:
```css
@import 'tokens.css';
```

---

## Tokens de cor semântica

| Token | Referência FluentUI | Uso |
|---|---|---|
| `--pz-surface-bg` | `--neutral-fill-rest` | Fundo de páginas |
| `--pz-surface-card` | `--neutral-fill-layer-rest` | Fundo de cards |
| `--pz-surface-overlay` | `--neutral-fill-secondary-rest` | Seções secundárias, inputs de dev |
| `--pz-text-primary` | `--neutral-foreground-rest` | Texto principal |
| `--pz-text-secondary` | `--neutral-foreground-hint` | Texto secundário, labels |
| `--pz-text-disabled` | `--neutral-foreground-disabled` | Texto desabilitado |
| `--pz-accent` | `--accent-fill-rest` | Botões primários, destaques |
| `--pz-accent-hover` | `--accent-fill-hover` | Estado hover do acento |
| `--pz-accent-foreground` | `--accent-foreground-rest` | Texto/ícone sobre acento |
| `--pz-success` | `--colorStatusSuccessBackground3` | Background de sucesso |
| `--pz-success-foreground` | `--colorStatusSuccessForeground3` | Texto de sucesso |
| `--pz-warning` | `--colorStatusWarningBackground3` | Background de aviso |
| `--pz-warning-foreground` | `--colorStatusWarningForeground3` | Texto de aviso |
| `--pz-error` | `--colorStatusDangerBackground3` | Background de erro |
| `--pz-error-foreground` | `--colorStatusDangerForeground3` | Texto de erro |
| `--pz-border-subtle` | `--neutral-stroke-rest` | Bordas sutis |
| `--pz-border-divider` | `--neutral-stroke-divider-rest` | Divisores de tabela/seção |

### Como usar no CSS
```css
/* ✅ Correto — usa token */
.meu-titulo {
    color: var(--pz-text-primary);
    background: var(--pz-surface-card);
}

/* ❌ Errado — hardcode sem fallback */
.meu-titulo {
    color: #1e293b;
    background: white;
}
```

---

## Tipografia

| Token | Tamanho | Uso recomendado |
|---|---|---|
| `--pz-font-size-xs` | ~10px | Metadados, timestamps |
| `--pz-font-size-sm` | ~12px | Labels, badges, notas |
| `--pz-font-size-base` | ~14px | Corpo de texto padrão |
| `--pz-font-size-md` | ~16px | Texto de destaque |
| `--pz-font-size-lg` | ~20px | Subtítulos |
| `--pz-font-size-xl` | ~24px | Títulos de seção |
| `--pz-font-size-2xl` | ~28px | Títulos de página |

| Token de peso | Valor |
|---|---|
| `--pz-font-weight-regular` | 400 |
| `--pz-font-weight-medium` | 500 |
| `--pz-font-weight-semibold` | 600 |
| `--pz-font-weight-bold` | 700 |

---

## Espaçamento

Os tokens de espaçamento mapeiam para os tokens FluentUI quando disponíveis:

| Token | Tamanho | Equivalente |
|---|---|---|
| `--pz-space-2` | 2px | `--spacingVerticalXXS` |
| `--pz-space-4` | 4px | `--spacingVerticalXS` |
| `--pz-space-8` | 8px | `--spacingVerticalS` |
| `--pz-space-12` | 12px | `--spacingVerticalM` |
| `--pz-space-16` | 16px | `--spacingVerticalL` |
| `--pz-space-24` | 24px | `--spacingVerticalXXL` |
| `--pz-space-32` | 32px | — |

---

## Border Radius

| Token | Valor | Uso |
|---|---|---|
| `--pz-radius-sm` | 2px | Tags, badges menores |
| `--pz-radius-md` | 4px | Inputs, botões |
| `--pz-radius-lg` | 8px | Cards, modais |
| `--pz-radius-xl` | 12px | Cards destacados |
| `--pz-radius-full` | 9999px | Chips, avatares |

---

## Sombras (Elevação)

| Token | Nível | Uso |
|---|---|---|
| `--pz-shadow-2` | 2 | Cards sutis, itens de lista |
| `--pz-shadow-4` | 4 | Cards padrão |
| `--pz-shadow-8` | 8 | Dropdowns, popovers |
| `--pz-shadow-16` | 16 | Modais, painéis |
| `--pz-shadow-28` | 28 | Diálogos principais |
| `--pz-shadow-64` | 64 | Overlays de tela cheia |

---

## Transições

| Token | Duração | Uso |
|---|---|---|
| `--pz-duration-fast` | 100ms | Hover, focus imediato |
| `--pz-duration-normal` | 200ms | Transições padrão |
| `--pz-duration-slow` | 300ms | Animações de entrada |

---

## Tema claro / escuro

Todos os tokens `--pz-*` herdam automaticamente do FluentUI, que gerencia o tema. O componente `FluentDesignTheme` em `MainLayout.razor` controla o tema ativo.

**Não use `prefers-color-scheme`** — o FluentUI já gerencia isso via JavaScript.

### Verificação de compatibilidade de tema
Antes de subir qualquer mudança visual, teste em ambos os temas:
1. Acesse as Configurações → Tema → Claro/Escuro
2. Verifique contraste de texto em fundos claros e escuros
3. Badges, labels e elementos coloridos são os mais propensos a falhar no escuro

---

## Z-index

| Token | Valor | Uso |
|---|---|---|
| `--pz-z-base` | 0 | Conteúdo padrão |
| `--pz-z-raised` | 10 | Elementos levantados na mesma camada |
| `--pz-z-overlay` | 100 | Overlays, drawers |
| `--pz-z-modal` | 200 | Diálogos e modais |
| `--pz-z-toast` | 300 | Notificações toast |
