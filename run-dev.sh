#!/usr/bin/env bash
set -euo pipefail

# -----------------------
# Configuração
# -----------------------
WEB_PROJECT="DbConnect.Web"
CONSOLE_PROJECT="DbConnect.Console"
ASPNETCORE_URLS_DEFAULT="http://localhost:5000"

compose_up() {
  echo "==> Subindo Postgres via docker compose..."
  docker compose up -d
  echo "==> Aguardando healthcheck do Postgres..."
  # espera até o container estar saudável (health: healthy)
  for i in {1..40}; do
    status=$(docker inspect -f '{{.State.Health.Status}}' pg-local 2>/dev/null || echo "unknown")
    if [[ "$status" == "healthy" ]]; then
      echo "==> Postgres OK (healthy)."
      return 0
    fi
    if [[ "$status" == "unhealthy" ]]; then
      echo "!! Postgres marcou unhealthy; verifique 'docker logs -f pg-local'."
      return 1
    fi
    sleep 1
  done
  echo "!! Timeout aguardando Postgres. Veja 'docker logs -f pg-local'."
  exit 1
}

run_web() {
  echo "==> Iniciando Web com dotnet watch (hot-reload)..."
  export DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1
  export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:5000}"
  echo "    URL: ${ASPNETCORE_URLS}"
  dotnet watch --project "$WEB_PROJECT" run --urls "${ASPNETCORE_URLS}"
}

run_fullstack() {
  echo "==> Iniciando Backend + Frontend..."

  # Primeiro mata qualquer processo nas portas
  kill_ports

  # Inicia o backend em background
  echo "==> Iniciando Backend (C#)..."
  export DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1
  export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:5000}"
  echo "    Backend URL: ${ASPNETCORE_URLS}"
  dotnet watch --project "$WEB_PROJECT" run --urls "${ASPNETCORE_URLS}" &
  BACKEND_PID=$!

  # Aguarda um pouco para o backend iniciar
  sleep 5
  
  # Inicia o React frontend
  echo "==> Iniciando Frontend (React)..."
  cd DbConnect.React
  echo "    Frontend URL: http://localhost:8080"
  npm run dev &
  FRONTEND_PID=$!
  
  # Trap para limpar os processos ao sair
  trap "echo '==> Parando serviços...'; kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; exit" INT TERM EXIT
  
  echo "==> ✅ Serviços iniciados!"
  echo "    📱 Frontend: http://localhost:8080"
  echo "    🔧 Backend:  ${ASPNETCORE_URLS}"
  echo "    💾 Database: PostgreSQL na porta ${DB_HOST_PORT:-5432}"
  echo ""
  echo "⚡ Pressione Ctrl+C para parar todos os serviços"
  
  # Aguarda os processos
  wait
}


run_console_usage() {
  cat <<EOF
Uso do console:
  $0 console list
  $0 console add <name> <kind> <host> <port> <database> <username> [password]
  $0 console test <name>

Exemplos:
  $0 console add localpg PostgreSql localhost 5432 appdb appuser apppass
  $0 console test localpg
EOF
}

run_console() {
  if [[ $# -lt 1 ]]; then
    run_console_usage
    exit 1
  fi
  echo "==> Executando Console: $*"
  dotnet run --project "$CONSOLE_PROJECT" -- "$@"
}

run_tests() {
  echo "==> Executando testes (xUnit + Testcontainers)..."
  dotnet test
}

show_logs() {
  echo "==> Logs do Postgres (CTRL+C para sair)"
  docker logs -f pg-local
}

compose_down() {
  echo "==> Parando containers (docker compose down)..."
  docker compose down
}

kill_ports() {
  echo "==> Matando processos nas portas 5000, 5001, 8000, 8001, 8080..."
  local ports=(5000 5001 8000 8001 8080)

  for port in "${ports[@]}"; do
    local pids=$(lsof -ti:$port 2>/dev/null || true)
    if [[ -n "$pids" ]]; then
      echo "    Matando processo(s) na porta $port: $pids"
      kill $pids 2>/dev/null || true
      sleep 1
      # Force kill se ainda estiver rodando
      local remaining=$(lsof -ti:$port 2>/dev/null || true)
      if [[ -n "$remaining" ]]; then
        echo "    Force kill na porta $port: $remaining"
        kill -9 $remaining 2>/dev/null || true
      fi
    else
      echo "    Porta $port: livre"
    fi
  done
  echo "==> Portas limpas!"
}

nuke_everything() {
  echo "!! ATENÇÃO: isso vai apagar o volume 'pgdata' e parar tudo."
  read -r -p "Confirmar? [y/N] " yn
  case "$yn" in
    [Yy]* )
      docker compose down -v
      echo "==> Volume e containers removidos."
      ;;
    * )
      echo "Abortado."
      ;;
  esac
}

print_help() {
  cat <<EOF
run-dev.sh — utilitário de desenvolvimento

Comandos:
  up         🚀 Sobe Postgres + Backend + Frontend (fullstack completo)
  web        (apenas) inicia o Web backend (requer Postgres já rodando)
  fullstack  🚀 Alias para 'up' (Postgres + Backend + Frontend)
  test       Roda a suíte de testes (usa Testcontainers; sobe Postgres temporário)
  console    Executa o app de console (subcomandos: list | add | test)
  logs       Mostra os logs do Postgres
  down       Para os containers (docker compose down)
  kill-ports Mata processos nas portas 5000, 5001, 8000, 8001, 8080
  nuke       Para e apaga o volume de dados do Postgres (cuidado!)
  help       Mostra esta ajuda

Atalhos principais:
  ./run-dev.sh up          # 🚀 Stack completa (recomendado para desenvolvimento)
  ./run-dev.sh web         # Apenas backend C#
  ./run-dev.sh test        # Testes automatizados
  ./run-dev.sh console add localpg PostgreSql localhost 5432 appdb appuser apppass

URLs de desenvolvimento:
  📱 Frontend React: http://localhost:8080
  🔧 Backend API:    http://localhost:5000
  💾 PostgreSQL:     localhost:5432
EOF
}

# -----------------------
# Dispatcher
# -----------------------
cmd="${1:-help}"
shift || true

case "$cmd" in
  up)
    compose_up
    run_fullstack
    ;;
  web)
    run_web
    ;;
  fullstack)
    compose_up
    run_fullstack
    ;;
  test)
    run_tests
    ;;
  console)
    run_console "$@"
    ;;
  logs)
    show_logs
    ;;
  down)
    compose_down
    ;;
  kill-ports)
    kill_ports
    ;;
  nuke)
    nuke_everything
    ;;
  help|--help|-h)
    print_help
    ;;
  *)
    echo "Comando desconhecido: $cmd"
    echo
    print_help
    exit 1
    ;;
esac
