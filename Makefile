DOTNET ?= dotnet
PROJECT := src/DotMad/DotMad.fsproj
TEST_PROJECT := tests/DotMad.Tests/DotMad.Tests.fsproj

.PHONY: all build test clean

all: build

build:
	$(DOTNET) build -c Release $(PROJECT)

test:
	$(DOTNET) run -c Release --project $(TEST_PROJECT)

clean:
	$(DOTNET) clean $(PROJECT)
	$(RM) -r src/DotMad/bin src/DotMad/obj tests/DotMad.Tests/bin tests/DotMad.Tests/obj
