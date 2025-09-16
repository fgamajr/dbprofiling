# DbConnect / Db-Profiling

Aplicação local em **C# (.NET 8)** + **React/Tailwind (frontend estático)** para gerenciar conexões com bancos de dados, salvar perfis de usuário e gerar relatórios de análises.  
Pensado para rodar **apenas em ambiente local** (sem deploy em nuvem).

---

## ✨ Funcionalidades

- **Sistema de usuários** (autenticação local via SQLite):
  - Registrar / Login
  - Alterar senha, deletar conta (em breve)
  - Suporte a avatar (em breve)
- **Gestão de perfis de conexão**:
  - PostgreSQL, SQL Server, MySQL, SQLite
  - Teste de conexão automático
  - Perfis persistidos por usuário
- **Relatórios**:
  - Cada análise gera e salva relatório (para não recalcular)
  - Listagem de relatórios por usuário
- **Frontend moderno**:
  - React + Tailwind
  - Avatar no topo com menu
  - Sidebar (Banco de dados, Tabelas, Relatórios, Perfis)
  - Cards bonitos e responsivos
  - Feedback visual (loading, toasts, erros)

---

## 🏗️ Estrutura

DbConnect.Core/ -> biblioteca central (.NET)
DbConnect.Console/ -> utilitário CLI
DbConnect.Web/ -> backend Web (ASP.NET Minimal API) + frontend React/Tailwind
run-dev.sh -> script para dev (docker + watch + testes)


---

## 🚀 Como rodar

### Requisitos
- .NET 8 SDK
- Docker
- Node.js (se quiser buildar frontend separado, opcional)
- Linux/WSL ou Mac (Windows funciona mas scripts foram feitos pensando em bash)

### Passos

1. Clone o repo:
   ```bash
   git clone https://github.com/fgamajr/db-profiling.git
   cd db-profiling


Suba tudo (Postgres via Docker + Web em hot reload):

./run-dev.sh up


Web: http://localhost:5000

Console CLI:

./run-dev.sh console add localpg PostgreSql localhost 5432 appdb appuser apppass
./run-dev.sh console test localpg
./run-dev.sh console list


Rodar testes:

./run-dev.sh test


Logs do Postgres:

./run-dev.sh logs


Encerrar tudo:

./run-dev.sh down


Limpar TUDO (containers + volume Postgres):

./run-dev.sh nuke

⚙️ Autenticação

Login/Registro via /api/auth/*

Cookies locais (dbconnect.auth)

SQLite em ~/.dbconnect/app.db (ignorado no git)

BCrypt para hash de senhas

🖥️ Frontend

Arquivo principal: DbConnect.Web/wwwroot/index.html

Stack:

React 18 (CDN)

Tailwind (Play CDN)

UI:

Avatar no topo (menu Minha conta/Sair)

Sidebar de navegação

Tela de perfis

Formulários com feedback visual

🔮 Roadmap

 Página Minha Conta (alterar senha, deletar conta, upload de avatar)

 Listagem de tabelas do banco

 Análises de qualidade dos dados

 Geração de relatórios HTML/PDF

 Export/Import de perfis