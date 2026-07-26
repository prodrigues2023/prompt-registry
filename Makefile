# Prompt Registry — local environment. One command each.

.PHONY: up down logs demo app build test

## up: build and start the registry + Postgres, wait until healthy
up:
	docker compose up -d --build
	@echo "waiting for the registry to be healthy..."
	@for i in $$(seq 1 30); do \
		curl -sf http://localhost:8080/health >/dev/null && { echo "registry is up on http://localhost:8080"; exit 0; }; \
		sleep 1; \
	done; \
	echo "registry did not become healthy in time"; docker compose logs registry; exit 1

## down: stop everything and drop the volume
down:
	docker compose down -v

## logs: tail the registry logs
logs:
	docker compose logs -f registry

## demo: run the full lifecycle — publish, test, promote, blocked regression, rollback
demo:
	./scripts/demo.sh

## app: run the example consumer (resolves prompt://checkout-summary@production live)
app:
	dotnet run --project samples/CheckoutSummarizer/CheckoutSummarizer.csproj

## build: compile the whole solution
build:
	dotnet build PromptRegistry.sln

## test: run the test suite
test:
	dotnet test PromptRegistry.sln
