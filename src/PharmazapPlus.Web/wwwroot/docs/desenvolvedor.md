# PharmazapPlus 💊 — Guia do Desenvolvedor

**Plataforma de Atendimento Integrado para Farmácias via WhatsApp e Instagram**

Este guia é destinado ao perfil **Desenvolvedor** — acesso técnico avançado para suporte, validações e configuração de integrações.

---

## 🎯 Escopo do Perfil Desenvolvedor

- ✅ Acesso técnico amplo à plataforma
- ✅ Visualização global para diagnóstico
- ✅ Acesso à tela de cadastro de canais (`/channels/create`)
- ✅ Configuração de credenciais de integração WhatsApp/Instagram por farmácia
- ✅ Suporte à operação com troubleshooting técnico

---

## 🔐 Permissões-chave

O perfil Desenvolvedor deve ter acesso às rotas técnicas de configuração e diagnóstico, incluindo:

- `/channels/create` (Cadastro de Canais)
- `/tests` (Diagnóstico)
- Demais módulos conforme permissões de suporte técnico

> Observação: o cadastro de canais é restrito a **Administrador** e **Desenvolvedor**.

---

## 🧭 Ajuda no sistema

A página `/ajuda` utiliza guia específico por perfil. Para Desenvolvedor, o conteúdo correspondente fica em:

- `PharmazapPlus.Web/wwwroot/docs/desenvolvedor.md`

---

## 🛠️ Validação recomendada

Na raiz do repositório:

```bash
dotnet build PharmazapPlus.slnx
dotnet test PharmazapPlus.slnx --no-build
```
