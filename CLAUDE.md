# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Froola is an automated build and test tool for Unreal Engine code plugin projects that supports multi-platform deployment (Windows, Mac, Linux, Android, iOS). It's built with .NET 9.0 and C# 12 as a console application.

## Common Development Commands

```bash
# Build the project
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release

# Restore dependencies
dotnet restore

# Run the application
dotnet run -- [command] [options]

# Run after build
./Froola.exe [command] [options]  # Windows
./Froola [command] [options]       # Mac/Linux
```

## High-Level Architecture

### Core Components

1. **Commands** (`Froola/Commands/`)
   - `InitConfigCommand`: Generates configuration file templates
   - `PluginCommand`: Main command that orchestrates building, testing, and packaging UE plugins

2. **Builders** (`Froola/Builders/`)
   - Platform-specific build implementations using the builder pattern
   - `WindowsBuilder`: Native Windows builds
   - `MacBuilder`: SSH-based Mac/iOS builds
   - `LinuxBuilder`: Docker-based Linux builds
   - All builders implement `IBuilder` interface

3. **Runners** (`Froola/Runners/`)
   - Execution layer for different environments
   - `ProcessRunner`: Local process execution using ProcessX
   - `DockerRunner`: Docker container management
   - `MacUnrealEngineRunner`: SSH-based UE execution on Mac
   - `WindowsUnrealEngineRunner`: Native Windows UE execution

4. **Configuration** (`Froola/Configs/`)
   - Configuration POCOs for different aspects
   - Uses Microsoft.Extensions.Configuration for loading from `appsettings.json`
   - Platform-specific configs inherit from base configurations

5. **Utilities** (`Froola/Utils/`)
   - `GitClient`: Git operations wrapper
   - `SshConnection`: SSH connectivity for Mac builds
   - `FroolaLogger`: Centralized logging using ZLogger

### Key Architectural Patterns

- **Dependency Injection**: Uses Microsoft.Extensions.DependencyInjection throughout
- **Interface-Based Design**: All major components have interfaces for testability
- **Async/Await**: Consistent use of async patterns for I/O operations
- **Builder Pattern**: Complex build operations use builder pattern
- **Configuration-Driven**: Behavior controlled via JSON configuration or CLI args

### Cross-Platform Build Flow

1. Windows builds execute natively using local Unreal Engine installation
2. Mac/iOS builds connect via SSH to Mac machines with UE installed
3. Linux builds use Docker containers with UE Linux images
4. Android builds run on Windows with Android SDK
5. Results are aggregated and packaged into a unified output

## Testing Approach

- **Framework**: xUnit with test parallelization disabled
- **Mocking**: Moq with AutoFixture for test data generation
- **Coverage**: Coverlet for code coverage reporting
- **Test Structure**: Separate test classes for each component in `Froola.Tests/`
- **Test Helpers**: Custom AutoFixture extensions for common test scenarios

## CI/CD Pipeline

The project uses GitHub Actions with three workflows:

1. **CI** (`ci.yml`): Runs on PRs - builds, tests, and uploads results
2. **Release** (`release.yml`): Auto-creates releases when version changes in `Froola.csproj`
3. **Version PR** (`version-pr.yml`): Manual workflow to create version bump PRs

Releases automatically create a zip package containing the self-contained executable and configuration template.

## Important Development Notes

- Always maintain interface contracts when modifying builders or runners
- Platform-specific code should be isolated in respective builder/runner implementations
- Use structured logging with ZLogger for consistent output
- Configuration changes should be reflected in both config classes and `appsettings.json` template
- Test any SSH or Docker functionality with appropriate mocks
- Follow existing async patterns - avoid blocking calls in async methods