sse conteúdo já está bem redondo para virar um README.md na raiz do projeto.
Segue em formato Markdown pronto:

# DbConnect

Aplicação C# para gerenciar conexões a bancos de dados, perfis de usuários e relatórios de análises.  
Tudo roda **localmente** (sem deploy externo), com suporte a **PostgreSQL via Docker** e **SQLite** para autenticação/cache de relatórios.

---

## Estrutura

- **DbConnect.Core** → Biblioteca de classes (modelos, serviços, abstrações)
- **DbConnect.Console** → Aplicativo de linha de comando para gerenciar perfis de conexão
- **DbConnect.Web** → API + UI (Minimal API + página HTML)
- **DbConnect.Tests** → Testes automatizados com xUnit + Testcontainers

---

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Docker](https://docs.docker.com/get-docker/) ou Docker Desktop
- Bash (para rodar o script `run-dev.sh`)

---

## Scripts disponíveis

### Subir tudo e iniciar o Web
```bash
./run-dev.sh up


Sobe o Postgres no Docker

Inicia o Web em hot-reload: http://localhost:5000

Rodar testes
./run-dev.sh test


Roda a suíte de testes (xUnit + Testcontainers)

Cria um container PostgreSQL temporário

Usar o console
./run-dev.sh console add localpg PostgreSql localhost 5432 appdb appuser apppass
./run-dev.sh console test localpg
./run-dev.sh console list

Ver logs do Postgres
./run-dev.sh logs

Parar containers
./run-dev.sh down

Apagar TUDO (containers + volume de dados do Postgres)
./run-dev.sh nuke

Listar os perfis salvos:

./run-dev.sh console list


Testar a conexão:

./run-dev.sh console test localpg


Se a saída for algo como:

✅ Conexão OK (PostgreSql)


./run-dev.sh console add localpg PostgreSql localhost 5433 postgres fgamajr senha
./run-dev.sh console → chama o app console via script.

add → subcomando para adicionar um perfil.

localpg → nome do perfil (apelido que você escolhe).

PostgreSql → tipo do banco (tem que bater com o enum DbKind: PostgreSql, SqlServer, MySql, Sqlite).

localhost → host.

5433 → porta (no seu caso você mapeou diferente de 5432, então correto).

postgres → database a conectar.

fgamajr → usuário do banco.

senha → senha do banco.

