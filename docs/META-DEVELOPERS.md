# 🛠️ Meta for Developers - Guia de Configuração (WhatsApp & Instagram)

Este guia detalha o passo a passo para configurar aplicativos no painel [Meta for Developers](https://developers.facebook.com), essencial para obter as credenciais (Access Tokens, IDs) necessárias para o funcionamento do PharmazapPlus.

---

## 🚀 1. Criando o Aplicativo Base

O Meta atualiza sua interface frequentemente. Se não encontrar o botão imediatamente, siga este fluxo:

1. Acesse o portal [Meta for Developers](https://developers.facebook.com).
2. Se você já tem conta, clique em **Meus Aplicativos** (My Apps) no canto superior direito.
   - *Nota:* Se for seu primeiro acesso, você precisará clicar em **Começar** (Get Started) e realizar a verificação da conta (e-mail e telefone).
3. No painel de aplicativos, clique no botão central ou lateral **Criar aplicativo** (Create App).
4. **Seleção do Tipo de App (Interface Atualizada):**
   - Geralmente, para integrações de API como WhatsApp/Instagram, escolha a opção **Outro** (Other) ou **Casos de Uso** -> **Negócios** (Business).
   - Clique em **Próximo**.
5. Dê um nome ao seu App (ex: `Pharmazap Integration`) e confirme o e-mail de contato.
6. Clique em **Criar aplicativo**. Você pode ser solicitado a digitar sua senha do Facebook por segurança.

---

## 📱 2. WhatsApp Business API

O WhatsApp permite o uso de números de teste gratuitos para desenvolvimento e a migração para números reais de produção.

### 2.1. Configuração Inicial
1. No painel do seu App (Dashboard), role até a seção **Adicionar um produto ao seu aplicativo**.
2. Procure por **WhatsApp** e clique em **Configurar** (Set up).
3. Você será solicitado a associar uma **Conta de Negócios (Meta Business Suite)**. Se não tiver uma, crie uma nova durante o processo.

### 2.2. Usando Números de Teste (Sandbox)
O WhatsApp disponibiliza números de teste para que você comece a desenvolver sem custos imediatos.
1. No menu lateral esquerdo, vá em **WhatsApp** -> **Configuração da API** (API Setup).
2. Na seção **Enviar e receber mensagens**, você verá um "Número de telefone de teste".
3. **Importante:** Números de teste só podem enviar mensagens para números que você autorizar previamente.
   - Localize o campo **Para** (To), clique em **Gerenciar lista de números de telefone**.
   - Adicione o seu número pessoal (com DDI + DDD).
   - Você receberá um código de verificação no seu WhatsApp para validar a permissão.

### 2.3. Modelos de Mensagem (Message Templates)
Os modelos são mensagens pré-aprovadas pelo WhatsApp necessárias para iniciar conversas com clientes ou responder após a "janela de 24 horas" ter expirado.

- **O que são:** Estruturas de texto fixas com espaços para variáveis (ex: `Olá {{1}}, seu pedido {{2}} está pronto!`).
- **Onde gerenciar:** No painel do WhatsApp, vá em **Ferramentas de configuração** -> **Modelos de mensagem** (ou acesse diretamente pelo [Gerenciador do WhatsApp](https://business.facebook.com/latest/whatsapp_manager/template_library)).
- **Como testar:** 
  1. Na biblioteca de modelos, selecione um modelo aprovado (ou crie um novo e aguarde a aprovação).
  2. Use o botão **Enviar mensagem** (na tela de Configuração da API) selecionando o modelo desejado.
  3. Você pode preencher os valores das variáveis e enviar para o seu número de teste autorizado para validar o recebimento.
- **Importante:** O PharmazapPlus utiliza esses modelos para notificações automáticas e reabertura de conversas.

### 2.4. Configurando Número Real (Produção)
Para usar o número da farmácia na aplicação real:
1. **Prepare o número:** O número **não pode** estar ativo no app WhatsApp/Business do celular. Se estiver, apague a conta nas configurações do app.
2. Em **WhatsApp** -> **Configuração da API**, role até o fim e clique em **Adicionar número de telefone**.
3. Siga o fluxo de preenchimento (Nome, Categoria "Saúde", etc) e verifique via SMS ou Voz.
4. **Atenção:** Ao adicionar o número real, um **novo Phone Number ID** será gerado. Você deve atualizar este ID no PharmazapPlus.

### 2.5. Credenciais Necessárias para o PharmazapPlus
Ao cadastrar o canal no sistema, você precisará de:
- **ID do número de telefone (Phone Number ID):** Obtido na tela "Configuração da API". Verifique se é o ID do número de teste ou do real.
- **ID da conta do WhatsApp Business (WhatsApp Business Account ID):** Encontrado na mesma tela.
- **Token de Acesso (Access Token):** Para produção, use o **Token Permanente** (ver item 4).

---

## 📸 3. Instagram Graph API

A integração com Instagram requer que sua conta do Instagram seja do tipo **Profissional/Business** e esteja vinculada a uma **Página do Facebook**.

### 3.1. Configuração Inicial
1. No painel do App, vá em **Adicionar Produto** e procure por **Instagram UI**. Clique em **Configurar**.
2. Adicione também o produto **Messenger** (o Instagram Direct agora faz parte da infraestrutura do Messenger).

### 3.2. Vinculação de Conta
1. Vá para as configurações da sua **Página do Facebook**.
2. Encontre **Contas Vinculadas** e conecte sua conta do **Instagram Business**.
3. No painel de desenvolvedor, vá em **Messenger** -> **Configurações**.
4. Na seção **Contas e Páginas**, gere o token para a página vinculada ao Instagram.

### 3.3. Credenciais Necessárias para o PharmazapPlus
- **Instagram Business ID:** Pode ser obtido via Graph API Explorer ou nas configurações do canal no Meta Business Suite.
- **Access Token:** Token de página com permissões `instagram_basic`, `instagram_manage_messages` e `pages_manage_metadata`.

---

## 🔗 4. Webhooks (Recebimento de Mensagens)

Para que o PharmazapPlus receba as mensagens dos clientes em tempo real, você deve configurar o Webhook.

### 4.1. Configuração de Callback
1. No menu lateral do App, vá em **WhatsApp** -> **Configuração**.
2. Localize a seção **Webhooks** e clique em **Editar**.
3. **URL de Callback:** Insira a URL do seu servidor (ex: `https://sua-api.com/webhooks/whatsapp`). Se estiver testando localmente, use uma ferramenta como `ngrok`.
4. **Verificar token:** Insira a string de verificação definida nas variáveis de ambiente do seu sistema (ex: `WHATSAPP_VERIFY_TOKEN`).
5. Clique em **Salvar e verificar**.

### 4.2. Inscrição em Campos (WhatsApp)
1. Após salvar a URL, clique em **Gerenciar** na seção Webhooks.
2. Assine o campo **messages** para receber notificações de novas mensagens e `message_echoes` se quiser rastrear mensagens enviadas por outros meios.

### 4.3. Modo de Produção (Publicação do App)
- **Importante:** Enquanto o app estiver em "Modo de Desenvolvimento", ele só receberá webhooks de números de teste ou administradores.
- Para receber dados de produção de clientes reais, você deve alternar a chave no topo do painel para **Publicado** (Live).
- Para publicar, o Meta exigirá que você forneça uma **URL de Política de Privacidade** e um ícone para o App.

---

## 🔑 5. Tokens de Acesso Permanente (Produção)

Tokens temporários expiram em 24 horas. Para o sistema funcionar ininterruptamente:
1. Vá ao [Meta Business Suite (Configurações do Negócio)](https://business.facebook.com/settings/).
2. Vá em **Usuários** -> **Usuários do Sistema**.
3. Crie um novo usuário do sistema (Admin).
4. Clique em **Gerar Novo Token**.
5. Selecione o seu App e as permissões: `whatsapp_business_messaging`, `whatsapp_business_management`, `instagram_manage_messages` e `pages_manage_metadata`.
6. **Salve este token em um lugar seguro**, ele não será exibido novamente.

---

## ⚠️ Limitações Importantes
- **Janela de 24 Horas:** Você só pode enviar mensagens de "texto livre" se o cliente tiver enviado uma mensagem nas últimas 24 horas. Fora isso, devem ser usados **Modelos de Mensagem (Templates)**.
- **Instagram Direct:** Requer que o recurso "Permitir acesso às mensagens" esteja ativado nas configurações de privacidade do aplicativo Instagram no celular (Configurações -> Mensagens e respostas ao story -> Controles de mensagens).
- **Segurança:** Recomenda-se anexar um certificado de cliente às solicitações de webhook para validar que a origem é realmente a Meta.
