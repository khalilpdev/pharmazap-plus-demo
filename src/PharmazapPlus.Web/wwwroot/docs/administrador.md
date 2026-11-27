# PharmazapPlus 💊 — Guia do Administrador da Farmácia

**Plataforma de Atendimento Integrado para Farmácias via WhatsApp**

Este guia é destinado ao **Administrador da Farmácia** — o responsável por gerenciar uma unidade farmacêutica específica, sua equipe de atendentes e todos os clientes da farmácia.

---

## 📑 Índice

- [Visão Geral](#-visão-geral)
- [Como Acessar o Sistema](#-como-acessar-o-sistema)
- [Seu Perfil de Acesso](#-seu-perfil-de-acesso)
- [Dashboard](#-dashboard)
- [Mensagens e Atendimentos](#-mensagens-e-atendimentos)
- [Clientes](#-clientes)
- [Atendentes](#-atendentes)
- [Relatórios](#-relatórios)
- [Menu de Navegação](#-menu-de-navegação)
- [Fluxo de Trabalho Recomendado](#-fluxo-de-trabalho-recomendado)
- [Checklist de Verificação](#-checklist-de-verificação)
- [Suporte](#-suporte)

---

## 🎯 Visão Geral

O **PharmazapPlus** é uma plataforma moderna de atendimento ao cliente desenvolvida especificamente para farmácias.
Permite o gerenciamento completo de conversas via WhatsApp, CRM de clientes, gestão da sua equipe e relatórios avançados.

### Principais Recursos

- ✅ **Integração WhatsApp Business API** — Envio/recebimento de mensagens em tempo real
- ✅ **CRM Avançado** — Gestão completa de clientes com importação/exportação CSV
- ✅ **Gestão de Atendentes** — Gerenciamento da equipe da sua farmácia
- ✅ **Atendimentos** — Assumir, transferir e encerrar conversas
- ✅ **Chat em Tempo Real** — Interface moderna e responsiva
- ✅ **Relatórios e Analytics** — Dashboard personalizado para sua farmácia
- ✅ **Upload de Mídia** — Suporte a imagens, vídeos e documentos

---

## 🔐 Como Acessar o Sistema

1. Abra o navegador e acesse o endereço fornecido pelo suporte (ex.: `https://app.pharmazapplus.com`).
2. Você será redirecionado automaticamente para a página de **Login**.
3. Informe suas credenciais:
   - **E-mail:** fornecido pelo Gestor do Sistema
   - **Senha:** fornecida pelo Gestor do Sistema
4. Clique em **"Entrar"**.

> **Dica:** Em ambiente de demonstração/desenvolvimento, utilize os botões de acesso rápido na tela de login para preencher as credenciais automaticamente.

---

## 👤 Seu Perfil de Acesso

| Item | Detalhe |
|------|---------|
| **Nível** | 2 — Médio |
| **Papel (Role)** | Administrador da Farmácia |
| **Escopo** | Sua farmácia específica |

### O que você pode fazer

- ✅ Gerenciar usuários (atendentes) da **sua farmácia**
- ✅ Ver e editar **todos** os clientes da sua farmácia
- ✅ Acessar **todas** as conversas da sua farmácia
- ✅ Assumir e transferir atendimentos para atendentes da farmácia
- ✅ Ver relatórios da farmácia
- ✅ Importar e exportar dados da farmácia
- ❌ Não pode gerenciar outras farmácias
- ❌ Não pode criar ou editar Gestores do Sistema

---

## 📊 Dashboard

Após o login você será direcionado ao **Dashboard do Administrador**, que exibe:

- **Boas-vindas** personalizadas com seu nome
- **Estatísticas da sua Farmácia:**
  - Clientes Ativos
  - Mensagens enviadas/recebidas hoje
  - Atendentes Ativos
  - Taxa de Resposta
- **Ações Rápidas:**
  - Gerenciar Clientes
  - Ver Mensagens
  - Relatórios da Farmácia
- **Atividades Recentes** da farmácia

---

## 💬 Mensagens e Atendimentos

Acesse pelo menu lateral em **"Mensagens"**.

### O que você verá

- Lista de **conversas da sua farmácia**
- Conversas organizadas por status:
  - **Pendentes** — aguardando ser assumidas por um atendente
  - **Em Andamento** — atendimento ativo com atendente responsável
  - **Finalizadas** — atendimentos encerrados
- Busca em tempo real por cliente ou conteúdo
- Contadores de mensagens não lidas

### Ações disponíveis

| Ação | Descrição |
|------|-----------|
| **Assumir** | Inicia o atendimento de uma conversa pendente |
| **Transferir** | Reassigna um atendimento para outro atendente da sua farmácia |
| **Enviar mensagem** | Responde ao cliente no chat |
| **Upload de mídia** | Envia imagens, vídeos ou documentos |
| **Finalizar** | Encerra o atendimento (seleciona motivo, valor e comprovante conforme regras) |

### Como Assumir um Atendimento

1. Na lista de conversas, localize uma conversa com status **"Pendente"**.
2. Clique no cartão da conversa para abrir o chat.
3. Clique no botão **"Assumir"** para iniciar o atendimento.
4. A conversa passará para o status **"Em Andamento"** com você como responsável.

### Como Transferir um Atendimento

1. Com o atendimento aberto, clique no botão **"Transferir"**.
2. Selecione o atendente de destino na lista.
3. Confirme a transferência.
4. O atendente selecionado receberá a notificação em tempo real.

---

## 👥 Clientes

Acesse pelo menu lateral em **"Clientes"**.

### Funcionalidades

- Lista de clientes da **sua farmácia** em tabela
- Busca por nome, e-mail, telefone ou CPF
- Filtros avançados:
  - Cidade
  - Estado
  - Status (Ativo / Inativo)
  - Uso contínuo (Sim / Não)
- Paginação automática
- Estatísticas resumidas

### Ações disponíveis

| Ação | Descrição |
|------|-----------|
| **Novo Cliente** | Cadastra um novo cliente |
| **Editar** | Altera dados do cliente |
| **Ver Detalhes** | Visualiza histórico completo |
| **Importar CSV** | Importa lista de clientes |
| **Exportar CSV** | Exporta a base de clientes da farmácia |

#### Formato do CSV para importação

```
Nome;Email;Telefone;CPF/CNPJ;Cidade;Estado
João Silva;joao@email.com;5511999999999;123.456.789-00;São Paulo;SP
```

- **Delimitador:** ponto e vírgula (`;`)
- **Encoding:** UTF-8
- **Primeira linha:** cabeçalho obrigatório

> **Dica:** Use o botão **"Exportar"** para baixar o modelo de CSV e preenchê-lo com os dados a importar.

---

## 👨‍💼 Atendentes

Acesse pelo menu lateral em **"Atendentes"** (disponível para Administrador e Gestor).

### Funcionalidades

- Lista de atendentes da sua farmácia em cards
- Busca por nome ou e-mail
- Filtro por status (Ativo / Inativo / Todos)
- Contagem de atendimentos por atendente

### Ações disponíveis

| Ação | Descrição |
|------|-----------|
| **Novo Atendente** | Abre o diálogo de cadastro com validação |
| **Editar** | Altera nome, e-mail, telefone ou senha |
| **Redefinir Senha** | Redefine a senha do atendente via diálogo dedicado |
| **Ativar / Desativar** | Controla se o atendente pode acessar o sistema |

### Como Adicionar um Novo Atendente

1. Clique em **"Novo Atendente"**.
2. Preencha os campos: nome, e-mail, telefone e senha.
3. Clique em **"Salvar"**.
4. O novo atendente aparecerá na lista e poderá fazer login imediatamente.

### Como Desativar um Atendente

1. Localize o atendente na lista.
2. Clique no botão de **desativar** (ícone de desabilitação).
3. Confirme a ação.
4. O atendente ficará com status **"Inativo"** e não conseguirá fazer login.

---

## 📈 Relatórios

Acesse pelo menu lateral em **"Relatórios"**.

O Dashboard de Relatórios do Administrador apresenta:

- Estatísticas de atendimento da sua farmácia
- Performance individual de cada atendente
- Volume de mensagens e tempo médio de resposta
- Análise da base de clientes

---

## 🧭 Menu de Navegação

O menu lateral exibe os seguintes itens para o Administrador:

| Item | Descrição |
|------|-----------|
| 🏠 Dashboard | Painel principal com visão da farmácia |
| 💬 Mensagens | Conversas da sua farmácia |
| 👥 Clientes | Base de clientes da farmácia |
| 📈 Relatórios | Relatórios da farmácia |
| 👨‍💼 Atendentes | Gestão da equipe *(exclusivo Adm/Gestor)* |
| 🏷️ Etiquetas | Gerenciamento de etiquetas *(exclusivo Adm/Gestor)* |
| 📋 Motivos Encerramento | Configuração dos motivos de encerramento *(exclusivo Adm)* |
| ⚙️ Configurações | Configurações da farmácia |
| 🚪 Sair | Encerrar sessão |

> **Nota:** O item "Farmácias" **não está disponível** para Administradores — apenas para o Gestor do Sistema.

---

## 🔄 Fluxo de Trabalho Recomendado

### Rotina Diária

1. **Login** na plataforma.
2. **Verificar Dashboard** — confira as estatísticas da farmácia do dia.
3. **Monitorar Mensagens** — verifique conversas pendentes e distribua para atendentes.
4. **Acompanhar Atendentes** — verifique o desempenho da equipe.
5. **Verificar Relatórios** — analise os indicadores de atendimento.
6. **Logout** ao finalizar.

### Simulação de uso real

1. Login como Administrador
2. Verificar estatísticas do dia no Dashboard
3. Abrir conversa pendente e clicar em **"Assumir"** ou **"Transferir"** para um atendente
4. Adicionar novo cliente se necessário
5. Importar lista de clientes CSV se necessário
6. Ver relatório de atendimento
7. Logout

---

## ✅ Checklist de Verificação

Use este checklist para validar o funcionamento do seu acesso:

### Autenticação
- [ ] Login com suas credenciais funciona
- [ ] Logout funciona corretamente

### Dashboard
- [ ] Exibe "Bem-vindo, [seu nome]"
- [ ] Exibe estatísticas da sua farmácia
- [ ] Cards: Clientes Ativos, Mensagens Hoje, Atendentes Ativos, Taxa de Resposta
- [ ] Ações disponíveis: Gerenciar Clientes, Ver Mensagens, Relatórios

### Menu Lateral
- [ ] Exibe seu nome e "Administrador da Farmácia"
- [ ] Item "Farmácias" **NÃO** está visível
- [ ] Itens "Atendentes" e "Etiquetas" estão **visíveis**
- [ ] Item "Relatórios" está visível

### Mensagens
- [ ] Lista conversas apenas da **sua farmácia**
- [ ] Busca funciona
- [ ] Botão "Assumir" funciona
- [ ] Botão "Transferir" está visível e funciona
- [ ] Envio de mensagem funciona

### Clientes
- [ ] Lista clientes apenas da **sua farmácia**
- [ ] Busca e filtros funcionam
- [ ] Adicionar, editar cliente funciona
- [ ] Importar e Exportar CSV funcionam

### Atendentes
- [ ] Página "Atendentes" acessível pelo menu
- [ ] Pode adicionar novo atendente
- [ ] Pode editar e desativar atendentes
- [ ] Diálogos abrem e fecham corretamente (botão X funciona)

### Permissões
- [ ] Acesso apenas à sua farmácia confirmado
- [ ] Relatórios da farmácia acessíveis

---

## 📞 Suporte

Para suporte, entre em contato:

- **E-mail:** suporte@pharmazapplus.com
- **WhatsApp:** (11) 99999-9999
- **Website:** https://www.pharmazapplus.com

---

**Desenvolvido com ❤️ pela equipe PharmazapPlus**

*Versão 1.5.0 — Março 2026*
