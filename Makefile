DOTNET ?= dotnet
CONFIGURATION ?= Release
PLUGIN_PROJECT := RepairMe/RepairMe.csproj
TEST_PROJECT := RepairMe.Tests/RepairMe.Tests.csproj

# Dalamud's dev hooks folder. Used by `make fetch-dalamud` and by the test project (see its csproj for defaults).
DALAMUD_HOME ?=
DALAMUD_URL := https://goatcorp.github.io/dalamud-distrib/latest.zip

ACTIONLINT_VERSION := 1.7.7
ACTIONLINT_IMAGE := docker.io/rhysd/actionlint:$(ACTIONLINT_VERSION)

.DEFAULT_GOAL := help
.PHONY: help build test lint format validate-workflows fetch-dalamud ci clean

help: ## List the available targets
	@awk 'BEGIN {FS = ":.*## "} /^[a-zA-Z_-]+:.*## / {printf "  %-20s %s\n", $$1, $$2}' $(MAKEFILE_LIST)

build: ## Build the plugin
	$(DOTNET) build $(PLUGIN_PROJECT) -c $(CONFIGURATION) --nologo

test: ## Run the unit tests
	$(DOTNET) test $(TEST_PROJECT) --nologo

lint: ## Check code style and fail on compiler warnings
	$(DOTNET) format style $(PLUGIN_PROJECT) --verify-no-changes --severity warn
	$(DOTNET) format style $(TEST_PROJECT) --verify-no-changes --severity warn
	$(DOTNET) build $(PLUGIN_PROJECT) -c $(CONFIGURATION) --nologo -warnaserror

format: ## Apply code style fixes
	$(DOTNET) format style $(PLUGIN_PROJECT) --severity warn
	$(DOTNET) format style $(TEST_PROJECT) --severity warn

validate-workflows: ## Lint the GitHub Actions workflow files with actionlint
	@if command -v actionlint >/dev/null 2>&1; then \
		actionlint; \
	elif command -v podman >/dev/null 2>&1; then \
		podman run --rm -v "$(CURDIR):/repo:Z" -w /repo $(ACTIONLINT_IMAGE); \
	elif command -v docker >/dev/null 2>&1; then \
		docker run --rm -v "$(CURDIR):/repo" -w /repo $(ACTIONLINT_IMAGE); \
	else \
		echo "actionlint, podman or docker is required to validate the workflows" >&2; \
		exit 1; \
	fi

fetch-dalamud: ## Download Dalamud into DALAMUD_HOME (used by CI)
	@test -n "$(DALAMUD_HOME)" || { echo "Set DALAMUD_HOME to the folder to download Dalamud into" >&2; exit 1; }
	@if [ -f "$(DALAMUD_HOME)/Dalamud.dll" ]; then \
		echo "Dalamud already present in $(DALAMUD_HOME)"; \
	else \
		mkdir -p "$(DALAMUD_HOME)" && \
		curl -fsSL -o "$(DALAMUD_HOME)/dalamud.zip" $(DALAMUD_URL) && \
		unzip -q "$(DALAMUD_HOME)/dalamud.zip" -d "$(DALAMUD_HOME)" && \
		rm "$(DALAMUD_HOME)/dalamud.zip"; \
	fi

ci: build test lint validate-workflows ## Everything CI runs

clean: ## Remove build output
	$(DOTNET) clean $(PLUGIN_PROJECT) -c $(CONFIGURATION) --nologo
	$(DOTNET) clean $(TEST_PROJECT) --nologo
