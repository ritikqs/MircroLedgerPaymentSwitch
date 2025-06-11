# MicroLedger

A microservice-based double-entry accounting system built with .NET 8.

## Features

- Double-entry accounting system
- Payment processing with transaction validation
- Daily interest accrual for savings accounts (runs at 2 AM UTC)
- RESTful API endpoints with Swagger documentation
- Containerized deployment with Docker
- SQL Server database with Entity Framework Core

## Project Structure

- `MicroLedger.Api`: Web API project with controllers and middleware
- `MicroLedger.Domain`: Core domain models and business logic
- `MicroLedger.Application`: Application services and use cases
- `MicroLedger.Infrastructure`: Data access and external service integration

## Getting Started

### Prerequisites

- .NET 8 SDK
- Docker and Docker Compose
- SQL Server (if running locally)

### Running with Docker

1. Build and start the containers:
   ```bash
   docker-compose up -d
   ```

2. The API will be available at `http://localhost:8080`
3. Swagger documentation will be available at `http://localhost:8080/swagger`

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
  ```json
  {
    "fromAccountId": "string",
    "toAccountId": "string",
    "amount": 0,
    "description": "string"
  }
  ```
- `GET /api/payments/{id}`: Get payment details

### Accounts

- `GET /api/accounts/{id}/balance`: Get account balance
  - Returns current balance including accrued interest

## Interest Accrual

The system automatically accrues interest on savings accounts daily at 2 AM UTC. The interest calculation:

1. Uses the daily rate (annual rate / 365)
2. Calculates interest based on the current balance
3. Creates a credit transaction for the interest amount
4. Updates the account balance

### Configuration

Key configuration settings in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "LedgerDb": "Server=localhost;Database=LedgerDb;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Interest": {
    "AnnualRate": 0.05,
    "Schedule": "0 2 * * *"  // Daily at 2 AM UTC
  }
}
```

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

### Database Migrations

To create a new migration:

```bash
cd MicroLedger.Infrastructure
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## Architecture

The system follows Clean Architecture principles:

- Domain Layer: Core business logic and entities
- Application Layer: Use cases and business rules
- Infrastructure Layer: Data access and external services
- API Layer: HTTP endpoints and request handling

## Error Handling

The API uses a global exception handler to provide consistent error responses:

```json
{
  "error": {
    "code": "string",
    "message": "string",
    "details": []
  }
}
``` 