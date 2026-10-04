# Plant Encyclopedia — Security Design

## 1. Purpose

This document defines the security principles, threat model, security controls, and verification strategy for Plant Encyclopedia.

Security is treated as a continuous engineering concern rather than a final hardening phase.

Every feature should consider:

1. What can an untrusted user control?
2. What resources are being accessed?
3. Who is allowed to access them?
4. What happens if the input is malicious or unexpected?
5. Could the feature expose private information?
6. Could the feature be abused to create excessive cost or load?

---

# 2. Security Goals

Plant Encyclopedia should protect:

* user accounts
* user favorites
* search and view history
* uploaded plant images
* API credentials and secrets
* database credentials
* external provider credentials
* AI provider API keys
* infrastructure configuration
* internal error and diagnostic information

The application should maintain:

* confidentiality
* integrity
* availability
* least privilege
* secure-by-default behavior

---

# 3. Security Principles

## 3.1 Backend Is the Security Boundary

The frontend must never be trusted to enforce security.

Angular and .NET MAUI may hide or disable UI elements based on authentication state, but the backend must independently verify:

* authentication
* authorization
* ownership
* permissions
* input validity

Example:

```text
Client
  ↓
API request
  ↓
Authentication
  ↓
Authorization
  ↓
Validation
  ↓
Business logic
  ↓
Database
```

---

## 3.2 Least Privilege

Users and services should receive only the access they require.

Examples:

* users can access their own private data
* the frontend does not receive database credentials
* the AI provider receives only the information necessary for identification
* application services should not use unnecessarily broad AWS permissions

---

## 3.3 Never Trust Client Input

All client-provided data must be treated as untrusted.

This includes:

* query parameters
* route parameters
* JSON bodies
* form fields
* uploaded files
* filenames
* authentication claims
* client-provided identifiers

Validation must occur on the backend.

---

# 4. Authentication

Authentication will be implemented when user accounts are introduced.

The authentication design must provide:

* secure login
* secure logout
* authenticated API requests
* protected resources
* appropriate credential handling
* secure session/token handling

The exact authentication mechanism is an architectural decision and should be documented separately when implementation begins.

Do not implement custom cryptographic authentication when a mature framework/library provides the required security primitives.

---

# 5. Authorization

Authentication answers:

> Who are you?

Authorization answers:

> Are you allowed to perform this action?

Authorization must be enforced server-side.

For user-owned resources, the backend must verify ownership or permission before returning or modifying data.

Example:

```text
GET /api/favorites/123

User A
   ↓
Is favorite 123 owned by User A?
   ├── YES → return resource
   └── NO  → deny access
```

Never rely on the frontend simply hiding another user's data.

---

# 6. Object-Level Authorization

Plant Encyclopedia must consider object-level authorization whenever an endpoint accepts a resource identifier.

Examples:

* favorite ID
* history ID
* identification request ID
* uploaded image ID
* user-specific resource ID

The application must not assume that possession of an ID grants access.

Security tests should explicitly attempt unauthorized access to another user's resources.

---

# 7. API Security

The API should implement:

* authentication where required
* authorization
* request validation
* consistent error handling
* rate limiting for abuse-sensitive endpoints
* appropriate CORS configuration
* HTTPS in deployed environments
* security-related response headers where appropriate
* correlation/request IDs for diagnostics
* safe logging

Production error responses must not expose:

* stack traces
* database connection strings
* API keys
* internal filesystem paths
* sensitive infrastructure details

---

# 8. Input Validation

All externally supplied values must be validated.

Validation areas include:

* string length
* required fields
* numeric ranges
* enum values
* identifiers
* pagination parameters
* search queries
* uploaded files
* request payload size

Validation should occur before business logic and database operations.

---

# 9. Database Security

The application should use Entity Framework Core safely and avoid constructing SQL queries through unsafe string concatenation.

Security requirements include:

* parameterized database access
* least-privilege database credentials
* separate development and production credentials
* secure connection configuration
* appropriate foreign-key constraints
* careful handling of user-owned data
* migration review
* no secrets stored in source control

Database access should occur through the application layer rather than exposing PostgreSQL directly to the public internet.

---

# 10. File Upload Security

Plant identification accepts user-uploaded images and therefore requires additional security controls.

