# Banco local de desenvolvimento

MySQL 8.4 rodando na própria pasta do projeto, com o esquema do ePonto e dados
de teste. Nada é instalado no sistema: binários e dados ficam em `.local/`,
que está no `.gitignore`.

| Arquivo | O que é |
|---------|---------|
| `db/schema.sql` | Esquema das 20 tabelas, reconstruído a partir do SQL dos repositórios (o repositório não tinha o esquema) |
| `db/seed.sql` | Perfis, cargos, jornadas, feriados, férias, pontos e solicitações de teste, além das contas de teste |
| `scripts/banco-local.sh` | Sobe o MySQL na porta 3307; na primeira vez, cria o banco com o esquema e o seed |
| `scripts/api-local.sh` | Sobe a API em `http://localhost:5283` apontando para esse banco |

## Contas de teste

As contas e senhas estão no topo de [`seed.sql`](seed.sql):

- **RH (admin):** tem os perfis Administrador e Funcionário, então o login pede para escolher.
- **Ana (funcionária):** a RH é a chefe dela.

As senhas só valem neste banco local.

## Primeira vez

1. Baixe o MySQL 8.4 para `.local/mysql`:

   ```bash
   mkdir -p .local && cd .local
   curl -LO https://cdn.mysql.com/Downloads/MySQL-8.4/mysql-8.4.10-macos15-arm64.tar.gz
   tar xzf mysql-8.4.10-macos15-arm64.tar.gz && mv mysql-8.4.10-macos15-arm64 mysql && rm mysql-*.tar.gz
   cd ..
   ```

2. Tenha o .NET SDK 8 instalado (`brew install --cask dotnet-sdk`). O `api-local.sh` também usa um SDK em `.local/dotnet`, se existir.

## Uso

Em dois terminais, na raiz do repositório:

```bash
bash scripts/banco-local.sh   # MySQL em 127.0.0.1:3307
bash scripts/api-local.sh     # API em http://localhost:5283 (Swagger em /swagger)
```

No front (`eponto-tcc`), aponte para a API local no `.env.local`:

```bash
NEXT_PUBLIC_API_URL=http://localhost:5283
```

Depois rode `npm run build && npm start`, ou `npm run dev`. O CORS da API local libera `http://localhost:3000` e `http://localhost:3005`.

## Recomeçar do zero

Pare o `banco-local.sh` e apague os dados. Na próxima vez que subir, o banco é recriado com o esquema e o seed:

```bash
rm -rf .local/data
```

Os pontos e saldos de teste usam datas relativas ao dia em que o banco foi criado.

## O que não funciona localmente

- **Upload de arquivos** (anexo de ausência, foto de perfil): precisa das credenciais do Cloudinary (`Cloudinary__*`).
- **E-mail de recuperação de senha:** precisa de `Email__Remetente` e `Email__Senha`. Sem eles, `RecuperarSenha` falha. Para testar a troca de senha, consulte o código gerado na tabela `LOGIN` (`COD_RECUPERACAO`).

## Migrações

Mudanças no esquema depois que um banco já foi criado ficam em
[`db/migracoes/`](migracoes/), em ordem de data. O `schema.sql` já inclui
todas; rode a migração só em bancos antigos (local ou Railway):

| Arquivo | O que muda |
|---------|------------|
| `2026-10-14-observacao-resposta.sql` | Comentário opcional na resposta de ajustes, ausências e férias |
