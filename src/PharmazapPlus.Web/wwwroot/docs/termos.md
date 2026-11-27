# Termos de Uso e Política de Privacidade

**Pharmazap Plus — Plataforma de Atendimento para Farmácias**  
**Última atualização:** maio de 2026  
**Versão:** 1.0

---

## 1. Identificação do Controlador de Dados

O **Pharmazap Plus** é uma plataforma de atendimento digital desenvolvida e operada por **Leandro Khalil Peixoto**, doravante denominado **Controlador**, que assume a responsabilidade pelo tratamento dos dados pessoais coletados por meio desta plataforma, em conformidade com a **Lei Geral de Proteção de Dados Pessoais (LGPD — Lei nº 13.709, de 14 de agosto de 2018)**.

**Contato do Encarregado de Dados (DPO):**  
E-mail: khalilpdev@gmail.com

---

## 2. Aceitação dos Termos

Ao acessar e utilizar o Pharmazap Plus, o Administrador da Farmácia declara, em nome de sua organização, que:

- Leu e compreendeu estes Termos de Uso e Política de Privacidade na íntegra;
- Possui capacidade legal para firmar este aceite em nome da farmácia;
- Concorda com todas as disposições aqui contidas;
- Autoriza o tratamento dos dados pessoais descritos neste documento para as finalidades indicadas.

> **Este aceite é obrigatório e registrado em conformidade com o art. 7º, I da LGPD (consentimento) e o art. 11, II, a da LGPD.**

---

## 3. Dados Pessoais Coletados

O Pharmazap Plus coleta e trata as seguintes categorias de dados pessoais:

### 3.1 Dados dos Usuários da Plataforma (Colaboradores da Farmácia)

| Dado | Finalidade |
|------|-----------|
| Nome completo | Identificação do usuário |
| E-mail | Autenticação e comunicação |
| Telefone | Contato operacional |
| CPF (criptografado) | Verificação de identidade |
| Cargo/Função (Role) | Controle de acesso |
| Data e hora de login | Segurança e auditoria |
| Preferências de tema | Personalização da interface |

### 3.2 Dados dos Clientes da Farmácia

| Dado | Finalidade |
|------|-----------|
| Nome completo | Identificação do cliente |
| Número de WhatsApp | Canal de atendimento |
| E-mail | Comunicação (opcional) |
| CPF (quando fornecido) | Verificação de identidade |
| Histórico de mensagens | Registro de atendimento |
| Histórico de compras | Perfil e segmentação |
| Etiquetas e anotações | Gestão de relacionamento |

### 3.3 Dados da Farmácia

| Dado | Finalidade |
|------|-----------|
| Razão social e CNPJ | Identificação da organização |
| Endereço e contatos | Cadastro e configuração |
| Logotipo | Personalização da interface |
| Dados de integração (tokens de API) | Conectividade com canais (WhatsApp, Instagram) |

---

## 4. Bases Legais para o Tratamento

O tratamento dos dados pessoais no Pharmazap Plus fundamenta-se nas seguintes hipóteses legais previstas na LGPD:

- **Art. 7º, I** — Consentimento do titular (aceite explícito dos termos);
- **Art. 7º, II** — Cumprimento de obrigação legal ou regulatória;
- **Art. 7º, V** — Execução de contrato ou procedimentos preliminares;
- **Art. 7º, VI** — Exercício regular de direitos em processo judicial, administrativo ou arbitral;
- **Art. 7º, IX** — Legítimo interesse do controlador, quando não predominarem interesses ou direitos do titular.

---

## 5. Finalidades do Tratamento

Os dados pessoais coletados são utilizados exclusivamente para:

1. **Operação da plataforma**: autenticação, controle de acesso e funcionalidades de atendimento;
2. **Comunicação via WhatsApp e Instagram**: envio e recebimento de mensagens entre a farmácia e seus clientes;
3. **Gestão de relacionamento com clientes (CRM)**: histórico de atendimentos, segmentação e campanhas;
4. **Segurança e auditoria**: monitoramento de acessos, detecção de fraudes e logs de operação;
5. **Melhoria da plataforma**: análise de uso para aprimoramento de funcionalidades;
6. **Cumprimento de obrigações legais**: atendimento a requisições de autoridades competentes, quando aplicável.

**O Pharmazap Plus não realiza tratamento para finalidades secundárias incompatíveis sem novo consentimento.**

---

## 6. Compartilhamento de Dados com Terceiros

O Pharmazap Plus pode compartilhar dados com os seguintes terceiros, em caráter operacional:

| Terceiro | Dados Compartilhados | Finalidade |
|----------|----------------------|-----------|
| **Meta (WhatsApp Business API)** | Número de telefone, mensagens | Envio/recebimento de mensagens |
| **Meta (Instagram Graph API)** | Identificador Instagram (IGSID), mensagens | Atendimento via Instagram |
| **Evolution API (Não Oficial)** | Número de telefone, mensagens | Integração WhatsApp não oficial |
| **Serviços de Infraestrutura (Hosting)** | Dados de tráfego | Operação dos servidores |

