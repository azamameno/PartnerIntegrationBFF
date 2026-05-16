# PartnerIntegrationBFF

A **Backend for Frontend (BFF)** service that accepts partner transaction requests, verifies partners against an external API, and queues accepted transactions via RabbitMQ for downstream processing.

---

## Architecture Overview

```
Partner Client
      │  POST /api/v1/partner/transactions
      │  Headers: X-Timestamp, X-Signature
      ▼
┌─────────────────────────────────────┐
│         PartnerIntegrationBFF       │
│                                     │
│  HmacAuthenticationHandler          │
│    → verify HMAC-SHA256 signature   │
│                                     │
│  CreateTransactionEndpoint          │
│    → FluentValidation (input)       │
│    → IPartnerClient (Refit)         │  ──► PartnerExternalApi
│    → IMessageQueueService           │  ──► RabbitMQ
└─────────────────────────────────────┘
```

---

## Architectural Decisions

### 1. BFF Pattern

The service acts as a BFF — a thin entry point dedicated to partner integrations. It handles authentication, validation, and orchestration without containing business logic itself. This separation makes it easy to evolve each concern independently and keeps the service focused on a single responsibility.

### 2. HMAC-SHA256 Request Signature Authentication

Instead of a shared API key (which provides only identity, not integrity), I chose per-request HMAC-SHA256 signatures:

- **Each partner has its own secret key** (`Security:PartnerSecrets:{partnerId}` in config), enabling independent revocation.
- **Signature covers the request body hash + timestamp**, ensuring the payload has not been tampered with in transit (integrity) and that each request is uniquely bound to a moment in time (non-repudiation).
- **`CryptographicOperations.FixedTimeEquals`** is used for all comparisons to prevent timing attacks.

**Signature algorithm:**
```
bodyHash  = SHA256(rawBody).toLowerHex()
signedStr = "{unixTimestamp}:{bodyHash}"
signature = Base64(HMAC-SHA256(partnerSecret, signedStr))
```

**Required headers:**
```
X-Timestamp: <unix timestamp UTC>
X-Signature: <base64 HMAC-SHA256>
```

### 3. Refit + Polly Resilience

Refit generates a type-safe HTTP client for `IPartnerClient` from an interface definition — no boilerplate, no raw `HttpClient` management. The resilience pipeline (via `Microsoft.Extensions.Http.Resilience`) adds:
- **Retry**: 3 attempts with 500ms exponential backoff for transient failures.
- **Timeout**: 30s per attempt to prevent indefinite hangs.

This is production-grade resilience with minimal code.

### 4. RabbitMQ for Async Processing

Transactions are accepted and **queued immediately** rather than processed synchronously. This decouples the BFF from downstream processing speed, allows the API to return `202 Accepted` without waiting for processing to complete, and provides durability if downstream systems are temporarily unavailable.

`RabbitMqMessageQueueService` is registered as both `IHostedService` (manages the connection lifecycle) and `IMessageQueueService` (publishing interface), using a single shared instance via a double-registration pattern.

### 5. Vertical Slice Architecture with Minimal APIs

Each feature lives in its own folder containing the endpoint, request model, and validator — all co-located. This avoids the overhead of a separate service layer for straightforward orchestration: validate → verify → queue. Endpoint handler methods are `static`, making them directly unit-testable without requiring an HTTP host.

The `IEndpoint` interface + `MapEndpoints` extension auto-discovers and registers all endpoints via reflection, keeping `Program.cs` clean.

### 6. FluentValidation

Input validation is declared in the feature folder alongside the endpoint. FluentValidation provides a declarative, testable validation model with clear separation from business logic. Validators are auto-registered via assembly scanning.

### 7. Global Exception Handler Middleware

Unhandled exceptions are caught centrally in `GlobalExceptionHandlerMiddleware`, returning a consistent JSON error response without leaking stack traces to clients.

---

## Project Structure