The backend should enforce:

* maximum file size
* allowed image formats
* MIME/content validation
* safe filename handling
* server-controlled storage paths
* private storage where appropriate
* image processing/re-encoding where appropriate
* protection against arbitrary file uploads

The original filename supplied by a user must never determine a filesystem path.

Uploaded images should not automatically become publicly accessible.

---

# 11. Image Privacy

Plant images may contain unintended personal information, such as:

* people
* homes
* addresses
* location metadata
* other private information

The application should minimize unnecessary retention and exposure.

Future implementation should consider:

* EXIF metadata handling
* retention policies
* deletion behavior
* private object storage
* signed URLs when temporary access is required

The application should not expose uploaded images publicly by default.

---

# 12. AI Provider Security

AI provider credentials must remain server-side.

The architecture should be:

```text
Angular / MAUI
      ↓
ASP.NET Core API
      ↓
AI Provider
```

and never:

```text
Angular / MAUI
      ↓
AI Provider using embedded secret key
```

AI requests should contain only the information required for the identification task.

AI output must not automatically become authoritative plant information.

---

# 13. Rate Limiting and Abuse Prevention

Rate limiting is especially important for:

* plant identification
* authentication endpoints
* search endpoints if abuse becomes significant
* expensive external provider requests

The identification endpoint is both a security and cost-control concern because each request may trigger an external AI API call.

The system should eventually distinguish reasonable user activity from abusive request patterns.

Anonymous access may receive stricter limits than authenticated access.

Exact limits should be chosen based on measured usage and provider costs rather than arbitrary numbers.

---

# 14. Secrets Management

Secrets must never be committed to source control.

Sensitive values may include:

* database passwords
* AI API keys
* AWS credentials
* authentication secrets
* third-party API credentials

Development, CI, and production environments should use appropriate secret-management mechanisms.

The frontend bundle must never contain server-side secrets.

---

# 15. CORS and Browser Security

CORS should allow only the origins required by the deployed application.

Development configuration may allow local development origins.

Production configuration should not use unrestricted origins merely for convenience.

Browser-facing security controls should be configured deliberately rather than copied blindly from development settings.

---

# 16. Authentication and Token Security

When authentication is implemented, the project should explicitly evaluate:

* token/session lifetime
* secure storage
* expiration
* logout behavior
* refresh behavior
* CSRF implications
* XSS implications
* cookie security attributes where cookies are used

The project should prefer established framework security mechanisms over custom implementations.

---

# 17. Logging and Sensitive Data

Logs should help diagnose security and application problems without becoming a source of data leakage.

Do not log:

* passwords
* API keys
* authentication tokens
* full private image contents
* unnecessary personal information

Security-relevant events may be logged in a privacy-conscious manner.

Examples:

* failed authentication
* authorization failure
* rate-limit rejection
* invalid upload
* external provider failure

---

# 18. Error Handling

Errors returned to clients should be useful but intentionally limited.

Good:

```text
Something went wrong.
Please try again.
```

or:

```text
You do not have permission to access this resource.
```

Avoid exposing internal implementation details such as:

```text
SqlException:
NpgsqlConnection failed at...
C:\Projects\PlantEncyclopedia\...
```

Detailed diagnostics belong in controlled server-side logs.

---

# 19. Dependency and Supply-Chain Security

Third-party dependencies should be:

* intentionally selected
* kept reasonably up to date
* reviewed before adoption
* scanned where practical

Do not add a package solely because an AI coding tool recommends it.

The project should periodically review:

* NuGet dependencies
* npm dependencies
* GitHub Actions
* Docker base images

---

# 20. CI/CD Security

CI/CD pipelines should avoid exposing secrets.

The pipeline should eventually include security-oriented checks where practical, such as:

* dependency vulnerability scanning
* secret scanning
* build validation
* tests
* static analysis where appropriate

Pull requests should not require developers to paste production credentials into logs or configuration files.

---

# 21. AWS Security

When deployed to AWS:

* use least-privilege IAM
* avoid long-lived credentials where possible
* keep databases private where practical
* expose only required network services
* use HTTPS
* protect object storage
* use managed secrets
* separate development and production resources
* monitor unexpected access and failures