> **Os terceiros acima estão sujeitos às suas próprias políticas de privacidade e termos de uso, que devem ser verificados pela farmácia antes de habilitar cada integração.**

O Pharmazap Plus **não vende, aluga ou cede** dados pessoais a terceiros para fins comerciais.

---

## 7. Armazenamento e Segurança dos Dados

### 7.1 Armazenamento

Os dados são armazenados em banco de dados PostgreSQL em servidores com acesso restrito. Tokens de integração (WhatsApp, Instagram) são armazenados de forma criptografada.

### 7.2 Medidas de Segurança

O Pharmazap Plus adota as seguintes medidas técnicas e administrativas:

- Autenticação por token JWT com expiração configurável;
- Senhas armazenadas com hash seguro (HMAC-SHA512 com salt individual);
- CPF dos colaboradores armazenado de forma criptografada;
- Comunicação via HTTPS/TLS;
- Controle de acesso baseado em funções (RBAC);
- Logs de auditoria para operações sensíveis.

### 7.3 Retenção de Dados

| Categoria | Período de Retenção |
|-----------|---------------------|
| Dados de clientes | Enquanto a farmácia estiver ativa, ou conforme solicitação de exclusão |
| Mensagens e atendimentos | Conforme configuração da farmácia (mínimo 90 dias para auditoria) |
| Logs de acesso | 90 dias |
| Dados de colaboradores | Até exclusão pela farmácia |

---

## 8. Direitos dos Titulares de Dados

Em conformidade com os arts. 17 a 22 da LGPD, os titulares de dados (clientes e colaboradores da farmácia) têm direito a:

| Direito | Descrição |
|---------|-----------|
| **Confirmação** | Saber se seus dados são tratados |
| **Acesso** | Obter cópia dos dados armazenados |
| **Correção** | Solicitar correção de dados incompletos ou imprecisos |
| **Anonimização/Exclusão** | Solicitar anonimização ou exclusão dos dados |
| **Portabilidade** | Receber os dados em formato estruturado |
| **Revogação do consentimento** | Revogar consentimento a qualquer momento |
| **Oposição** | Opor-se ao tratamento realizado com base em legítimo interesse |
| **Informação sobre compartilhamento** | Saber com quem os dados são compartilhados |

Para exercer qualquer desses direitos, entre em contato pelo e-mail: **khalilpdev@gmail.com**

O prazo de resposta é de **até 15 dias úteis**, conforme previsto na LGPD.

---

## 9. Responsabilidades da Farmácia (Operador)

A farmácia que utiliza o Pharmazap Plus atua como **operadora** dos dados pessoais de seus clientes, cabendo-lhe:

1. Informar seus clientes sobre o uso da plataforma para atendimento via WhatsApp/Instagram;
2. Obter o consentimento adequado dos clientes para tratamento de seus dados pessoais;
3. Não utilizar a plataforma para fins ilícitos, discriminatórios ou contrários à LGPD;
4. Garantir a segurança das credenciais de acesso à plataforma;
5. Comunicar ao Pharmazap Plus qualquer incidente de segurança que envolva dados da plataforma;
6. Designar um responsável interno pelo cumprimento da LGPD em sua organização.

---

## 10. Cookies e Armazenamento Local

O Pharmazap Plus utiliza **armazenamento local do navegador (localStorage)** para:

- Manter a sessão do usuário autenticado (token JWT);
- Armazenar preferências de tema e interface.

Nenhum cookie de rastreamento ou publicidade é utilizado.

---

## 11. Incidentes de Segurança

Em caso de incidente que possa acarretar risco ou dano relevante aos titulares, o Pharmazap Plus:

1. Notificará a **Autoridade Nacional de Proteção de Dados (ANPD)** dentro do prazo legal;
2. Comunicará os controladores afetados (farmácias) no menor prazo possível;
3. Adotará medidas para mitigar os efeitos do incidente.

---

## 12. Alterações neste Documento

O Pharmazap Plus se reserva o direito de atualizar estes Termos de Uso e Política de Privacidade a qualquer momento. Quando houver alterações relevantes:

- Os administradores de farmácias serão notificados na próxima sessão;
- Um novo aceite poderá ser solicitado caso as mudanças afetem substancialmente os direitos dos titulares.

---

## 13. Legislação Aplicável e Foro

Estes Termos são regidos pela **Lei Geral de Proteção de Dados Pessoais (LGPD — Lei nº 13.709/2018)**, pelo **Código de Defesa do Consumidor (CDC — Lei nº 8.078/1990)** e demais legislações aplicáveis da República Federativa do Brasil.

Para dirimir quaisquer controvérsias decorrentes destes Termos, fica eleito o foro da comarca do domicílio do Controlador.

---

## 14. Contato

**Encarregado de Proteção de Dados (DPO):** Leandro Khalil Peixoto  
**E-mail:** khalilpdev@gmail.com  
**Plataforma:** Pharmazap Plus  

> *Para dúvidas sobre privacidade, exercício de direitos de titulares ou incidentes de segurança, utilize o canal acima.*

---

*© 2026 Pharmazap Plus. Documento elaborado em conformidade com a Lei nº 13.709/2018 (LGPD).*
