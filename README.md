# MicroLedger - Double-Entry Accounting Microservice

A modern, scalable double-entry accounting system built with .NET 8, implementing event-driven architecture and real-time balance updates.

## Architecture

### Core Components

1. **Data Model**
   - Account (Id, OwnerName, Currency, Type: {Current, Savings})
   - Transaction (Id, TimestampUtc, Reference)
   - JournalLine (Id, TxId, AccountId, Debit, Credit)
   - Enforces double-entry accounting (ΣDebit = ΣCredit per Transaction)

2. **API Endpoints**
   - POST /api/payments - Process payments between accounts
   - GET /api/accounts/{id}/balance - Get real-time account balance
   - GET /api/transactions/{id} - Get transaction details
   - POST /api/auth/login - JWT authentication

3. **Real-time Updates**
   - gRPC streaming service for balance updates
   - MassTransit for event publishing
   - RabbitMQ for message broker

4. **Background Services**
   - Daily interest accrual (02:00 UTC)
   - Event outbox publisher
   - Health monitoring

### Technology Stack

- **.NET 8** - Core framework
- **Entity Framework Core 8** - Data access
- **SQL Server** - Primary database
- **RabbitMQ** - Message broker
- **gRPC** - Real-time streaming
- **MassTransit** - Message bus
- **JWT** - Authentication
- **OpenTelemetry** - Observability
- **Serilog** - Logging
- **Docker** - Containerization

## Getting Started

### Prerequisites

- Docker and Docker Compose
- .NET 8 SDK
- SQL Server (if running locally)

### Running with Docker

1. Clone the repository:
```bash
git clone https://github.com/yourusername/microledger.git
cd microledger
```

2. Start the services:
```bash
docker-compose up -d
```

This will start:
- API service (https://localhost:5000)
- SQL Server (localhost:1433)
- RabbitMQ (localhost:5672, Management UI: http://localhost:15672)
- Redis (localhost:6379)

3. Access the services:
- API: https://localhost:5000
- Swagger UI: https://localhost:5000/swagger
- RabbitMQ Management: http://localhost:15672 (guest/guest)
- Health Check: https://localhost:5000/healthz

### Development Setup

1. Restore dependencies:
```bash
dotnet restore
```

2. Apply database migrations:
```bash
dotnet ef database update
```

3. Run the application:
```bash
dotnet run --project MicroLedger.Api
```

## Features

### 1. Payment Processing
- Validates currency matching
- Ensures sufficient funds
- Creates balanced journal entries
- Publishes events for downstream processing

### 2. Real-time Balance Updates
- gRPC streaming service
- Instant balance updates
- Efficient binary protocol
- Bi-directional streaming support

### 3. Interest Accrual
- Daily calculation (02:00 UTC)
- Configurable interest rates
- Balanced transaction creation
- Event publishing

### 4. Event Outbox
- Reliable event publishing
- Once-and-only-once delivery
- Retry mechanism
- Error tracking

### 5. Authentication & Authorization
- JWT Bearer tokens
- Role-based access control
- Secure password handling
- Token refresh support

## Trade-offs and Decisions

### 1. Database Choice
- **SQL Server**: Chosen for ACID compliance and transaction support
- **Trade-off**: Could use PostgreSQL for open-source alternative

### 2. Message Broker
- **RabbitMQ**: Selected for reliability and message persistence
- **Trade-off**: Could use Kafka for higher throughput

### 3. Real-time Updates
- **gRPC**: Chosen for efficient binary protocol
- **Trade-off**: Could use SignalR for WebSocket support

### 4. Event Sourcing
- **Outbox Pattern**: Implemented for reliability
- **Trade-off**: Could use full event sourcing for better audit trail

### 5. Caching
- **Redis**: Used for FX rates
- **Trade-off**: Could implement more aggressive caching

## Monitoring and Observability

### 1. Logging
- Serilog for structured logging
- Console and file output
- Correlation IDs
- Log levels configuration

### 2. Metrics
- OpenTelemetry integration
- Custom metrics for:
  - Payment processing time
  - Balance calculation time
  - Event publishing latency

### 3. Health Checks
- Database connectivity
- RabbitMQ connection
- Redis connection
- Custom health indicators

## Security Considerations

1. **Authentication**
   - JWT with short expiration
   - Secure token storage
   - Role-based access

2. **Data Protection**
   - HTTPS enforcement
   - SQL injection prevention
   - XSS protection

3. **Audit Trail**
   - Transaction logging
   - User action tracking
   - Balance change history

## Performance Considerations

1. **Database**
   - Indexed queries
   - Efficient joins
   - Connection pooling

2. **Caching**
   - Redis for FX rates
   - Balance caching (optional)
   - Query result caching

3. **Message Processing**
   - Batch processing
   - Retry policies
   - Dead letter queues

## Future Improvements

1. **Multi-currency Support**
   - FX rate integration
   - Currency conversion
   - Exchange rate caching

2. **Scalability**
   - Horizontal scaling
   - Read replicas
   - Message partitioning

3. **Analytics**
   - Transaction analytics
   - Balance trends
   - User activity metrics

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details. 