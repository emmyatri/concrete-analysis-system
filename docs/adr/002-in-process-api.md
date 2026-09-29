
# ADR-002: In-Process ASP.NET Core API

## 1. Context

The Vulcan Materials Concrete Batch Analysis and Strength Prediction System is designed to operate on a single Windows workstation. The application requires a backend responsible for processing requests, enforcing data validation, and coordinating access to the local SQLite database.

The team considered deploying ASP.NET Core as a separate server process or hosting it directly within the WPF desktop application.

A separate server would require additional installation, configuration, and process management. Since the system is intended for a single workstation, maintaining an independent server process would introduce unnecessary deployment complexity.

The team also considered allowing WPF to access application services or the database directly. However, this would establish multiple pathways for data operations, making it more difficult to maintain consistent architectural boundaries.

Hosting ASP.NET Core within the desktop application provides a single entry point for data operations while retaining a single-program deployment model.

## 2. Decision

The team has selected **ASP.NET Core hosted in-process within the WPF desktop application**, with all WPF data operations routed through the API.

The application will start and manage the ASP.NET Core server as part of its normal lifecycle. The server will listen exclusively on the local loopback interface (`127.0.0.1`), preventing direct access from other computers on the network.

### 2.1 API and Data Access

All database read and write operations initiated by the WPF interface will pass through the ASP.NET Core API using HTTP.

The WPF interface will not directly access application services, Entity Framework Core, or the SQLite database.

API controllers will handle HTTP requests and responses, delegating business operations to the application service layer.

Core will contain the authoritative business logic, calculations, and validation rules. Entity Framework Core will handle database persistence through the infrastructure layer.

This separation establishes a consistent data-access pathway and prevents duplication of business logic within API controllers.

### 2.2 Authoritative Validation

The WPF interface performs preliminary validation to provide immediate feedback to the operator.

However, all submitted data will pass through the ASP.NET Core API, which delegates validation to the application service layer.

The application service layer will enforce the authoritative business rules defined in `Vulcan.Domain` before any changes are persisted.

API controllers will not duplicate business logic or validation rules. Instead, they will invoke the appropriate application services and return their results to the WPF interface.

Requests that fail validation will be rejected, and the API will return appropriate error information to the WPF interface.

### 2.3 Application Lifecycle

The ASP.NET Core server will start when the WPF application launches and shut down when the application closes.

The server and desktop interface will operate within the same application process, eliminating the need for a separately installed backend service.

The application must handle API startup failures and ensure that database operations are unavailable until the server is ready.

## 3. Consequences

| Positive Consequences | Trade-offs |
|---|---|
| No separate server installation or configuration | The API is unavailable when the desktop application is closed |
| Simplified deployment through a single application | Server startup and shutdown must be managed by the desktop application |
| A single, consistent entry point for all WPF data operations | All WPF data requests require HTTP communication and serialization |
| Centralized enforcement of business rules and validation | Additional API endpoints and request/response models must be maintained |
| Separation between presentation, business logic, and persistence | Hosting an HTTP server introduces additional application complexity |
| Local API communication without external network access | The API is not directly accessible from other workstations |

### Data Integrity and Maintainability

Routing all WPF data operations through the API establishes a consistent architectural boundary and reduces the risk of bypassing authoritative validation.

Keeping business logic and validation within Core prevents duplication between API controllers and other application components. Changes to business rules can therefore be implemented and tested in one location.

### Security and Deployment

Binding the server to `127.0.0.1` limits network exposure but does not prevent other processes on the same workstation from attempting to access the API.

Appropriate access controls should be considered if the API exposes sensitive operations.

### Future Scalability

If the system is expanded to support multiple workstations or centralized hosting, the team may need to separate the API into an independently deployed service.

Maintaining a clear separation between the WPF interface, API, Core, and infrastructure layer can help accommodate future architectural changes.

## 4. Status

**Proposed** — Pending formal Co-Lead approval.
