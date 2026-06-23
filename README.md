# Payment Allocation Ledger

Production-style .NET solution for allocating GBP payments across invoice line items using an append-only ledger.

## How to Run

Requirements:

- .NET 10 SDK
- Docker, for PostgreSQL integration tests
- PostgreSQL running locally, if you want to use the UI against a real database

Run the UI:

```bash
dotnet run --project PaymentAllocation.csproj
```

The UI is available at:

- `http://localhost:5261`
- `https://localhost:7084`

Workflow in the UI:

1. Open the dashboard.
2. Create an invoice with one or more line items.
3. Open the invoice details page.
4. Enter a payment amount and allocate it.
5. Review the updated outstanding balance and the append-only ledger entries.

Build the solution:

```bash
dotnet build PaymentAllocation.slnx
```

## Apply Migrations

The web app applies migrations on startup, so the simplest path is to run the UI after setting a connection string. If you want to apply migrations manually, use:

```bash
dotnet ef database update --project src/Ledger.Infrastructure --startup-project src/Ledger.Infrastructure
```

Set the connection string first if needed:

```bash
set LEDGER_CONNECTION_STRING=Host=localhost;Port=5432;Database=AllocatedPaymentSys;Username=postgres;Password=Admin@123
```

On bash-compatible shells, use `export` instead of `set`.

## Run Tests

```bash
dotnet test
```

The tests use Testcontainers with PostgreSQL so the allocation logic, migrations, transactions, constraints, and locking behavior are exercised against the real database engine.

## Design Decisions

- Clean Architecture is kept intentionally small: domain entities in `Ledger.Domain`, the payment allocation contract and result DTOs in `Ledger.Application`, and EF Core plus the allocator implementation in `Ledger.Infrastructure`.
- Money is stored as `decimal` with PostgreSQL `numeric(18,2)` and domain/database checks ensure penny precision.
- Ledger entries are append-only. Charges are positive, payment allocations and customer credits are negative.
- Outstanding balance is derived from `ledger_entries` by summing amounts for an invoice. Line outstanding is derived by summing entries for each line item.
- Oldest-first ordering is `DueDate`, then `CreatedAt`, then `Id` as a deterministic final tie-breaker.

## Assumptions Made

- Invoices and line items are created before payment allocation.
- Creating an invoice line item also creates a matching `Charge` ledger entry. The tests seed data this way.
- Payment amounts must be positive and supplied in GBP to two decimal places.
- Overpayment credit belongs to the invoice/customer context and is represented as a negative invoice-level ledger entry.

## Concurrency Strategy

`AllocatePaymentAsync` opens a database transaction and locks the invoice row with `SELECT ... FOR UPDATE` before reading ledger balances or inserting payment entries. Every allocator must acquire that invoice lock first, so two simultaneous payments for the same invoice are serialized. Payments for different invoices can still run concurrently.

The important ordering is:

1. Start transaction.
2. Lock the invoice row.
3. Read ledger-derived balances.
4. Insert new ledger entries.
5. Commit.

The second concurrent allocator cannot read and allocate against stale balances because it waits for the first transaction to commit before it continues past the lock.

## Known Limitations

- There is no API layer because the assignment only requires the service method and persistence.
- There is no idempotency key for duplicate payment messages.
- Currency is assumed to be GBP for all invoices rather than modelled as a column.
- No outbox/audit export is included beyond the append-only ledger table.
- The UI is intentionally focused on the core workflow and does not try to become a full accounting back office.

## What I Would Do Next With More Time

- Add a payment receipt table with a unique external payment reference for idempotency.
- Add application-level commands for invoice creation that atomically create line items and charge ledger entries.
- Add observability around allocation failures, lock waits, and overpayment rates.
- Add explicit customer-level credit application across invoices if required by the business.

## AI Review

- AI tool used: OpenAI Codex.
- Incorrect/incomplete output encountered: the first draft used ledger-derived balances but did not yet document that invoice creation must insert initial `Charge` entries.
- How it was caught and corrected: reviewing the append-only requirement showed that line item amount alone must not be the balance source, so the domain/test seed and README were adjusted to make charge entries explicit.

## Four Questions

### 1. PostgreSQL Experience

I solved a production locking issue where concurrent workers were double-processing rows. The fix used a partial index on eligible rows plus `SELECT ... FOR UPDATE SKIP LOCKED` inside short transactions, which let workers safely claim independent batches without blocking the whole queue.

### 2. AI Failure Example

I have seen AI generate EF Core code that calculated balances from mutable invoice columns instead of ledger entries. I caught it by comparing the code against the append-only accounting rule and corrected it so all balances are derived from inserted ledger rows.

### 3. Concurrency

This implementation locks the invoice row with `SELECT ... FOR UPDATE` inside the allocation transaction. That serializes allocations per invoice, so the second payment reads balances only after the first payment's ledger entries have committed.

### 4. Compensation & Availability

- Current notice period: 15 Days
- Salary expectation: 10 LPA
- Whether Rs 10-16 LPA works: Yes
