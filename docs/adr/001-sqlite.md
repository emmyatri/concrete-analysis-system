
# ADR-001: SQLite for Local Data Storage

## 1. Context

The Vulcan Materials Concrete Batch Analysis and Strength Prediction System is designed to operate on a single Windows workstation. The application requires persistent storage for concrete batch information, specimen measurements, curing conditions, environmental measurements, and break-test results.

The team considered SQLite and SQL Server Express as potential database solutions. Both support the project's anticipated data requirements and integrate with Entity Framework Core.

However, SQL Server Express requires a separate database engine installation and configuration. Since the system will operate on a single workstation, the additional deployment and maintenance requirements may introduce unnecessary complexity.

SQLite provides an embedded, file-based database that can operate directly within the application without requiring a separate database server.

## 2. Decision

The team has selected **SQLite with Entity Framework Core (EF Core)** as the application's database solution.

SQLite will provide persistent local storage, while EF Core will handle database access, entity mapping, and schema migrations.

This decision supports the project's single-workstation architecture and simplifies installation and maintenance for Vulcan Materials.

## 3. Consequences

| Positive Consequences | Trade-offs |
|---|---|
| No separate database server installation | Limited support for concurrent writes |
| Simplified application deployment | Not designed for extensive multi-workstation access |
| Integration with the existing .NET technology stack | Future database migration may be necessary |
| Reduced configuration and maintenance | Database backup procedures must be established |

Although SQLite supports the project's current requirements, expanding the system to multiple workstations or introducing centralized database access may require reevaluating this decision.

The application must also implement appropriate backup and recovery procedures to protect locally stored data.

## 4. Status

**Proposed** — Pending Co-Lead approval.
