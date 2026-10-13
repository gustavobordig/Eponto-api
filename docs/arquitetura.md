# Revisão de arquitetura — EPonto API

API em .NET 8 com 77 rotas em 14 controllers, 15 services e 13 repositórios
(Dapper + MySQL), com um job diário no Hangfire. Este documento registra o
diagnóstico e o que foi feito na branch `revisao/seguranca-e-arquitetura`.

## Como está

```text
EPonto (controllers) → Application (services, DTOs) → Data (repositórios, SQL) → MySQL
                                   ↘ Domain (entidades e enums) ↙
```

- **Camadas:**
  - controllers finos chamam services, que chamam repositórios com SQL escrito à mão;
  - `DbSession` (scoped) abre a conexão MySQL por requisição e controla as transações;
  - as entidades em `Domain` são modelos de dados, sem comportamento.
- **Respostas:** envelope `{ sucesso, mensagem, ... }`, com o payload numa chave por recurso (`cargos`, `listaFerias`…).
- **Login:** em duas etapas, com JWT de 60 minutos e role `Admin` ou `User`.
- **Jobs:** o Hangfire processa o banco de horas todo dia às 20:30.

## Diagnóstico

### Segurança (resolvido nesta branch)

| # | Problema | Situação |
|---|----------|----------|
| S1 | `AlteraSenhaLogin` era pública e trocava a senha de qualquer e-mail sem conferir o código: qualquer pessoa tomava qualquer conta, inclusive de admin. | Resolvido: a troca exige o código, que vale 15 minutos, é de uso único e é anulado depois de 5 tentativas erradas. |
| S2 | O `appsettings.json` versionado (repositório público) tinha a senha dos dois bancos, a chave do JWT e o segredo do Cloudinary; o `LoginService` tinha a senha de app do Gmail. | Saíram do código e vêm de variáveis de ambiente. **As credenciais continuam no histórico do git e precisam ser trocadas** (ver [Ações fora do código](#ações-fora-do-código)). |
| S3 | Ações de admin com só `[Authorize]`: qualquer funcionário aprovava o próprio ajuste de ponto, via todos os registros, ausências e feedbacks, rodava o relatório e o processamento do banco de horas, e excluía ausências e feedbacks. | `Roles = "Admin"` nessas rotas. |
| S4 | Rotas com o id do usuário na URL ou no corpo não conferiam quem estava logado: trocando o número, via-se ou alterava-se o dado de outra pessoa (inclusive o próprio cargo, que define o salário). | Quem não é admin só acessa os próprios dados (`UsuarioLogado`), e na atualização do próprio cadastro cargo e jornada não mudam. |
| S5 | O login respondia "Email não localizado" ou "Senha incorreta", revelando quais e-mails existem; a recuperação de senha também. | Mesma mensagem nos dois casos, e a recuperação sempre responde sucesso. |
| S6 | O JWT não conferia emissor e público, embora o token fosse gerado com os dois. | Conferidos. |

### Qualidade (resolvido nesta branch)

- **Mensagens quebradas:** `Program.cs` e `PerfilService.cs` estavam em Windows-1252, e as mensagens de 401 e 403 chegavam com caracteres quebrados. Foram convertidos para UTF-8.
- **Pacotes sem uso:** `Swashbuckle 5.6.0` (do .NET Framework, só para um `using` sem uso), Google APIs, MailKit e `Microsoft.Data.SqlClient` saíram. Isso eliminou os 56 avisos `NU1701`.
- **DLLs versionadas:** as DLLs de build que estavam na raiz do repositório saíram do git.
- **CORS:** as origens estavam fixas em `localhost` e passaram para a configuração.
- **Pipeline:** `UseAuthentication` agora é explícito.
- **Testes:** primeiro projeto, com 12 testes de unidade da recuperação de senha e da regra de acesso.

### Pendências

**Contrato com o front**

1. O enum `StatusFeedback` tem os nomes trocados (`Finalizado = 0`, `Pendente = 1`), mas o banco usa 0 para pendente e 1 para respondida.
2. Atualizar usuário não grava `SENHA` nem `IND_ATIVO` (o service calcula o hash da senha e o `UPDATE` o ignora).
3. Feriado não tem rota de atualização; o front edita cadastrando um novo e excluindo o antigo.
4. Férias usam `DatIncioFerias` (com erro de digitação) no cadastro e na listagem.

**Robustez**

5. **99 respostas devolvem `ex.Message`** ao cliente, o que expõe detalhes internos (SQL, nomes de tabela). Um tratador global de exceções (middleware ou `ProblemDetails`) pode registrar o erro no log e devolver uma mensagem genérica.
6. **Falso async:** 10 métodos `async` não têm `await` (aviso CS1998), e 10 chamadas a repositório usam a versão síncrona do Dapper (`ExecuteReader`, `ExecuteScalar`). Isso segura uma thread por requisição; a troca é por `QueryAsync`/`ExecuteAsync`.
7. **Avisos de nullable:** 145 dos 162 avisos do build (principalmente CS8618, propriedades não inicializadas). Dá para inicializar com `= string.Empty` ou marcar como `string?` o que pode vir nulo do banco.
8. **Respostas inconsistentes:** algumas rotas devolvem o envelope e outras só `true`/`false` (recuperação de senha), algumas com 400 e outras com 404 para o mesmo tipo de erro.

**Estrutura**

9. **Os quatro projetos referenciam quase os mesmos pacotes** (Hangfire, Swashbuckle, Cloudinary, JWT…). `Domain` deveria depender só do .NET; hoje depende até de `Swashbuckle.AspNetCore.Filters`, por causa dos exemplos do Swagger.
10. **A tabela `LOGIN` guarda o JWT inteiro a cada login**, e também os códigos de recuperação. Ela cresce sem limite e mistura dois assuntos. Uma tabela de códigos com validade e tentativas substituiria o contador em memória (que hoje vale por instância da API).
11. **Deploy:**
    - há dois Dockerfiles idênticos (raiz e `EPonto/`);
    - o `railway.toml.txt`, com a extensão `.txt`, é ignorado pelo Railway;
    - o Swagger fica aberto em produção.

## Plano

### Fase 1 — segurança ✅

Itens S1 a S6, com testes.

### Fase 2 — limpeza e configuração ✅

Encoding, pacotes, DLLs, CORS e pipeline (itens de qualidade acima).

### Fase 3 — erros e async

Criar um tratador global de exceções e tirar o `ex.Message` das respostas. Passar os repositórios para Dapper async e remover o falso async. Padronizar o status HTTP de cada tipo de erro.

### Fase 4 — contrato e dados

Corrigir o enum `StatusFeedback` e a atualização de usuário (senha e situação), criar a rota de atualização de feriado e uma tabela própria para códigos de recuperação. Renomear `DatIncioFerias`; isso exige mudar o front ao mesmo tempo.

### Fase 5 — estrutura

Enxugar as dependências de cada projeto (`Domain` sem pacotes), tirar os exemplos do Swagger de `Domain`, zerar os avisos de nullable, deixar um Dockerfile só e fechar o Swagger em produção.

## Ações fora do código

Estas não dá para fazer por commit:

1. **Trocar todas as credenciais que estavam no repositório:**
   - senha dos dois bancos MySQL;
   - chave do JWT (trocar invalida os tokens atuais, o que é o desejado);
   - API secret do Cloudinary;
   - senha de app do Gmail.

   Apagar do código não basta: o histórico do git continua público.
2. **Configurar as variáveis de ambiente no Railway antes de publicar esta branch.** Sem `Jwt__Key` e as connection strings, a API não sobe.
3. Avaliar reescrever o histórico do git (por exemplo com `git filter-repo`) para remover os segredos antigos. Isso **não substitui** a troca das credenciais.