```
PartnerIntegrationBFF/
├── Features/
│   ├── Partners/
│   │   └── CreateTransaction/
│   │       ├── CreateTransactionEndpoint.cs   # Minimal API handler
│   │       ├── CreateTransactionRequest.cs    # Request model
│   │       └── CreateTransactionValidator.cs  # FluentValidation rules
 HMAC signature
├── Infrastructure/
│   ├── Auth/
│   │   └── HmacAuthenticationHandler.cs      # HMAC-SHA256 auth handler
│   └── Messaging/
│       ├── IMessageQueueService.cs            # Queue abstraction
│       └── RabbitMqMessageQueueService.cs     # RabbitMQ implementation
├── Shared/
│   ├── Contracts/
│   │   └── IPartnerClient.cs                 # Refit external API client
│   ├── Extensions/
│   │   └── EndpointExtensions.cs             # IEndpoint auto-discovery
│   └── Middleware/
│       └── GlobalExceptionHandlerMiddleware.cs
├── Program.cs

PartnerExternalApi/                            # Simulated partner verification API
PartnerIntegrationBFF.Tests/                   # xUnit unit tests
docker-compose.yml
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker + Docker Compose](https://docs.docker.com/get-docker/) (for full environment)

---

## Running the Project

### Option A — Docker Compose (recommended)

Starts RabbitMQ, PartnerExternalApi, and PartnerIntegrationBFF together:

```bash
docker-compose up --build
```

| Service | URL |
|---------|-----|
| PartnerIntegrationBFF | http://localhost:5000 |
| PartnerExternalApi | http://localhost:5241 |
| RabbitMQ Management UI | http://localhost:15672 (guest/guest) |

Partner secrets are injected via environment variables in `docker-compose.yml`. ASP.NET Core maps double-underscore to nested config keys:

```yaml
# docker-compose.yml
environment:
  - Security__PartnerSecrets__partner-01=docker-secret-partner-01
  - Security__PartnerSecrets__partner-02=docker-secret-partner-02   # add more as needed
```

### Option B — Local Development

**Step 1:** Start infrastructure dependencies:
```bash
docker-compose up rabbitmq partner-external-api
```

**Step 2:** Configure local secrets (already in `appsettings.Development.json`, git-ignored):
```json
{
  "Security": {
    "PartnerSecrets": {
      "partner-01": "dev-secret-partner-01-abc123"
    }
  }
}
```

**Step 3:** Run the BFF:
```bash
cd PartnerIntegrationBFF
dotnet run
```

Service available at `http://localhost:5000` (or port shown in console).

---

## API

### POST `/api/v1/partner/transactions`

**Headers:**
```
Content-Type: application/json
X-Timestamp: <unix timestamp UTC, e.g. 1747382400>
X-Signature: <Base64(HMAC-SHA256(partnerSecret, "{timestamp}:{SHA256hex(body)}"))>
```

**Request body:**
```json
{
  "partnerId": "partner-01",
  "transactionReference": "ref-001",
  "amount": 250.00,
  "currency": "USD",
  "timestamp": "2026-05-16T00:00:00Z"
}
```

**Responses:**

| Status | Meaning |
|--------|---------|
| `202 Accepted` | Transaction accepted and queued |
| `400 Bad Request` | Validation failed |
| `401 Unauthorized` | Missing or invalid HMAC signature |
| `502 Bad Gateway` | Partner verification failed |
| `503 Service Unavailable` | Queue unavailable |

**Computing the signature (example in C#):**
```csharp
var body = JsonSerializer.Serialize(request);
var bodyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
var signedStr = $"{unixTimestamp}:{bodyHash}";
using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(partnerSecret));
var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedStr)));
```

---

## Running Tests

```bash
dotnet test
```

**Test coverage:**

| Suite | Tests | What's covered |
|-------|-------|----------------|
| `HmacAuthHandlerSignatureTests` | 9 | HMAC auth: happy path, tampered body, wrong secret, missing headers, unknown partner |
| `CreateTransactionEndpointTests` | 5 | Endpoint handler: validation failure → 400, partner not verified → 502, client throws, queue throws, success → 202 |
| `CreateTransactionValidatorTests` | 9 | Validation rules for all fields |

All tests use **xUnit** + **Moq** with no external dependencies (in-memory configuration, `DefaultHttpContext`).
