#!/usr/bin/env bash
# Sobe a API em http://localhost:5283 apontando para o banco local
# (scripts/banco-local.sh). Os valores abaixo são só de desenvolvimento.
set -euo pipefail

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
PORTA_MYSQL="${PORTA_MYSQL:-3307}"
CONEXAO="Server=127.0.0.1;Port=$PORTA_MYSQL;Uid=eponto;Pwd=eponto-local;Allow User Variables=true;"

export ConnectionStrings__DefaultConnection="${CONEXAO}Database=eponto;"
export ConnectionStrings__HangfireConnection="${CONEXAO}Database=eponto_hangfire;"
# Chave só para o ambiente local; em produção, Jwt__Key vem das variáveis do Railway
export Jwt__Key="${JWT_KEY_LOCAL:-chave-local-de-desenvolvimento-do-eponto-0123456789}"
export Cors__Origens__0="http://localhost:3000"
export Cors__Origens__1="http://localhost:3005"
export ASPNETCORE_ENVIRONMENT=Development

# Usa o SDK de .local/ quando existir; senão, o dotnet instalado na máquina
if [ -f "$RAIZ/.local/dn.sh" ]; then
  # shellcheck source=/dev/null
  . "$RAIZ/.local/dn.sh"
fi

cd "$RAIZ"
exec dotnet run --project EPonto/EPonto.csproj --no-launch-profile --urls http://localhost:5283