AWS architecture should be driven by actual application requirements rather than adding services solely for résumé keywords.

---

# 22. Threat Model

Important threats for Plant Encyclopedia include:

| Threat                   | Example                               | Primary Defense                        |
| ------------------------ | ------------------------------------- | -------------------------------------- |
| Broken access control    | User accesses another user's favorite | Server-side authorization              |
| Credential theft         | API key exposed in Angular            | Server-side secrets                    |
| Malicious upload         | Non-image file uploaded               | File validation and controlled storage |
| Resource exhaustion      | Huge image upload                     | Size limits                            |
| AI abuse                 | Repeated identification requests      | Rate limiting                          |
| Injection                | Malicious search/input                | Validation + safe data access          |
| XSS                      | Malicious content rendered in UI      | Output encoding/safe rendering         |
| CORS abuse               | Unauthorized origin calling API       | Explicit CORS policy                   |
| Information leakage      | Stack trace returned to client        | Safe error handling                    |
| Secret leakage           | Key committed to Git                  | Secret scanning + environment secrets  |
| Dependency vulnerability | Vulnerable npm/NuGet package          | Dependency review/scanning             |

This threat model should evolve as the application gains functionality.

---

# 23. Security Testing

Security should be tested as part of normal development.

Important tests include:

### Authentication

* unauthenticated user cannot access protected endpoints
* invalid authentication is rejected
* expired/invalid credentials are rejected

### Authorization

* users cannot access another user's favorites
* users cannot modify another user's private data
* protected operations require appropriate permissions

### Input Validation

* invalid IDs are rejected safely
* oversized requests are rejected
* invalid enum values are rejected
* malformed payloads do not cause server errors

### File Upload

* unsupported formats are rejected
* oversized files are rejected
* malicious/non-image files are rejected
* filenames cannot escape controlled storage paths

### Rate Limiting

* repeated expensive requests eventually receive a rate-limit response
* normal requests remain usable

### Error Handling

* production responses do not expose stack traces
* secrets do not appear in responses or logs

---

# 24. Security Verification

Security claims should be demonstrated rather than merely documented.

For portfolio readiness, the project should be able to demonstrate examples such as:

```text
Security Verification

✓ Server-side authorization
✓ Object-level access-control tests
✓ Secure image upload validation
✓ AI API key kept server-side
✓ Rate limiting on expensive endpoints
✓ Safe production error responses
✓ Secret scanning
✓ Dependency vulnerability checks
✓ HTTPS in deployment
✓ Least-privilege cloud configuration
```

Only mark a control as complete when it has actually been implemented and verified.

---

# 25. Portfolio Security Story

The goal is not to claim that Plant Encyclopedia is "fully secure."

Instead, the project should demonstrate a professional security mindset:

> Security was considered during design, implementation, testing, and deployment.

A strong portfolio explanation should focus on concrete engineering decisions:

* why the frontend is not treated as a security boundary
* how object-level authorization prevents unauthorized access
* how image uploads are validated
* how AI credentials are protected
* why rate limiting is necessary for AI endpoints
* how secrets are managed
* how security behavior is tested
* how deployment configuration reduces attack surface

---

# 26. Security Scope Control

Do not introduce unnecessary security infrastructure merely to make the project look sophisticated.

Avoid premature additions such as:

* custom cryptography
* unnecessary microservices
* complex identity infrastructure
* Kubernetes security tooling
* enterprise SIEM systems
* elaborate zero-trust architecture

Use mature framework capabilities first.

Add complexity only when there is a demonstrated requirement or meaningful learning value.

---

# 27. Security Review Milestones

Security should be reviewed at several project milestones:

### Foundation

* secrets/configuration
* dependency hygiene
* secure development defaults

### Authentication

* authentication
* authorization
* object-level access control

### Identification

* upload validation
* privacy
* rate limiting
* AI credential protection

### Deployment

* HTTPS
* CORS
* cloud permissions
* secrets
* network exposure

### Portfolio Release

* dependency scan
* secret scan
* security tests
* production configuration review
* documented limitations

---

# 28. Security Principle

The project's core security principle is:

> **Do not trust the client, minimize privileges, validate external input, protect secrets, and verify security behavior with tests.**

Security should improve together with the product rather than being postponed until the end.
