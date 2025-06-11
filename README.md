# MicroLedger

A microservice-based double-entry accounting system built with .NET 7.

## Features

- Double-entry accounting system
- Payment processing
- Daily interest accrual for savings accounts
- RESTful API endpoints
- Containerized deployment with Docker

## Project Structure

- `MicroLedger.Api`: Web API project
- `MicroLedger.Domain`: Core domain models and business logic
- `MicroLedger.Application`: Application services and use cases
- `MicroLedger.Infrastructure`: Data access and external service integration

## Getting Started

### Prerequisites

- .NET 7 SDK
- Docker and Docker Compose
- SQL Server (if running locally)

### Running with Docker

1. Build and start the containers:
   ```bash
   docker-compose up -d
   ```

2. The API will be available at `http://localhost:8080`

### Running Locally

1. Update the connection string in `appsettings.json` to point to your SQL Server instance
2. Run the API project:
   ```bash
   cd MicroLedger.Api
   dotnet run
   ```

## API Endpoints

### Payments

- `POST /api/payments`: Create a new payment
- `GET /api/payments/{id}`: Get payment details

### Accounts

- `GET /api/accounts/{id}/balance`: Get account balance

## Configuration

Key configuration settings in `appsettings.json`:

- `ConnectionStrings:LedgerDb`: SQL Server connection string
- `Interest:AnnualRate`: Annual interest rate for savings accounts

## Development

### Adding New Features

1. Add domain models in `MicroLedger.Domain`
2. Implement business logic in `MicroLedger.Application`
3. Add data access in `MicroLedger.Infrastructure`
4. Create API endpoints in `MicroLedger.Api`

### Running Tests

```bash
dotnet test
``` 