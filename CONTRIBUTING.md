# Contribuindo com o DriveSyncPing

Obrigado pelo interesse em contribuir! Este documento reúne o que você precisa
para rodar o projeto localmente, o padrão de código adotado e o fluxo de
_pull requests_.

## Pré-requisitos

- **.NET SDK 10.0** ou superior (`dotnet --version`)
- **make** (opcional, mas os atalhos deste guia usam o `Makefile`)
- Uma conta Google com a **Google Drive API** habilitada e um par
  _Client ID_ / _Client Secret_ do tipo **Desktop app**

## Configuração inicial

1. Clone o repositório e entre na pasta:

   ```bash
   git clone https://github.com/estevooliveira111/DriveSyncPing.git
   cd DriveSyncPing
   ```

2. Crie um arquivo `.env` na raiz (ele está no `.gitignore`, nunca faça commit):

   ```env
   GOOGLE_CLIENT_ID="seu-client-id.apps.googleusercontent.com"
   GOOGLE_CLIENT_SECRET="seu-client-secret"
   ```

3. Restaure, compile e rode os testes:

   ```bash
   make restore
   make build
   make test
   ```

4. Execute a aplicação:

   ```bash
   make run          # app desktop (Avalonia)
   make run-worker   # serviço de agendamento em segundo plano
   ```

O banco SQLite e o token OAuth são criados em
`~/.local/share/DriveSyncPing/` (Linux/macOS) ou `%LOCALAPPDATA%\DriveSyncPing\`
(Windows).

## Estrutura do projeto

O projeto segue **Clean Architecture**; a dependência sempre aponta para dentro:

| Projeto                        | Responsabilidade |
|--------------------------------|------------------|
| `DriveSyncPing.Domain`         | Entidades e enums de negócio. Sem dependências externas. |
| `DriveSyncPing.Application`    | Interfaces de serviço, DTOs e opções de caso de uso. |
| `DriveSyncPing.Infrastructure`| EF Core/SQLite, Google Drive API, acesso a arquivos, migrations. |
| `DriveSyncPing.App`           | Interface desktop (Avalonia + MVVM com CommunityToolkit.Mvvm). |
| `DriveSyncPing.Worker`        | `BackgroundService` que dispara a sincronização agendada. |
| `DriveSyncPing.Tests`         | xUnit + FluentAssertions + SQLite in-memory + fakes. |

Regras práticas:

- Novos contratos de serviço vivem em `Application/Services`; a implementação
  vai em `Infrastructure/Services`.
- `Domain` nunca referencia `Application`, `Infrastructure`, EF Core ou
  bibliotecas do Google.
- A UI conversa apenas com as interfaces de `Application`.

## Banco de dados e migrations

Ao alterar uma entidade, gere a migration correspondente:

```bash
make migrate name=DescricaoCurtaDaMudanca
```

Isso equivale a:

```bash
dotnet ef migrations add DescricaoCurtaDaMudanca \
  --project DriveSyncPing.Infrastructure \
  --startup-project DriveSyncPing.Infrastructure
```

Faça commit dos três arquivos gerados em `Infrastructure/Migrations/`
(migration, `.Designer.cs` e o snapshot atualizado). As migrations são
aplicadas automaticamente no _startup_ do `App` e do `Worker`.

## Padrão de código

- **Formatação:** rode `make format` nos arquivos que você tocou antes de abrir
  o PR (`make format-check` roda `dotnet format --verify-no-changes`; o código
  ainda tem desvios legados, então não o adicione ao `make ci` até que a base
  esteja formatada).
- **Nomenclatura:** `PascalCase` para tipos/membros públicos, `_camelCase`
  para campos privados, `camelCase` para locais e parâmetros.
- **Nullable reference types** estão habilitados em todos os projetos —
  trate os avisos, não os suprima.
- **Async:** métodos assíncronos terminam em `Async` e recebem
  `CancellationToken` quando fazem I/O.
- **Idioma:** mensagens exibidas ao usuário e logs em **português**;
  nomes de código em **inglês**.
- Escreva teste para todo comportamento novo de serviço. Os testes usam
  `TestDatabase` (SQLite `:memory:`) e `FakeGoogleDriveService` — sem rede.

## Mensagens de commit

Usamos **[Conventional Commits](https://www.conventionalcommits.org/)**:

```
<tipo>: <resumo no imperativo>

[corpo opcional explicando o porquê]
```

Tipos comuns: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `build`.

Exemplo: `feat: permitir nome personalizado da pasta raiz no Drive`

## Fluxo de Pull Request

1. Crie um branch a partir de `main`: `git switch -c feat/minha-mudanca`.
2. Faça as alterações com commits pequenos e descritivos.
3. Garanta que `make ci` passa (restore + build + testes + formatação).
4. Atualize a seção `[Unreleased]` do [`CHANGELOG.md`](CHANGELOG.md).
5. Abra o PR descrevendo **o que** mudou e **por quê**; referencie a _issue_
   relacionada, se houver.
6. O PR precisa de build verde e revisão aprovada para ser mesclado
   (_squash merge_ preferencial).

## Reportando bugs

Abra uma _issue_ com: passos para reproduzir, comportamento esperado x obtido,
versão do .NET e sistema operacional, e trechos relevantes de log (sem incluir
`GOOGLE_CLIENT_SECRET` nem tokens).
