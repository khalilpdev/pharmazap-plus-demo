# Integração WhatsApp Não-Oficial — Evolution Go

> **Aviso:** O Evolution Go utiliza o protocolo do WhatsApp Web (via biblioteca `whatsmeow`) e **não** é homologado pela Meta. Use este método ciente das políticas da plataforma e dos riscos de bloqueio de número.

---

## Sumário

1. [O que é o Evolution Go e por que usá-lo](#o-que-é-o-evolution-go-e-por-que-usá-lo)
2. [Comparativo: Evolution Go × Evolution API (Node.js)](#comparativo-evolution-go--evolution-api-nodejs)
3. [Arquitetura de Integração com PharmazapPlus](#arquitetura-de-integração-com-pharmazapplus)
4. [Pré-requisitos — Ubuntu Server](#pré-requisitos--ubuntu-server)
5. [Instalação no Ubuntu Server via Docker Compose](#instalação-no-ubuntu-server-via-docker-compose)
   - [1. Preparar o servidor](#1-preparar-o-servidor)
   - [2. Instalar Docker e Docker Compose](#2-instalar-docker-e-docker-compose)
   - [3. Criar o arquivo `.env`](#3-criar-o-arquivo-env)
   - [4. Criar o `docker-compose.yml`](#4-criar-o-docker-composeyml)
   - [5. Subir os serviços](#5-subir-os-serviços)
6. [Ativação da Licença](#ativação-da-licença)
7. [Conectar o Número via QR Code](#conectar-o-número-via-qr-code)
8. [Configurar Webhook para o PharmazapPlus](#configurar-webhook-para-o-pharmazapplus)
9. [Configurar o Canal no PharmazapPlus](#configurar-o-canal-no-pharmazapplus)
10. [Referência de Variáveis de Ambiente](#referência-de-variáveis-de-ambiente)
11. [Referência dos Endpoints REST](#referência-dos-endpoints-rest)
12. [Diferenças de API em relação à Evolution API (Node.js)](#diferenças-de-api-em-relação-à-evolution-api-nodejs)
13. [Configuração do Nginx como Reverse Proxy](#configuração-do-nginx-como-reverse-proxy)
14. [Segurança](#segurança)
15. [Troubleshooting](#troubleshooting)
16. [Recursos e Links](#recursos-e-links)

---

## O que é o Evolution Go e por que usá-lo

O **Evolution Go** é uma API de WhatsApp não-oficial de **alta performance**, escrita em Go, mantida pela **Evolution Foundation**. Ele fornece a mesma proposta de valor da Evolution API (Node.js), mas com consumo de recursos drasticamente menor — ideal para servidores com pouca RAM ou para quem precisa rodar várias instâncias em paralelo.

Internamente, o Evolution Go usa a biblioteca **[whatsmeow](https://github.com/tulir/whatsmeow)** (em Go) como motor de protocolo WhatsApp Web, em vez do Baileys (JavaScript). O resultado é um binário compilado muito mais eficiente.

| Recurso | Link |
|---------|------|
| 🌐 Site oficial | <https://evolutionfoundation.com.br> |
| 📖 Documentação | <https://docs.evolutionfoundation.com.br> |
| 🐙 Repositório GitHub | <https://github.com/evolution-foundation/evolution-go> |
| 🐳 Docker Hub | <https://hub.docker.com/r/evoapicloud/evolution-go> |
| 💬 Comunidade | <https://evolutionfoundation.com.br/community> |

---

## Comparativo: Evolution Go × Evolution API (Node.js)

| Aspecto | Evolution API (Node.js) | Evolution Go |
|---------|------------------------|--------------|
| **Linguagem** | TypeScript/Node.js | Go (compilado) |
| **Protocolo WhatsApp** | Baileys | whatsmeow |
| **Imagem Docker** | ~700–900 MB | ~77 MB |
| **RAM em idle** | ~200–400 MB | ~40–100 MB |
| **Licença** | MIT | Apache 2.0 (requer ativação gratuita) |
| **Banco de dados** | Opcional (SQLite/PostgreSQL) | **Obrigatório** (2× PostgreSQL) |
| **MinIO/S3 nativo** | Não (arquivos locais) | ✅ Suporte nativo |
| **AMQP / NATS** | Não | ✅ RabbitMQ e NATS integrados |
| **Manager UI** | `/manager` | `/manager/login` |
| **Swagger UI** | Não disponível | `/swagger/index.html` |
| **Variável de autenticação** | `AUTHENTICATION_API_KEY` | `GLOBAL_API_KEY` |
| **Header HTTP de autenticação** | `apikey: <chave>` | `apikey: <chave>` |

> ✅ **Recomendação de uso:** Para servidores Ubuntu com menos de **1 GB de RAM** ou para hospedar muitas farmácias, use o Evolution Go. Para ambientes que já usam a Evolution API Node.js e não querem migrar, mantenha a documentação `WHATSAPP-EVOLUTION.md`.

---

## Arquitetura de Integração com PharmazapPlus

O fluxo de mensagens é idêntico ao da Evolution API:

```
[WhatsApp — celular da farmácia]
        │ mensagem enviada/recebida
        ▼
[Evolution Go — porta 8080]
  └─ instância: "<ChaveWhatsApp>"
        │ evento via Webhook HTTP POST
        ▼
[PharmazapPlus API — porta 5051 ou 6001]
  POST /api/whatsapp/webhook
        │ processa, persiste, notifica
        ▼
[PharmazapPlus Web — porta 5160]
  └─ Chat em tempo real via SignalR
```

### Como as credenciais são armazenadas no PharmazapPlus

Igual à Evolution API: as credenciais ficam **por farmácia** no banco de dados, tabela `FarmaciaIntegracoes`:

| Campo | Valor para Evolution Go |
|-------|------------------------|
| `WhatsAppApiUrl` | URL pública do Evolution Go (ex.: `https://evo.sua-farmacia.com`) |
| `WhatsAppApiKey` | Valor de `GLOBAL_API_KEY` definido no `.env` do servidor |
| `ChaveWhatsApp` | Gerado automaticamente pelo PharmazapPlus — usado como nome da instância |

---

## Pré-requisitos — Ubuntu Server

| Requisito | Versão mínima | Observação |
|-----------|--------------|------------|
| Ubuntu Server | 20.04 LTS | 22.04 ou 24.04 LTS recomendados |
| RAM | 512 MB | 1 GB ou mais recomendado para múltiplas instâncias |
| Disco | 10 GB livres | Para dados do Docker e PostgreSQL |
| Docker Engine | 24.x+ | Instalado via script oficial |
| Docker Compose | v2.x+ | Plugin do Docker (não o binário legado) |
| Domínio ou IP fixo | — | Obrigatório para o webhook funcionar |
| Porta 8080 | Aberta no firewall | Ou a porta que você configurar |

### Verificar requisitos

```bash
# Verificar SO e RAM disponível
lsb_release -a
free -h

# Verificar espaço em disco
df -h /

# Verificar se porta 8080 está livre
ss -tlnp | grep :8080
```

---

## Instalação no Ubuntu Server via Docker Compose

### 1. Preparar o servidor

```bash
# Atualizar pacotes do sistema
sudo apt update && sudo apt upgrade -y

# Instalar utilitários básicos
sudo apt install -y curl git wget nano ufw
```

**Configurar firewall (UFW):**

```bash
# Habilitar UFW
sudo ufw enable

# Liberar SSH (importante: não feche sua própria sessão!)
sudo ufw allow ssh

# Liberar porta do Evolution Go
sudo ufw allow 8080/tcp

# Liberar porta HTTPS (se usar Nginx/Caddy)
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# Verificar regras ativas
sudo ufw status
```

### 2. Instalar Docker e Docker Compose

```bash
# Remover versões antigas (se houver)
sudo apt remove -y docker docker-engine docker.io containerd runc 2>/dev/null

# Instalar dependências
sudo apt install -y ca-certificates curl gnupg lsb-release

# Adicionar chave GPG oficial do Docker
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
  | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

# Adicionar repositório do Docker
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
  https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Instalar Docker Engine e plugin Compose
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Iniciar e habilitar Docker
sudo systemctl enable --now docker

# (Opcional) Permitir uso sem sudo
sudo usermod -aG docker $USER
newgrp docker

# Verificar instalação
docker --version
docker compose version
```

### 3. Criar o arquivo `.env`

Escolha um diretório de trabalho no servidor:

```bash
mkdir -p ~/evolution-go
cd ~/evolution-go
```

Crie o arquivo `.env`:

```bash
nano .env
```

Conteúdo do `.env` (ajuste os valores marcados com `★`):

```env
# ──────────────────────────────────────────────
# SERVIDOR
# ──────────────────────────────────────────────
SERVER_PORT=8080
CLIENT_NAME=evolution          # identificador interno da instância
OS_NAME=Evolution GO

# ──────────────────────────────────────────────
# AUTENTICAÇÃO ★ OBRIGATÓRIO
# Gere uma chave forte: openssl rand -base64 32
# Esta chave será informada no PharmazapPlus em /channels/create
# ──────────────────────────────────────────────
GLOBAL_API_KEY=TROQUE_POR_UMA_CHAVE_SECRETA_FORTE

# ──────────────────────────────────────────────
# BANCO DE DADOS ★ OBRIGATÓRIO
# O Evolution Go precisa de 2 bancos PostgreSQL separados.
# Se usar o docker-compose abaixo, mantenha os nomes como estão.
# ──────────────────────────────────────────────
POSTGRES_AUTH_DB=postgresql://evogo:<senha-forte>@postgres-evogo:5432/evogo_auth?sslmode=disable
POSTGRES_USERS_DB=postgresql://evogo:<senha-forte>@postgres-evogo:5432/evogo_users?sslmode=disable
DATABASE_SAVE_MESSAGES=false   # true = persiste mensagens no banco (consome mais disco)

# ──────────────────────────────────────────────
# WEBHOOK GLOBAL ★ OBRIGATÓRIO para receber mensagens
# Substitua pelo endereço público da sua API PharmazapPlus.
# ──────────────────────────────────────────────
WEBHOOK_URL=https://api.sua-farmacia.com/api/whatsapp/webhook
WEBHOOK_FILES=true             # envia arquivos de mídia pelo webhook

# ──────────────────────────────────────────────
# COMPORTAMENTO NA INICIALIZAÇÃO
# ──────────────────────────────────────────────
CONNECT_ON_STARTUP=true        # reconecta instâncias salvas ao reiniciar

# ──────────────────────────────────────────────
# LOGGING
# ──────────────────────────────────────────────
WADEBUG=INFO                   # DEBUG | INFO | WARN | ERROR
LOGTYPE=console                # console | json

# ──────────────────────────────────────────────
# MINIO — armazenamento de mídia (opcional)
# Desative se não quiser armazenar arquivos enviados/recebidos.
# ──────────────────────────────────────────────
MINIO_ENABLED=false
# MINIO_ENDPOINT=minio:9000
# MINIO_ACCESS_KEY=minioadmin
# MINIO_SECRET_KEY=minioadmin
# MINIO_BUCKET=evolution-media
# MINIO_USE_SSL=false

# ──────────────────────────────────────────────
# AMQP/RabbitMQ (opcional — para filas de mensagens)
# ──────────────────────────────────────────────
AMQP_GLOBAL_ENABLED=false
# AMQP_URL=amqp://admin:admin@rabbitmq:5672/default

# ──────────────────────────────────────────────
# PROXY (opcional — para contornar bloqueios de IP)
# ──────────────────────────────────────────────
# PROXY_PROTOCOL=socks5
# PROXY_HOST=proxy.example.com
# PROXY_PORT=1080
# PROXY_USERNAME=user
# PROXY_PASSWORD=secret
```

> 💡 **Dica de segurança:** Nunca use a chave padrão `429683C4C977415CAAFCCE10F7D57E11` do `.env.example` em produção.  
> Gere uma chave com: `openssl rand -base64 32`

### 4. Criar o `docker-compose.yml`

```bash
nano docker-compose.yml
```

```yaml
# =============================================================================
# Evolution Go — WhatsApp não-oficial de alta performance (Go + whatsmeow)
# =============================================================================
# Para uso com o PharmazapPlus como backend de mensagens não-oficiais.
# Documentação: docs/WHATSAPP_EVOLUTION-GO.md
# =============================================================================

services:

  evolution-go:
    image: evoapicloud/evolution-go:latest
    container_name: evolution-go
    restart: unless-stopped
    ports:
      - "8080:8080"
    env_file:
      - .env
    depends_on:
      postgres-evogo:
        condition: service_healthy
    volumes:
      - evolution_go_data:/app/data
    networks:
      - evolution-go-net
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 5
      start_period: 60s

  postgres-evogo:
    image: postgres:16-alpine
    container_name: postgres-evogo
    restart: unless-stopped
    environment:
      POSTGRES_USER: evogo
      POSTGRES_PASSWORD: <senha-forte>      # ★ altere para uma senha forte
      POSTGRES_MULTIPLE_DATABASES: evogo_auth,evogo_users
    volumes:
      - postgres_evogo_data:/var/lib/postgresql/data
      - ./init-multiple-dbs.sh:/docker-entrypoint-initdb.d/init-multiple-dbs.sh:ro
    networks:
      - evolution-go-net
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U evogo"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  evolution_go_data:
  postgres_evogo_data:

networks:
  evolution-go-net:
    driver: bridge
```

O PostgreSQL precisa de um script de inicialização para criar os dois bancos de dados:

```bash
nano init-multiple-dbs.sh
```

```bash
#!/bin/bash
# Cria múltiplos bancos de dados no PostgreSQL ao inicializar o container.
# Chamado via docker-entrypoint-initdb.d.
set -e

for db in $(echo $POSTGRES_MULTIPLE_DATABASES | tr ',' ' '); do
    echo "Criando banco de dados: $db"
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
        CREATE DATABASE "$db";
        GRANT ALL PRIVILEGES ON DATABASE "$db" TO "$POSTGRES_USER";
EOSQL
done
```

```bash
chmod +x init-multiple-dbs.sh
```

Para uso em **Windows (CMD)**, você também pode manter a versão equivalente `init-multiple-dbs.bat`:

```bat
@echo off
setlocal enabledelayedexpansion

if "%POSTGRES_MULTIPLE_DATABASES%"=="" (
  echo Variável POSTGRES_MULTIPLE_DATABASES não definida.
  exit /b 1
)

if "%POSTGRES_USER%"=="" (
  echo Variável POSTGRES_USER não definida.
  exit /b 1
)

for %%D in (%POSTGRES_MULTIPLE_DATABASES:,= %) do (
  echo Criando banco de dados: %%D
  psql -v ON_ERROR_STOP=1 --username "%POSTGRES_USER%" -d postgres -c "CREATE DATABASE \"%%D\";"
  if errorlevel 1 exit /b !errorlevel!
  psql -v ON_ERROR_STOP=1 --username "%POSTGRES_USER%" -d postgres -c "GRANT ALL PRIVILEGES ON DATABASE \"%%D\" TO \"%POSTGRES_USER%\";"
  if errorlevel 1 exit /b !errorlevel!
)

endlocal
```

> ℹ️ **Importante:** no `docker-compose`, mantenha o volume montando `init-multiple-dbs.sh` (script executado dentro do container Linux).
> O `init-multiple-dbs.bat` é um equivalente para execução manual em host Windows.


> ⚠️ **Atenção com a senha do PostgreSQL:** O valor de `POSTGRES_PASSWORD` no `docker-compose.yml` deve coincidir com o que está nas URLs `POSTGRES_AUTH_DB` e `POSTGRES_USERS_DB` no `.env`.

### 5. Subir os serviços

```bash
# Subir em background
docker compose up -d

# Acompanhar os logs em tempo real
docker compose logs -f evolution-go

# Verificar status dos containers
docker compose ps
```

Aguarde até ver nos logs algo como:

```
INFO  Server started on :8080
INFO  License check completed
```

> ⏱️ Na **primeira execução**, o PostgreSQL pode demorar 30–60 segundos para inicializar os bancos. O `healthcheck` do `evolution-go` garante que ele só suba após o banco estar pronto.

---

## Ativação da Licença

O Evolution Go requer ativação de licença antes de aceitar requisições (sem ativação, todos os endpoints retornam `503 Service Unavailable`). A licença é **gratuita** e é ativada pela interface web do Manager.

### Passos de ativação:

**1.** Acesse o Manager no navegador:

```
http://IP_DO_SEU_SERVIDOR:8080/manager/login
```

**2.** Faça login com:
- **API URL:** `http://localhost:8080` (ou a URL pública do servidor)
- **API Key:** o valor de `GLOBAL_API_KEY` definido no `.env`

**3.** Siga o fluxo de registro de licença apresentado na tela. Você precisará:
   - Informar nome/e-mail para registro
   - Confirmar a ativação

**4.** Após ativação, a API estará totalmente operacional.

> 📌 **O status da licença é persistido no banco PostgreSQL** (tabela `runtime_configs`). A licença não precisa ser reativada a cada reinicialização — apenas quando o banco de dados for recriado do zero.

### Verificar status da licença via curl:

```bash
curl -s http://IP_DO_SEU_SERVIDOR:8080/health \
  -H "apikey: SUA_GLOBAL_API_KEY" | jq .
```

---

## Conectar o Número via QR Code

Após a licença estar ativa, você precisa criar uma **instância** e escanear o QR Code com o WhatsApp do número da farmácia.

### 1. Obter a ChaveWhatsApp no PharmazapPlus

Primeiro, cadastre o canal no PharmazapPlus (seção abaixo) para obter a **ChaveWhatsApp** — este será o nome da instância na Evolution Go.

> ✅ O nome da instância enviado para a Evolution Go é **exatamente** o identificador digitado no canal (apenas `trim` de espaços).

### 2. Criar a instância

```bash
# Substitua SUA_CHAVE e SUA_API_KEY pelos valores reais
curl -s -X POST http://IP_DO_SEU_SERVIDOR:8080/instance/create \
  -H "Content-Type: application/json" \
  -H "apikey: SUA_GLOBAL_API_KEY" \
  -d '{
    "instanceName": "SUA_CHAVE",
    "integration": "WHATSAPP-BAILEYS"
  }' | jq .
```

### 3. Obter o QR Code

O PharmazapPlus suporta as seguintes variantes de rota de QR Code, nesta ordem:

1. `GET /instance/{name}/qrcode` *(preferencial na Evolution Go)*
2. `GET /instance/qrcode/{name}` *(variante compatível observada em algumas builds)*
3. `GET /instance/{name}/qr` *(fallback adicional de compatibilidade)*
4. `GET /instance/connect/{name}` *(legado da Evolution API Node.js)*

Se `GET /instance/fetchInstances?instanceName={name}` já retornar `qrcode`/`base64`, o PharmazapPlus reutiliza esse valor como fonte primária do QR e evita depender da rota dedicada.

```bash
curl -s http://IP_DO_SEU_SERVIDOR:8080/instance/SUA_CHAVE/qrcode \
  -H "apikey: SUA_GLOBAL_API_KEY" | jq .
```

A resposta conterá um campo `base64` com a imagem do QR Code. Para visualizá-lo:
- Cole o valor `base64` em <https://www.qrcodescan.in/>
- **Ou** acesse o Manager em `http://IP_DO_SEU_SERVIDOR:8080/manager/login` → seção de instâncias → QR Code visual

No PharmazapPlus, essa imagem agora também é persistida pela API em `/uploads/qrcodes/` e o Web consome a URL gerada diretamente. Isso evita trafegar o base64 gigante pelo Blazor Server e torna a exibição do QR mais estável.

### 4. Escanear com o WhatsApp

No celular com o número da farmácia:
1. Abra o **WhatsApp** → Menu (⋮) → **Aparelhos conectados**
2. Toque em **Conectar um aparelho**
3. Escaneie o QR Code

Após a leitura, o status da instância ficará `open`:

```bash
# Verificar status da instância
curl -s http://IP_DO_SEU_SERVIDOR:8080/instance/SUA_CHAVE/status \
  -H "apikey: SUA_GLOBAL_API_KEY" | jq .
```

### Reconexão automática

Com `CONNECT_ON_STARTUP=true` no `.env` e `DATABASE_SAVE_MESSAGES=false` (mas com PostgreSQL ativo), a sessão autenticada é salva no banco e as instâncias se reconectam automaticamente após reinicializações — **sem precisar escanear o QR Code novamente**.

---

## Configurar Webhook para o PharmazapPlus

O webhook é configurado via variável `WEBHOOK_URL` no `.env`. Essa URL deve apontar para o endpoint do PharmazapPlus que processa eventos do WhatsApp.

### URL padrão do webhook PharmazapPlus

```
https://api.sua-farmacia.com/api/whatsapp/webhook
```

### Verificar se o webhook está funcionando

```bash
# Testar manualmente o endpoint do PharmazapPlus
curl -X POST https://api.sua-farmacia.com/api/whatsapp/webhook \
  -H "Content-Type: application/json" \
  -d '{"event":"test","data":{}}'
```

Espere HTTP `200 OK` como resposta.

### Configurar webhook por instância (opcional)

Além do webhook global (`WEBHOOK_URL`), você pode definir webhooks específicos por instância:

```bash
curl -s -X POST http://IP_DO_SEU_SERVIDOR:8080/webhook/SUA_CHAVE \
  -H "Content-Type: application/json" \
  -H "apikey: SUA_GLOBAL_API_KEY" \
  -d '{
    "url": "https://api.sua-farmacia.com/api/whatsapp/webhook",
    "enabled": true,
    "events": ["MESSAGES_UPSERT", "CONNECTION_UPDATE", "SEND_MESSAGE"]
  }' | jq .
```

> 📌 **Eventos importantes para o PharmazapPlus:**
> - `MESSAGES_UPSERT` — nova mensagem recebida
> - `CONNECTION_UPDATE` — mudança no status da conexão (conectado/desconectado)
> - `SEND_MESSAGE` — confirmação de mensagem enviada
> - `MESSAGES_READ` — mensagem lida pelo destinatário

---

## Configurar o Canal no PharmazapPlus

1. Acesse `/channels/create` (disponível apenas para **Administrador** e **Desenvolvedor**).
2. Selecione o tipo de canal **WhatsApp**.
3. Desmarque a opção **"Oficial (Meta)"** para habilitar o modo Não-Oficial.
4. Preencha:
   - **URL da API:** URL pública do Evolution Go (ex.: `https://evo.sua-farmacia.com`)
   - **API Key:** O valor de `GLOBAL_API_KEY` definido no `.env`
5. No campo **Identificador da instância**, informe o telefone no formato **DDI + DDD + número** (ex.: `5562998808389`).
6. Clique em **Salvar**.

O PharmazapPlus usa **exatamente o identificador digitado** como `instanceName` na Evolution Go, aplicando apenas `Trim()` de espaços. O nome da farmácia/canal **não** é usado como identidade da instância.

> ✅ **Retry seguro para token duplicado:** se `POST /instance/create` falhar com erro de token duplicado (`uni_instances_token` / `SQLSTATE 23505`), o PharmazapPlus gera um **novo token** e repete a criação **uma única vez**, mantendo o mesmo `instanceName`.

> ⚠️ **Nota de Compatibilidade:** O PharmazapPlus foi originalmente desenvolvido para a Evolution API (Node.js). O Evolution Go mantém os mesmos **nomes de endpoints principais** (`/instance/create`, `/instance/connect/`, etc.) para compatibilidade reversa, mas pode haver diferenças em endpoints específicos. Consulte a seção [Diferenças de API](#diferenças-de-api-em-relação-à-evolution-api-nodejs) e verifique no Swagger (`/swagger/index.html`) se todos os endpoints usados pelo PharmazapPlus estão presentes.

---

## Referência de Variáveis de Ambiente

| Variável | Descrição | Obrigatório | Padrão |
|----------|-----------|-------------|--------|
| `SERVER_PORT` | Porta do servidor HTTP | Não | `8080` |
| `CLIENT_NAME` | Identificador interno do cliente | Não | `evolution` |
| `OS_NAME` | Nome exibido no Manager | Não | `Evolution GO` |
| `GLOBAL_API_KEY` | Chave mestra de autenticação da API. Informe esta chave no PharmazapPlus. | **Sim** | — |
| `POSTGRES_AUTH_DB` | URL de conexão PostgreSQL para banco de autenticação/licença | **Sim** | — |
| `POSTGRES_USERS_DB` | URL de conexão PostgreSQL para banco de instâncias/sessões | **Sim** | — |
| `DATABASE_SAVE_MESSAGES` | Persistir mensagens no banco | Não | `false` |
| `WEBHOOK_URL` | URL global para envio de eventos ao PharmazapPlus | **Sim** | — |
| `WEBHOOK_FILES` | Incluir arquivos de mídia nos payloads do webhook | Não | `true` |
| `CONNECT_ON_STARTUP` | Reconectar instâncias salvas ao inicializar | Não | `true` |
| `WADEBUG` | Nível de log do whatsmeow: `DEBUG`, `INFO`, `WARN`, `ERROR` | Não | `INFO` |
| `LOGTYPE` | Formato dos logs: `console` ou `json` | Não | `console` |
| `MINIO_ENABLED` | Ativar armazenamento de mídia no MinIO/S3 | Não | `false` |
| `MINIO_ENDPOINT` | Endereço do servidor MinIO | Se MinIO ativo | — |
| `MINIO_ACCESS_KEY` | Chave de acesso MinIO | Se MinIO ativo | — |
| `MINIO_SECRET_KEY` | Chave secreta MinIO | Se MinIO ativo | — |
| `MINIO_BUCKET` | Nome do bucket MinIO | Se MinIO ativo | `evolution-media` |
| `AMQP_GLOBAL_ENABLED` | Ativar publicação de eventos via RabbitMQ | Não | `false` |
| `AMQP_URL` | URL de conexão RabbitMQ | Se AMQP ativo | — |
| `PROXY_PROTOCOL` | Protocolo do proxy (`http`, `https`, `socks5`) | Não | — |
| `PROXY_HOST` | Host do proxy | Se proxy ativo | — |
| `PROXY_PORT` | Porta do proxy | Se proxy ativo | — |
| `PROXY_USERNAME` | Usuário do proxy | Não | — |
| `PROXY_PASSWORD` | Senha do proxy | Não | — |

---

## Referência dos Endpoints REST

> Documentação interativa disponível em `http://IP_DO_SEU_SERVIDOR:8080/swagger/index.html` (requer licença ativa).

**Cabeçalho de autenticação:** `apikey: <GLOBAL_API_KEY>`

### Instâncias

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `POST` | `/instance/create` | Criar nova instância WhatsApp |
| `GET` | `/instance/{name}/qrcode` | Obter QR Code para pareamento (rota preferencial) |
| `GET` | `/instance/qrcode/{name}` | Variante compatível de QR Code |
| `GET` | `/instance/{name}/qr` | Variante compatível adicional de QR Code |
| `GET` | `/instance/connect/{name}` | Fallback legado de QR Code |
| `GET` | `/instance/{name}/status` | Verificar status da conexão |
| `GET` | `/instance/fetchInstances` | Listar todas as instâncias |
| `DELETE` | `/instance/{name}` | Deletar instância |
| `POST` | `/instance/{name}/logout` | Desconectar instância (mantém no banco) |
| `POST` | `/instance/{name}/restart` | Reiniciar instância |

### Mensagens

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `POST` | `/message/sendText` | Enviar mensagem de texto |
| `POST` | `/message/sendMedia` | Enviar mídia (imagem, vídeo, áudio, documento) |
| `POST` | `/message/sendAudio` | Enviar áudio como mensagem de voz |
| `POST` | `/message/sendReaction` | Enviar reação a uma mensagem |

### Webhook

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `POST` | `/webhook/{instanceName}` | Configurar webhook de uma instância |
| `GET` | `/webhook/{instanceName}` | Consultar configuração do webhook |

### Exemplos de payload

**Criar instância:**
```bash
curl -s -X POST http://IP:8080/instance/create \
  -H "apikey: SUA_GLOBAL_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"instanceName": "5562998808389", "integration": "WHATSAPP-BAILEYS", "token": "TOKEN_UNICO_DA_TENTATIVA"}'
```

> 📌 O campo `instanceName` deve ser o telefone/identificador digitado no PharmazapPlus (trim-only). O PharmazapPlus não usa o nome da farmácia/canal para criar a identidade da instância.

**Enviar mensagem de texto:**
```bash
curl -s -X POST http://IP:8080/message/sendText \
  -H "apikey: SUA_GLOBAL_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "number": "5511999999999",
    "text": "Olá! Seu pedido está pronto.",
    "instanceName": "SUA_CHAVE"
  }'
```

**Enviar imagem:**
```bash
curl -s -X POST http://IP:8080/message/sendMedia \
  -H "apikey: SUA_GLOBAL_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "number": "5511999999999",
    "mediatype": "image",
    "media": "https://url-da-imagem.com/foto.jpg",
    "caption": "Confira o comprovante",
    "instanceName": "SUA_CHAVE"
  }'
```

---

## Diferenças de API em relação à Evolution API (Node.js)

Se você está migrando da Evolution API (Node.js) para o Evolution Go, atente para as diferenças:

| Operação | Evolution API (Node.js) | Evolution Go |
|----------|------------------------|--------------|
| Autenticação | `AUTHENTICATION_API_KEY` / `apikey` header | `GLOBAL_API_KEY` / `apikey` header (mesmo header) |
| Obter QR Code | `GET /instance/connect/{name}` | `GET /instance/{name}/qrcode` *(preferencial)*, com fallback para `GET /instance/qrcode/{name}` e `GET /instance/{name}/qr` |
| Listar instâncias | `GET /instance/fetchInstances` | `GET /instance/fetchInstances` ✅ |
| Criar instância | `POST /instance/create` | `POST /instance/create` ✅ |
| Webhook global | `WEBHOOK_GLOBAL_URL` + `WEBHOOK_GLOBAL_ENABLED=true` | `WEBHOOK_URL` (sempre ativo se definido) |
| Status da instância | `GET /instance/connectionState/{name}` | `GET /instance/{name}/status` |
| Manager URL | `/manager` | `/manager/login` |

> 📌 **Para integração com o PharmazapPlus:** Verifique no serviço `WhatsAppService.cs` quais endpoints são chamados e compare com os endpoints do Evolution Go listados no Swagger. Caso haja divergência de caminhos, será necessário adaptar o serviço ou criar um adapter.

---

## Configuração do Nginx como Reverse Proxy

Para usar HTTPS (obrigatório em produção) e um domínio próprio, configure o Nginx na frente do Evolution Go.

### Instalar Nginx e Certbot

```bash
sudo apt install -y nginx certbot python3-certbot-nginx
```

### Configurar Virtual Host

```bash
sudo nano /etc/nginx/sites-available/evolution-go
```

```nginx
server {
    listen 80;
    server_name evo.sua-farmacia.com;   # ★ substitua pelo seu domínio

    # Redireciona HTTP → HTTPS
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name evo.sua-farmacia.com;   # ★ substitua pelo seu domínio

    # Certificado SSL (gerado pelo Certbot)
    ssl_certificate     /etc/letsencrypt/live/evo.sua-farmacia.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/evo.sua-farmacia.com/privkey.pem;

    # Configurações SSL seguras
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         HIGH:!aNULL:!MD5;

    # Proxy para o Evolution Go
    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;

        # Timeout generoso para conexões WebSocket e QR Code
        proxy_read_timeout 120s;
        proxy_send_timeout 120s;

        # Limite de tamanho para upload de mídia
        client_max_body_size 50M;
    }
}
```

```bash
# Ativar o site
sudo ln -s /etc/nginx/sites-available/evolution-go /etc/nginx/sites-enabled/

# Testar configuração
sudo nginx -t

# Recarregar Nginx
sudo systemctl reload nginx
```

### Gerar certificado SSL com Certbot

```bash
# Substitua pelo seu e-mail e domínio
sudo certbot --nginx -d evo.sua-farmacia.com --email voce@sua-farmacia.com --agree-tos --non-interactive

# Renovação automática (já configurada pelo Certbot, mas verifique)
sudo certbot renew --dry-run
```

Após o HTTPS funcionar, atualize o `.env`:

```env
WEBHOOK_URL=https://api.sua-farmacia.com/api/whatsapp/webhook
```

E atualize o canal no PharmazapPlus com a URL HTTPS:
- **URL da API:** `https://evo.sua-farmacia.com`

---

## Segurança

- **Nunca** exponha a `GLOBAL_API_KEY` em repositórios públicos ou logs.
- Use sempre HTTPS para a URL do Evolution Go e para o webhook.
- Altere a senha padrão do PostgreSQL (`<senha-forte>`) para algo forte.
- Limite o acesso ao Manager (`/manager/login`) por IP via Nginx, se possível:
  ```nginx
  location /manager {
      allow SEU_IP_FIXO;
      deny all;
      proxy_pass http://127.0.0.1:8080;
  }
  ```
- Mantenha a imagem Docker atualizada: `docker compose pull && docker compose up -d`
- Rotacione a `GLOBAL_API_KEY` periodicamente e atualize no PharmazapPlus via `/channels/create`.
- Ative o rate-limiting no Nginx para proteger a API contra abusos.
- O Evolution Go coleta **telemetria anônima** (rotas usadas, versão da API) — sem dados pessoais.

---

## Troubleshooting

| Sintoma | Causa provável | Solução |
|---------|---------------|---------|
| Endpoints retornam `503 Service Unavailable` | Licença não ativada | Acesse `/manager/login` e complete a ativação |
| Teste de conexão retorna `401 Unauthorized` | API Key incorreta | Verifique `GLOBAL_API_KEY` no `.env` e no cadastro do canal |
| Containers não sobem | PostgreSQL não inicializou | `docker compose logs postgres-evogo` — verifique erros de permissão no banco |
| Mensagens não chegam ao PharmazapPlus | Webhook mal configurado | Verifique `WEBHOOK_URL` e se a URL é acessível publicamente |
| QR Code não aparece | Licença inativa ou instância não criada | Ative a licença e crie a instância via `POST /instance/create` |
| QR Code expira antes de escanear | Timeout do WhatsApp | Chame novamente uma das rotas suportadas de QR (`/instance/{name}/qrcode`, `/instance/qrcode/{name}`, `/instance/{name}/qr` ou `/instance/connect/{name}`) para gerar um novo |
| `Connection refused` na porta 8080 | Container não está rodando | `docker compose ps` e `docker compose logs evolution-go` |
| Sessão perde ao reiniciar | Banco PostgreSQL com dados perdidos | Verifique se o volume `postgres_evogo_data` está persistido corretamente |
| Banco de dados não encontrado | Script `init-multiple-dbs.sh` não executou | Destrua o volume e recrie: `docker compose down -v && docker compose up -d` |
| `exec user process caused: exec format error` | Imagem incompatível com arquitetura | Ubuntu Server ARM? Use `evoapicloud/evolution-go:latest` (verifica suporte multi-arch) |
| Timeout no webhook | URL da API PharmazapPlus inacessível | Verifique firewall e se a API está rodando |

### Comandos úteis de diagnóstico

```bash
# Ver logs em tempo real
docker compose logs -f evolution-go

# Verificar uso de memória e CPU
docker stats evolution-go postgres-evogo

# Reiniciar apenas o Evolution Go (sem recriar banco)
docker compose restart evolution-go

# Verificar conectividade do webhook manualmente
curl -X POST https://api.sua-farmacia.com/api/whatsapp/webhook \
  -H "Content-Type: application/json" \
  -d '{"event":"test","data":{}}'

# Listar instâncias registradas
curl -s http://localhost:8080/instance/fetchInstances \
  -H "apikey: SUA_GLOBAL_API_KEY" | jq .

# Ver versão do Evolution Go em execução
curl -s http://localhost:8080/health -H "apikey: SUA_GLOBAL_API_KEY" | jq .

# Acessar o banco PostgreSQL diretamente
docker exec -it postgres-evogo psql -U evogo -d evogo_users
```

> ⚠️ **Deploy / validação pós-merge:** após publicar o fix no PharmazapPlus, limpe `bin/` e `obj/` antes do rebuild/redeploy para evitar DLL antiga mascarando a correção.

---

## Recursos e Links

| Recurso | Link |
|---------|------|
| 🌐 Site Evolution Foundation | <https://evolutionfoundation.com.br> |
| 📖 Documentação oficial | <https://docs.evolutionfoundation.com.br> |
| 🐙 Repositório GitHub | <https://github.com/evolution-foundation/evolution-go> |
| 🐳 Docker Hub | <https://hub.docker.com/r/evoapicloud/evolution-go> |
| 📋 Changelog | <https://github.com/evolution-foundation/evolution-go/blob/master/CHANGELOG.md> |
| 💬 Comunidade | <https://evolutionfoundation.com.br/community> |
| 📧 Suporte | <suporte@evofoundation.com.br> |
| 🔒 Biblioteca whatsmeow | <https://github.com/tulir/whatsmeow> |
| 📄 Documentação Evolution API (Node.js) | `docs/WHATSAPP-EVOLUTION.md` |
