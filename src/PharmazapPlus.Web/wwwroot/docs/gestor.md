# PharmazapPlus 💊 — Guia do Gestor do Sistema

**Plataforma de Atendimento Integrado para Farmácias via WhatsApp**

Este guia é destinado ao **Gestor do Sistema** — o administrador principal com acesso total à plataforma, responsável por gerenciar todas as farmácias cadastradas.

---

## 📑 Índice

- [Visão Geral](#-visão-geral)
- [Como Acessar o Sistema](#-como-acessar-o-sistema)
- [Seu Perfil de Acesso](#-seu-perfil-de-acesso)
- [Dashboard](#-dashboard)
- [Mensagens e Atendimentos](#-mensagens-e-atendimentos)
- [Clientes](#-clientes)
- [Farmácias](#-farmácias)
- [Relatórios](#-relatórios)
- [Menu de Navegação](#-menu-de-navegação)
- [Fluxo de Trabalho Recomendado](#-fluxo-de-trabalho-recomendado)
- [Checklist de Verificação](#-checklist-de-verificação)
- [Suporte](#-suporte)

---

## 🎯 Visão Geral

O **PharmazapPlus** é uma plataforma moderna de atendimento ao cliente desenvolvida especificamente para farmácias.
Permite o gerenciamento completo de conversas via WhatsApp, CRM de clientes e relatórios avançados.

### Principais Recursos

- ✅ **Integração WhatsApp Business API** — Envio/recebimento de mensagens em tempo real
- ✅ **CRM Avançado** — Gestão completa de clientes com importação/exportação CSV
- ✅ **Multi-farmácias** — Gerenciamento centralizado de múltiplas unidades
- ✅ **Sistema de Permissões** — 3 níveis de acesso (Gestor, Administrador, Atendente)
- ✅ **Chat em Tempo Real** — Interface moderna e responsiva
- ✅ **Relatórios e Analytics** — Dashboards personalizados por perfil
- ✅ **Upload de Mídia** — Suporte a imagens, vídeos e documentos

---

## 🔐 Como Acessar o Sistema

1. Abra o navegador e acesse o endereço fornecido pelo seu administrador de TI (ex.: `https://app.pharmazapplus.com`).
2. Você será redirecionado automaticamente para a página de **Login**.
3. Informe suas credenciais:
   - **E-mail:** `gestor@pharmazapplus.com`
   - **Senha:** fornecida pelo suporte
4. Clique em **"Entrar"**.

> **Dica:** Em ambiente de demonstração/desenvolvimento, utilize os botões de acesso rápido na tela de login para preencher as credenciais automaticamente.

---

## 👤 Seu Perfil de Acesso

| Item | Detalhe |
|------|---------|
| **Nível** | 1 — Mais alto |
| **Papel (Role)** | Gestor do Sistema |
| **Escopo** | Todas as farmácias (acesso global) |

### O que você pode fazer

- ✅ Gerenciar **todas** as farmácias cadastradas (criar, editar, excluir)
- ✅ Gerenciar **todos** os usuários do sistema (qualquer nível)
- ✅ Acessar conversas e clientes de **todas** as farmácias
- ✅ Visualizar relatórios e estatísticas globais
- ✅ Acessar configurações do sistema
- ✅ Todas as funcionalidades disponíveis nos níveis inferiores

---

## 📊 Dashboard

Após o login você será direcionado ao **Dashboard do Gestor**, que exibe:

- **Boas-vindas** personalizadas com seu nome
- **Estatísticas Globais:**
  - Total de Farmácias cadastradas
  - Total de Clientes na base
  - Mensagens enviadas/recebidas hoje
- **Ações Rápidas:**
  - Gerenciar Farmácias
  - Ver Relatórios
  - Configurações do Sistema
- **Atividades Recentes** do sistema

---

## 💬 Mensagens e Atendimentos

Acesse pelo menu lateral em **"Mensagens"**.

### O que você verá

- Lista de **todas as conversas** de todas as farmácias
- Conversas organizadas por status:
  - **Pendentes** — aguardando ser assumidas por um atendente
  - **Em Andamento** — atendimento ativo
  - **Finalizadas** — atendimentos encerrados
- Busca em tempo real por cliente ou conteúdo
- Contadores de mensagens não lidas

### Ações disponíveis

| Ação | Descrição |
|------|-----------|
| **Assumir** | Inicia o atendimento de uma conversa pendente |
| **Transferir** | Reassigna um atendimento para outro atendente |
| **Enviar mensagem** | Responde ao cliente no chat |
| **Upload de mídia** | Envia imagens, vídeos ou documentos |
| **Finalizar** | Encerra o atendimento |

> **Nota:** Como Gestor, você **não envia mensagens diretamente** pelo chat — apenas Administradores e Atendentes atribuídos podem enviar. Você tem visibilidade completa para supervisão.

---

## 👥 Clientes

Acesse pelo menu lateral em **"Clientes"**.

### Funcionalidades

- Lista de clientes de **todas as farmácias** em tabela
- Busca por nome, e-mail, telefone ou CPF
- Filtros avançados:
  - Cidade
  - Estado
  - Status (Ativo / Inativo)
  - Uso contínuo (Sim / Não)
- Paginação automática
- Estatísticas resumidas do total de clientes

### Ações disponíveis

| Ação | Descrição |
|------|-----------|
| **Novo Cliente** | Cadastra um novo cliente |
| **Editar** | Altera dados do cliente |
| **Ver Detalhes** | Visualiza histórico completo |
| **Importar CSV** | Importa lista de clientes |
| **Exportar CSV** | Exporta a base de clientes |

#### Formato do CSV para importação

```
Nome;Email;Telefone;CPF/CNPJ;Cidade;Estado
João Silva;joao@email.com;5511999999999;123.456.789-00;São Paulo;SP
```

- **Delimitador:** ponto e vírgula (`;`)
- **Encoding:** UTF-8
- **Primeira linha:** cabeçalho obrigatório

---

## 🏪 Farmácias

Acesse pelo menu lateral em **"Farmácias"** (exclusivo do Gestor).

### Funcionalidades

- Lista de todas as farmácias cadastradas
- Criar nova farmácia
- Editar dados de uma farmácia existente
- Visualizar farmácias ativas e inativas
- Associar Administradores a cada farmácia

---

## 📈 Relatórios

Acesse pelo menu lateral em **"Relatórios"**.

O Dashboard de Relatórios do Gestor apresenta:

- Estatísticas globais consolidadas de todas as farmácias
- Performance individual de cada farmácia
- Indicadores de atendimento (tempo de resposta, volume de mensagens)
- Análise de clientes por farmácia

---

## 🧭 Menu de Navegação

O menu lateral exibe os seguintes itens para o Gestor:

| Item | Descrição |
|------|-----------|
| 🏠 Dashboard | Painel principal com visão global |
| 💬 Mensagens | Conversas de todas as farmácias |
| 👥 Clientes | Base de clientes global |
| 📈 Relatórios | Relatórios e analytics globais |
| 🏪 Farmácias | Gerenciamento de farmácias *(exclusivo)* |
| ⚙️ Configurações | Configurações do sistema |
| 🚪 Sair | Encerrar sessão |

---

## 🔄 Fluxo de Trabalho Recomendado

### Rotina Diária

1. **Login** na plataforma.
2. **Verificar Dashboard** — confira as estatísticas globais do dia.
3. **Monitorar Mensagens** — verifique conversas pendentes em todas as farmácias.
4. **Acompanhar Relatórios** — analise a performance das equipes.
5. **Gerenciar Usuários/Farmácias** conforme necessidade.
6. **Logout** ao finalizar.

### Simulação de uso real

1. Login como Gestor
2. Ver visão geral de todas as farmácias no Dashboard
3. Acessar relatórios globais
4. Verificar performance das farmácias
5. Gerenciar usuários ou farmácias se necessário
6. Logout

---

## ✅ Checklist de Verificação

Use este checklist para validar o funcionamento do seu acesso:

### Autenticação
- [ ] Login com suas credenciais funciona
- [ ] Logout funciona corretamente
- [ ] Ao tentar acessar o sistema sem login, é redirecionado para `/login`

### Dashboard
- [ ] Exibe "Bem-vindo, Gestor do Sistema"
- [ ] Exibe estatísticas globais (todas as farmácias)
- [ ] Cards: Total Farmácias, Total Clientes, Mensagens Hoje
- [ ] Ações disponíveis: Gerenciar Farmácias, Ver Relatórios, Configurações

### Menu Lateral
- [ ] Exibe seu nome e "Gestor do Sistema"
- [ ] Item "Farmácias" está **visível** (exclusivo do Gestor)
- [ ] Todos os itens esperados estão presentes

### Mensagens
- [ ] Lista conversas de **todas** as farmácias
- [ ] Busca funciona
- [ ] Filtros de status funcionam
- [ ] Botão "Transferir" está visível

### Clientes
- [ ] Lista clientes de **todas** as farmácias
- [ ] Busca e filtros funcionam
- [ ] Importar e Exportar CSV funcionam

### Farmácias
- [ ] Página "Farmácias" acessível pelo menu
- [ ] Pode criar, editar farmácias

### Permissões
- [ ] Acesso a todas as farmácias confirmado
- [ ] Relatórios globais acessíveis

---

## 📞 Suporte

Para suporte, entre em contato:

- **E-mail:** suporte@pharmazapplus.com
- **WhatsApp:** (11) 99999-9999
- **Website:** https://www.pharmazapplus.com

---

**Desenvolvido com ❤️ pela equipe PharmazapPlus**

*Versão 1.5.0 — Março 2026*
