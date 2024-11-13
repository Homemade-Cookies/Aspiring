# Aspiring

## Overview

Aspiring is a project designed to provide a comprehensive solution for managing and monitoring distributed applications. It includes features such as health checks, service discovery, resilience, and OpenTelemetry integration.

## Setup Instructions

To get the project up and running, follow these steps:

1. Clone the repository:
   ```sh
   git clone https://github.com/yourusername/aspiring.git
   cd aspiring
   ```

2. Build the project:
   ```sh
   dotnet build
   ```

3. Run the project:
   ```sh
   dotnet run --project Aspiring.AppHost
   ```

4. Run the tests:
   ```sh
   dotnet test
   ```

5. Access the health checks UI:
   ```sh
   http://localhost:8080/healthchecks-ui
   ```

6. View the Grafana dashboard:
   ```sh
   http://localhost:3000
   ```

## Usage Examples

Here are some examples of how to use the project:

- To access the health checks UI, navigate to `http://localhost:8080/healthchecks-ui`.
- To view the Grafana dashboard, navigate to `http://localhost:3000`.

## Contribution Guidelines

We welcome contributions from the community! To contribute, please follow these guidelines:

1. Fork the repository and create a new branch for your feature or bugfix.
2. Write tests for your changes.
3. Ensure all tests pass.
4. Submit a pull request with a clear description of your changes.

## Technologies and Frameworks Used

- .NET 8.0
- ASP.NET Core
- OpenTelemetry
- Prometheus
- Grafana
- HealthChecksUI