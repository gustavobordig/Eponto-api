# EPonto API

API do ePonto, sistema de ponto eletrônico do nosso TCC, em .NET 8
(ASP.NET Core), Dapper e MySQL. O front (Next.js) fica no repositório
`eponto-tcc`.

## Rodando localmente

Requisitos: .NET SDK 8.0.407 (fixado no `global.json`) e um MySQL com o banco
do projeto.

Os segredos **não** ficam no `appsettings.json`, porque o repositório é
público. Configure-os com `dotnet user-secrets` (no desenvolvimento) ou com
variáveis de ambiente:

```bash
cd EPonto
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=...;Uid=...;Pwd=...;Allow User Variables=true;"
dotnet user-secrets set "ConnectionStrings:HangfireConnection" "Server=...;Allow User Variables=true;"
dotnet user-secrets set "Jwt:Key" "uma-chave-aleatoria-de-pelo-menos-32-caracteres"
dotnet user-secrets set "Cloudinary:CloudName" "..."
dotnet user-secrets set "Cloudinary:ApiKey" "..."
dotnet user-secrets set "Cloudinary:ApiSecret" "..."
dotnet user-secrets set "Email:Remetente" "conta@gmail.com"
dotnet user-secrets set "Email:Senha" "senha-de-app-do-gmail"
dotnet run
```

O Swagger fica em `/swagger`. Sem `Jwt:Key` com pelo menos 32 caracteres, a
API não sobe.

### Variáveis no deploy (Railway)

| Variável | Para quê |
|----------|----------|
| `ConnectionStrings__DefaultConnection` | Banco da aplicação |
| `ConnectionStrings__HangfireConnection` | Banco do Hangfire (jobs) |
| `Jwt__Key` | Chave de assinatura do JWT (mínimo 32 caracteres) |
| `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret` | Upload de arquivos |
| `Email__Remetente`, `Email__Senha` | Envio do código de recuperação de senha |
| `Cors__Origens__0`, `Cors__Origens__1`… | Endereços do front liberados no CORS (padrão: `http://localhost:3000`) |
| `PORT=8080` | Porta em que o Railway entrega as requisições (a imagem escuta na 8080) |
| `TZ=America/Sao_Paulo` | Fuso do container: os registros de ponto usam a hora local do servidor |

O `railway.toml` usa o `Dockerfile` da raiz. O banco precisa de `db/schema.sql`
(as tabelas); com o modo demonstração ligado, a API carrega os dados de
exemplo sozinha na primeira subida.

### Modo demonstração

Para deixar o app aberto para quem quiser testar, com as contas de teste do
[`db/seed.sql`](db/seed.sql) divulgadas. Ligado, ele:

- limita as gravações (POST, PUT, PATCH, DELETE) por usuário e no total, a cada
  hora, e as tentativas nas rotas de login por IP, a cada minuto (responde 429).
  Leituras não têm limite. O limite vale para qualquer usuário logado, para
  ninguém escapar dele criando contas novas;
- protege as contas divulgadas: não podem ser editadas, desativadas nem ter a
  senha trocada; e os perfis de acesso não podem ser alterados (responde 403);
- volta o banco para os dados de exemplo todo dia (as datas do seed são
  relativas a hoje).

| Variável | Padrão | Para quê |
|----------|--------|----------|
| `Demo__Ativo` | `false` | Liga o modo |
| `Demo__Emails__0`, `Demo__Emails__1`… | — | Contas divulgadas |
| `Demo__GravacoesPorUsuarioPorHora` | `30` | Cota de cada usuário |
| `Demo__GravacoesTotaisPorHora` | `500` | Teto somando todos |
| `Demo__TentativasDeLoginPorMinuto` | `10` | Por IP, nas rotas de login e senha |
| `Demo__CronRestauracao` | `0 4 * * *` | Quando restaurar (horário de São Paulo) |

## Testes

```bash
dotnet run --project EPonto.Tests
```

O projeto de testes usa xUnit v3 e roda como executável. Os testes cobrem a
recuperação de senha e a regra de acesso aos dados, sem precisar de banco (o
repositório de login é falso, em memória).

## Organização

```text
Domain/        entidades (modelos das tabelas) e enums
Data/          repositórios com Dapper e SQL; DbSession abre a conexão por requisição
Application/   services (regras), DTOs de resposta, interfaces e o job do Hangfire
EPonto/        controllers, Program.cs (DI, JWT, CORS, Swagger, Hangfire) e Autorizacao/
EPonto.Tests/  testes
```

O fluxo de uma requisição é controller → service → repositório. As respostas
seguem o envelope `{ sucesso, mensagem, ... }`.

## Login e acesso

O login tem duas etapas:

1. `POST /api/login/RealizarLogin` confere e-mail e senha e devolve os perfis
   do usuário.
2. `POST /api/login/AutenticarPerfil` recebe o perfil escolhido e devolve o
   JWT (60 minutos). O token traz o id do usuário (`sub`) e a role: `Admin`
   para perfil com acesso de administrador, `User` para os demais.

Nas rotas:

- `[Authorize(Roles = "Admin")]` protege as rotas de cadastro e as listagens
  gerais;
- nas rotas com o id do usuário, quem não é admin só acessa os próprios dados
  (`UsuarioLogado.PodeAcessarDadosDe` em `EPonto/Autorizacao/`).

A recuperação de senha manda um código de 6 dígitos por e-mail. O código vale
15 minutos, é de uso único e é anulado depois de 5 tentativas erradas.

A revisão de arquitetura e as pendências estão em
[docs/arquitetura.md](docs/arquitetura.md).
