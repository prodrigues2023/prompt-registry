# Prompt Registry — local environment. One command each.

.PHONY: up down logs seed demo regression drills rollback-drill fleet-drill fallback-drill app build test

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

## seed: load development sample data (2 namespaces, 2 services each) into the running registry
seed:
	docker compose exec -T postgres sh -c 'psql -U "$$POSTGRES_USER" -d "$$POSTGRES_DB"' < db/seed/dev_seed.sql
	@echo "seeded. try: curl 'http://localhost:8080/prompts?namespace=checkout'"

## demo: run the full lifecycle — publish, test, promote, blocked regression, rollback
demo:
	./scripts/demo.sh

## regression: run the golden-set harness — a caught regression blocks a promotion (Milestone 4)
regression:
	./scripts/regression.sh

## drills: run the self-contained validation drills (fleet + fallback; no server needed)
drills: fleet-drill fallback-drill

## rollback-drill: measure how fast a rollback reaches a consumer (needs `make up`)
rollback-drill:
	dotnet run --project src/PromptRegistry.Drills -- rollback

## fleet-drill: two instances briefly disagree during a refresh, then converge
fleet-drill:
	dotnet run --project src/PromptRegistry.Drills -- fleet

## fallback-drill: registry down at cold start serves bundled; warm serves stale
fallback-drill:
	dotnet run --project src/PromptRegistry.Drills -- fallback

## app: run the example consumer (resolves prompt://checkout-summary@production live)
app:
	dotnet run --project samples/CheckoutSummarizer/CheckoutSummarizer.csproj

## build: compile the whole solution
build:
	dotnet build PromptRegistry.sln

## test: run the test suite
test:
	dotnet test PromptRegistry.sln
