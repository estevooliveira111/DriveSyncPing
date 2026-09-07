# DriveSyncPing — atalhos de desenvolvimento
#
# Uso: make <alvo>. Rode `make help` para ver a lista completa.

SOLUTION      := DriveSyncPing.slnx
APP_PROJECT   := DriveSyncPing.App
WORKER_PROJECT:= DriveSyncPing.Worker
TEST_PROJECT  := DriveSyncPing.Tests
EF_PROJECT    := DriveSyncPing.Infrastructure
CONFIG        ?= Debug

# Nome da migration: `make migrate name=AddAlgumaCoisa`
name ?=

.DEFAULT_GOAL := help

.PHONY: help restore build rebuild run run-worker test test-watch \
        format format-check migrate migrations-list db-update db-drop clean distclean ci

help: ## Mostra esta ajuda
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-16s\033[0m %s\n", $$1, $$2}'

restore: ## Restaura os pacotes NuGet
	dotnet restore $(SOLUTION)

build: restore ## Compila a solução ($(CONFIG))
	dotnet build $(SOLUTION) -c $(CONFIG) --no-restore

rebuild: clean build ## Limpa e recompila do zero

run: ## Executa o app desktop (Avalonia)
	dotnet run --project $(APP_PROJECT) -c $(CONFIG)

run-worker: ## Executa o serviço de agendamento em segundo plano
	dotnet run --project $(WORKER_PROJECT) -c $(CONFIG)

test: ## Roda a suíte de testes
	dotnet test $(TEST_PROJECT) -c $(CONFIG)

test-watch: ## Roda os testes em modo watch
	dotnet watch --project $(TEST_PROJECT) test

format: ## Aplica `dotnet format` na solução
	dotnet format $(SOLUTION)

format-check: ## Verifica formatação sem alterar arquivos
	dotnet format $(SOLUTION) --verify-no-changes

migrate: ## Cria uma migration EF Core: make migrate name=NomeDaMigration
	@if [ -z "$(name)" ]; then echo "Erro: informe name=NomeDaMigration"; exit 1; fi
	dotnet ef migrations add $(name) --project $(EF_PROJECT) --startup-project $(EF_PROJECT)

migrations-list: ## Lista as migrations existentes
	dotnet ef migrations list --project $(EF_PROJECT) --startup-project $(EF_PROJECT)

db-update: ## Aplica as migrations no banco SQLite local
	dotnet ef database update --project $(EF_PROJECT) --startup-project $(EF_PROJECT)

db-drop: ## Remove o banco SQLite local (pede confirmação)
	dotnet ef database drop --project $(EF_PROJECT) --startup-project $(EF_PROJECT)

clean: ## Remove artefatos de build (bin/obj)
	dotnet clean $(SOLUTION) 2>/dev/null || true
	find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +

distclean: clean ## clean + remove caches locais do dotnet
	rm -rf ~/.nuget/packages/.tools 2>/dev/null || true

ci: restore build test ## Pipeline usado na verificação de CI (adicione `format-check` quando o código estiver formatado)
