# Changelog

Todas as mudanças relevantes deste projeto são documentadas aqui.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota [Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [Unreleased]

Primeira versão funcional do MVP. Ainda não houve _release_ com tag.

### Added

- **Configuração de pastas locais**: selecionar, adicionar e remover pastas a
  sincronizar, com validação de caminho e permissões; configuração persistida
  em SQLite.
- **Autenticação com o Google Drive** via OAuth 2.0, com _Client ID_/_Secret_
  lidos de um arquivo `.env` e token armazenado localmente.
- **Upload para o Google Drive** com criação automática de pastas, relatório de
  progresso e tratamento de falhas por arquivo.
- **Pasta raiz configurável no Drive** (`DriveFolderName`, padrão
  `DriveSyncPing`), editável na aba _Configurações_; valor em branco volta ao
  padrão. A estrutura passa a ser `<DriveFolderName>/<pasta local>/<organização>`.
- **Organização de arquivos** por extensão, por tipo (Imagens, Documentos,
  Vídeos, Áudios, Outros) ou por data (`yyyy-MM`), escolhida por pasta.
- **Detecção de duplicados** por hash SHA-256, comparado com o `appProperty`
  gravado nos uploads (com _fallback_ por tamanho); duplicados são ignorados e
  registrados.
- **Validação de upload**: relê o arquivo remoto e compara tamanho e hash antes
  de considerar o envio concluído.
- **Exclusão segura opcional** (`DeleteAfterUpload`): remove o arquivo local
  apenas após a validação; erros de I/O mantêm o arquivo e são registrados.
- **Registro de operações e histórico**: início/fim de sincronização, uploads,
  duplicados, validações, exclusões, cancelamentos e erros; abas _Histórico_ e
  _Erros_ na interface.
- **Retomada e cancelamento**: estado por arquivo persistido, _jobs_
  interrompidos marcados como falha na execução seguinte, arquivos já
  sincronizados são pulados e a sincronização pode ser cancelada com segurança
  via `CancellationToken`.
- **Modo simulação (dry-run)**: mostra o que seria enviado e excluído sem
  alterar nada, local ou remotamente.
- **Agendamento automático** (`ScheduleConfig` + `DriveSyncPing.Worker`):
  dias da semana e horário configuráveis, com verificação a cada minuto e
  proteção contra disparo duplo no mesmo _slot_.
- **Interface desktop** em Avalonia (MVVM) com abas _Pastas_, _Histórico_,
  _Erros_ e _Configurações_, botão de cancelamento e barra de progresso.
- **Metadados nos uploads**: além do hash, cada arquivo enviado recebe os
  `appProperties` `signature = "DriveSyncPing"` e `backupDate` (UTC ISO-8601).
- **Suíte de testes** (xUnit + FluentAssertions) cobrindo upload, duplicados,
  validação, exclusão, retomada, dry-run, agendamento, configurações e
  histórico, usando SQLite in-memory e um _fake_ do Google Drive.
- **Ferramentas de desenvolvimento**: `Makefile` com alvos de build, testes,
  execução, formatação e migrations; `CONTRIBUTING.md` com o fluxo de
  contribuição.

### Changed

- `GoogleDriveService` e `SyncService` reescritos com suporte a
  `CancellationToken`, leitura de metadados remotos e cache de subpastas por
  regra de organização.
- `GoogleAuthService` deixou de depender do `AppDbContext` (removida dependência
  não utilizada que quebrava a validação de escopo de DI no `Worker`).
- Migração de dados: nova migration `AddSyncFeatures` adiciona colunas de
  contadores/estado em `SyncJobs` e `SyncFiles` e cria a tabela
  `ScheduleConfigs`.

[Unreleased]: https://github.com/estevooliveira111/DriveSyncPing/commits/main
