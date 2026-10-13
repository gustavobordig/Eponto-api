#!/usr/bin/env bash
# Sobe o MySQL local de desenvolvimento (porta 3307). Na primeira vez, cria o
# banco a partir de db/schema.sql e db/seed.sql. Para recomeçar do zero, apague
# .local/data. Detalhes em db/README.md.
set -euo pipefail

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
MYSQL="$RAIZ/.local/mysql"
DADOS="$RAIZ/.local/data"
PORTA="${PORTA_MYSQL:-3307}"
SOCKET="$RAIZ/.local/mysql.sock"
CLIENTE=("$MYSQL/bin/mysql" --no-defaults -uroot -h127.0.0.1 -P"$PORTA")

if [ ! -x "$MYSQL/bin/mysqld" ]; then
  echo "MySQL não encontrado em $MYSQL. Veja db/README.md." >&2
  exit 1
fi

NOVO=0
if [ ! -d "$DADOS/mysql" ]; then
  rm -rf "$DADOS"
  "$MYSQL/bin/mysqld" --no-defaults --initialize-insecure --basedir="$MYSQL" --datadir="$DADOS"
  NOVO=1
fi

"$MYSQL/bin/mysqld" --no-defaults --basedir="$MYSQL" --datadir="$DADOS" \
  --port="$PORTA" --bind-address=127.0.0.1 --socket="$SOCKET" --mysqlx=OFF &
PID=$!
trap 'kill "$PID" 2>/dev/null' EXIT INT TERM

until "$MYSQL/bin/mysqladmin" --no-defaults -uroot -h127.0.0.1 -P"$PORTA" ping >/dev/null 2>&1; do sleep 1; done

if [ "$NOVO" = 1 ]; then
  echo "Criando o banco de teste…"
  "${CLIENTE[@]}" < "$RAIZ/db/schema.sql"
  "${CLIENTE[@]}" < "$RAIZ/db/seed.sql"
  "${CLIENTE[@]}" -e "
    CREATE USER 'eponto'@'127.0.0.1' IDENTIFIED BY 'eponto-local';
    CREATE USER 'eponto'@'localhost' IDENTIFIED BY 'eponto-local';
    GRANT ALL ON eponto.* TO 'eponto'@'127.0.0.1', 'eponto'@'localhost';
    GRANT ALL ON eponto_hangfire.* TO 'eponto'@'127.0.0.1', 'eponto'@'localhost';"
  echo "Banco criado. Contas de teste em db/seed.sql."
fi

echo "MySQL pronto em 127.0.0.1:$PORTA"
wait "$PID"
