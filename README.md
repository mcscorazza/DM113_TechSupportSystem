# 💬 TechSuportSystem - Sistema de Suporte Técnico com gRPC

Este projeto foi desenvolvido como parte de um trabalho de pós-graduação, com o objetivo de aplicar os conceitos de sistemas distribuídos utilizando gRPC em C#.  
Ele simula um sistema de suporte técnico com abertura de chamados e chat em tempo real entre clientes e atendentes.

---

## 🎯 Objetivo

Implementar um sistema distribuído com:

- Comunicação via **gRPC** com C#
- Uso de **operação unária** (abertura de chamado)
- Uso de **streaming bidirecional** (chat entre cliente e atendente)
- Definição de serviço via `.proto` com **tipos personalizados**
- Armazenamento simples de dados (em arquivos `.txt` e `.log`)
- Aplicações de console para cliente e atendente

---

## 🛠️ Tecnologias

- [.NET 8](https://dotnet.microsoft.com/)
- [gRPC](https://grpc.io/)
- C#
- Protobuf (`.proto`)
- Console App (sem banco de dados)
- Armazenamento local com arquivos

---

## 🧱 Estrutura do Projeto

```plaintext
DM113_TechSupportSystem/
├── DM113_TechSupportSystem.Protos/          # Arquivo support.proto com a definição do serviço
├── DM113_TechSupportSystem.Server/          # Servidor gRPC que gerencia os tickets e mensagens
│   └── Logs/
│       ├── tickets.txt                      # Armazena os tickets abertos
│       └── Chats/                           # Logs de conversas por ticket
├── DM113_TechSupportSystem.ClientConsole/   # Cliente que abre ticket e conversa com atendente
├── DM113_TechSupportSystem.AgentConsole/    # Atendente que escolhe ticket e conversa com cliente
```

---

## ✍️ Como funciona

1. O **cliente** abre um chamado via console com seu nome e descrição do problema.
2. O servidor gera um `ticket_id` e armazena as informações no arquivo `tickets.txt`.
3. O **atendente** visualiza os chamados disponíveis e escolhe um para atender.
4. Inicia-se um **chat bidirecional em tempo real** entre cliente e atendente usando gRPC streaming.
5. As mensagens são registradas no arquivo `Logs/Chats/chat_<ticket_id>.log`.

---

## ▶️ Como executar

> Pré-requisito: .NET 8 instalado

### 1. Clone o repositório:

```bash
git clone https://github.com/mcscorazza/DM113_TechSupportSystem.git
cd DM113_TechSupportSystem
```

### 2. Restaure os pacotes

```bash
dotnet restore
```

### 3. Execute os projetos

Em três terminais separados:

```bash
# Terminal 1 - Servidor
cd DM113_TechSupportSystem.Server
dotnet run

# Terminal 2 - Cliente
cd DM113_TechSupportSystem.ClientConsole
dotnet run

# Terminal 3 - Atendente
cd DM113_TechSupportSystem.AgentConsole
dotnet run
```

---

## 📦 Exemplo de uso

### Cliente:
```
Seu nome: Marcos
Descrição do problema: Não consigo acessar o sistema
Ticket gerado: TKT-X8KJ2L

Iniciando chat...
[14:21:00] Marcos: Olá, estou com problema
```

### Atendente:
```
Chamados disponíveis:
ID: TKT-X8KJ2L | Usuário: Marcos | Descrição: Não consigo acessar o sistema

Digite o ticket_id: TKT-X8KJ2L
Seu nome (atendente): Ana

[14:21:05] Agent: Ana    : [Atendente Ana entrou no chat]
[14:21:08] Agent: Ana    : Oi Marcos, tudo bem? Em que posso ajudar?
```

---

## 📁 Armazenamento de dados

- 📄 `tickets.txt`: lista de chamados abertos com status
- 🗂️ `chat_<ticket_id>.log`: histórico completo do chat
- Nenhum banco de dados foi utilizado (requisito do projeto)

---

## ✅ Requisitos acadêmicos atendidos

- ✅ Comunicação síncrona (unária) com `OpenTicket`
- ✅ Comunicação assíncrona (streaming bidirecional) com `ChatSupport`
- ✅ Uso de `.proto` com tipos personalizados
- ✅ Aplicações cliente/servidor independentes
- ✅ Sistema funcional e testável via console

---

## 👨‍💻 Autores

- **Marcos Corazza**
- **Paulo Matheus**
- Projeto desenvolvido para a disciplina DM113

---

## 📄 Licença

Este projeto é acadêmico e de uso educacional. Fique à vontade para adaptar e reutilizar com os devidos créditos.

