# Study Point Assignment: Logging

## Case

_Copenhagen City Bikes_ is a small bike-rental web service. You must add **system logging** and **audit logging** to help operators run the system and security teams trace user actions. The service has the following core flows:

- Browse available bikes
- Reserve a bike
- Pick up a bike (start rental)
- Return a bike (end rental)
- Admin adjusts inventory / prices

The service can just be a simple Java app (or preferred programming language). No database is required; in‑memory data is fine.

---

## Assignment Goals

1. **System logs** tell the story of the application’s health and operations.
2. **Audit logs** capture _who did what, when, and where_ for accountability.
3. Logs are **privacy‑aware**, **rotated**, and **useful** (not noisy).

---

## Requirements

### A) System Logging (operators)

- Use **SLF4J + Logback** (or similar).
- Emit **INFO** milestones (e.g. application start or bike reservation), **WARN** for degraded behavior (e.g. external service slow, or low inventory), **ERROR** for failures (e.g. reservation rejected - bike already taken or payment service down). Optional **DEBUG** for dev details.
- Log to **console** _and_ to a **rotating file** (`logs/app.log`, daily rotation, retain ≥ 14 days).
- Include at least these events:
  - Application startup/shutdown
  - Each request summary: HTTP method, endpoint path (e.g. /reservations), HTTP response status, and elapsed time (measured in ms - between start and end) - see later for code exmaples.
  - External call simulation (e.g., payment/verification): success, latency, fallback usage
  - Errors with stack trace (once—avoid double-logging)
- Add **MDC** fields at request scope (if using Java logging framework):
  - `correlation_id` (UUID per request). It's optional, but recommended → a unique ID for the request, useful to trace across logs. (In Java, you can use `java.util.UUID`: `UUID.randomUUID().toString();`
  - `session_id` or `user_id` when known (IDs only, no PII)

### B) Audit Logging (security/compliance)

- Separate logger: `AUDIT` → **JSON** lines written to `logs/audit.log` (using `logstash-logback-encoder`).
- Include **who, what, when, where**:
  - `user_id` (internal ID only), `action`, `resource_id`, `ip`
  - Timestamp is automatic; include `correlation_id` from MDC (if using Java logging framework)
- Required audit events:
  - `LOGIN_SUCCESS`, `LOGIN_FAILURE`
  - `RESERVATION_CREATE` (bike_id, start_time)
  - `RENTAL_START` (rental_id)
  - `RENTAL_END` (rental_id, duration, fees_charged)
  - `ADMIN_INVENTORY_UPDATE` (admin_id, delta)
- **Privacy-aware**: never log CPR, names, emails, tokens, or passwords. Use IDs/hashes.
- Retain `audit.log` longer (e.g., ≥ 90 days).

### C) Helpers to keep code clean

You can consider to:

- Create a small **helper** method for timings, e.g. `TimeIt.info(log, "Reserve bike", () -> ...)`.
- Use **conditional logging** for expensive DEBUG operations: `if (log.isDebugEnabled()) { ... }`

### RequestLogger helper example

Example code to log request summary:

```java
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class RequestLogger {
    private static final Logger log = LoggerFactory.getLogger(RequestLogger.class);

    public static void log(String method, String path, int status, Runnable action) {
        long start = System.currentTimeMillis();
        try {
            action.run(); // run the simulated request
        } finally {
            long elapsed = System.currentTimeMillis() - start;
            log.info("Request handled method={} path={} status={} elapsed_ms={}",
                     method, path, status, elapsed);
        }
    }
}
```

Example usage in Main

```java
public class Main {
    public static void main(String[] args) {
        // Example: simulate a reservation
        RequestLogger.log("POST", "/reservations", 201, () -> {
            // Simulate some work
            try {
                Thread.sleep(120); // pretend it takes 120 ms
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
            }
            System.out.println("Reservation created!"); // business logic
        });

        // Example: simulate fetching bikes
        RequestLogger.log("GET", "/bikes", 200, () -> {
            try {
                Thread.sleep(50); // pretend it takes 50 ms
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
            }
            System.out.println("List of bikes"); // business logic
        });
    }
}
```

Example log output

```pgsql
2025-09-23 15:42:10 INFO  RequestLogger - Request handled method=POST path=/reservations status=201 elapsed_ms=123
2025-09-23 15:42:11 INFO  RequestLogger - Request handled method=GET path=/bikes status=200 elapsed_ms=52

```

_Usage_: If users say “rentals are slow”, logs show how long each request takes.

Here is anothet example where one request log (INFO) wraps a "TimeIt" debug log for an inner database call — to show operator vs developer detail in action: [Examples that measure timing](https://github.com/Tine-m/DLSE25/blob/main/logging-requestlogger-timeit-combined.md)

---

## Functional App Scope

Implement endpoints (or CLI commands) for:

- `GET /bikes` — list available bikes
- `POST /reservations` — reserve a bike `{ bike_id }`
- `POST /rentals/start` — start rental `{ reservation_id }`
- `POST /rentals/end` — end rental `{ rental_id }`
- `POST /auth/login` — simulate login `{ user_id, password }`
- `POST /admin/inventory` — simulate admin action `{ bike_id, delta }`

> You can stub data in memory and simulate delays/failures to produce logs.

---

## Suggested Log Examples

### System (INFO)

```
INFO  Request handled method=POST path=/reservations status=201 elapsed_ms=142 correlation_id=6fdb...
```

### System (WARN)

```
WARN  Payment service slow elapsed_ms=850 using_fallback=true correlation_id=...
```

### System (ERROR) + stack trace once

```
ERROR Reservation failed bike_id=b-42 error="SoldOutException" correlation_id=...
<stack trace>
```

### Audit (JSON)

```json
{
  "@timestamp": "2025-09-22T10:10:12Z",
  "level": "INFO",
  "logger_name": "AUDIT",
  "message": "USER_ACTION",
  "action": "RESERVATION_CREATE",
  "user_id": "u123",
  "resource_id": "bike:b-42",
  "ip": "203.0.113.7",
  "correlation_id": "6fdb-...",
  "log_type": "audit",
  "service": "city-bikes"
}
```

---

## Deliverables

- GitHub link emailed to tm@ek.dk with
  - Source code (if Java with `logback.xml`) and minimal endpoints/CLI.
  - `logs/app.log` and `logs/audit.log` samples demonstrating all required events.
  - Short `README.md`: how to run; what you log and why; how rotation/retention is set.

## Deadline

Tuesday November 4th
