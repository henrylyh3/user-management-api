# user-management-api

REST API for managing users, authentication, authorization, profile information, and profile images.

# Tech stack

.net 10, ASP.NET Core Web API,
JWT Bearer authentication, Role-based authorization
ASP.NET Core PasswordHasher<TUser> using PBKDF2
Database: MongoDB
Logging: Serilog
Transactional email: Resend through IEmailService
File storage: Local / CloudflareR2 (s3 api)
Hosting: Render with docker

# Structure

Web - Controller, middleware, web services
Application - business logic & service interfaces
DataAccess - Repository, Database Client
Core - Models, DTO
Seed - Seeding default db item

# Prerequisites

- install .net 10 sdk
- run 'dotnet restore' to get all required package

# Command to run

Set local email values through environment variables or another untracked configuration source. Do not commit the API key:

`export Resend__ApiKey='re_...' Resend__FromEmail='account@your-verified-domain.example' Resend__FromName='User Management'`

dotnet run --project src/UserManagement.Web/UserManagement.Web.csproj --launch-profile http
Swagger: http://localhost:5000/swagger

# Command to run seed

dotnet run --project src/UserManagement.Seed
Default user (user@user.com [user], admin@admin.com [admin])

# Layered

Controller -> IUserService -> UserService -> IUserRepository -> UserRepository -> MongoDBClient

# Dependency

Dependency
Web -> Application + Core
Application -> DataAccess + Core
DataAccess -> Core
Core -> x
Seed -> Core + DataAccess

# Account and session flow

Sign up -> validate input -> hash password -> create unverified User -> email a single-use verification link -> verify email -> allow sign in

Sign in -> validate password -> require Turnstile after two failed attempts -> enforce temporary lockout after five failed attempts -> create access JWT and rotating refresh token

Forgot password -> validate Turnstile -> return a generic response -> email a short-lived single-use link -> reset password -> revoke every refresh session and invalidate existing access tokens

Change password -> validate the current password and CSRF token -> update password -> revoke every refresh session and invalidate existing access tokens

Session management -> list active refresh sessions -> revoke one owned session -> immediately reject its access token and refresh token

Personal data -> export profile and active-session metadata as JSON, or verify the current password and permanently delete the account and its owned data

Raw verification and reset tokens are sent only in email links. MongoDB stores SHA-256 hashes of those tokens in `accountTokens`; TTL indexes remove expired records.

# Audit events

Security and mutation events are appended to the MongoDB `auditEvents` collection through `IAuditEventWriter`. Each event stores a module, action, outcome, actor user ID, resource, processed client IP, user-agent, trace ID, timestamp, and optional sanitized metadata. Passwords, tokens, cookies, credentials, connection strings, and key values are rejected from metadata.

`Audit:RetentionDays` controls the TTL index and defaults to 90 days. Audit records remain append-only until TTL expiry, including after an account is deleted. Storage failures are logged with the module, action, and exception type without failing the original operation.

Future modules depend on `IAuditEventWriter` and submit an `AuditEventRequest`; they do not access MongoDB directly. Use a stable module and action name, identify the affected resource, and include only non-sensitive metadata.

Admins can query the audit history through `GET /api/audit-events`. The endpoint returns newest events first, supports pagination and filters for module, action, outcome, actor, resource, and date range, and omits stored IP addresses, user agents, and metadata from its response.

# api flow

UseAuthentication -> validate signature, issuer, audience, expiry, user token version, and active session ID -> create user claims -> UseAuthorization checks endpoint role/ownership -> controller

# application flow

Users only able to access own profile
Admin able to access user list and able to edit information as per user access
update profile: update name/email -> return updated profile + image
update profile image: store image -> update imageurl -> delete previous image(if any) -> return updated profile + image
remove image: remove image, delete imageurl -> return updated profile + image(null)
get user: get profile + image

# Encryption

ASP .Net Core built in PasswordHasher -> generate random salt -> self algorithm to create hash

# file storage (Local / CloudflareR2)

IFileStorageService is able to decide which storage to use from appsetting before build
public api -> browser displays the uploaded image
s3 api -> authenticated upload and deletion
-using AWSSDK package s3 api to connect to cloudflare via s3 api

# Database: mongodb

- image of database table('Users table.png') and index list('Users index.png') is provided on the root folder
- MongoDB indexes are reconciled automatically at application startup. The managed indexes cover unique user email, refresh-token hashes/users/expiry, account-token hashes/users/purposes/expiry, and audit-event retention/query fields.

# hosting (Render free tier)

- The application is deployed as a docker web service on render free tier
- Production API: https://user-management-api-md95.onrender.com
- Health check: GET https://user-management-api-md95.onrender.com/health

# render variable

ASPNETCORE_ENVIRONMENT =
Cors\_\_AllowedOrigins\_\_0 =
Jwt\_\_Key =
MongoDB\_\_ConnectionString =
MongoDB\_\_DatabaseName =
CloudflareR2\_\_AccessKeyId =
CloudflareR2\_\_SecretAccessKey =
CloudflareR2\_\_BucketName =
CloudflareR2\_\_PublicBaseUrl =
CloudflareR2\_\_S3Endpoint =
FileStorage\_\_Provider=CloudflareR2
RateLimiting\_\_TrustCloudflareConnectingIp=true
DataProtection\_\_CertificateBase64=
DataProtection\_\_CertificatePassword=
AccountSecurity\_\_FrontendBaseUrl=https://your-frontend.example.com
Email\_\_Provider=Resend
Resend\_\_ApiKey=
Resend\_\_FromEmail=
Captcha\_\_SecretKey=
Captcha\_\_AllowedHostnames\_\_0=user-management-web-eta.vercel.app

Set `RateLimiting__TrustCloudflareConnectingIp=true` only for the public Render deployment, where Render routes traffic through Cloudflare. Keep it `false` for local development or any deployment that can receive requests without Cloudflare.

ASP.NET Core Data Protection keys are stored in the configured MongoDB database so CSRF cookies remain valid after a Render restart or redeploy. In production, the persisted keys are encrypted with the password-protected PFX certificate from the two DataProtection Render variables. MongoDB access must remain restricted and encrypted in transit and at rest.

Generate the certificate once and store only its Base64 PFX value and password as Render secrets:

`openssl req -x509 -newkey rsa:2048 -keyout data-protection-key.pem -out data-protection-cert.pem -days 3650 -nodes -subj "/CN=user-management-data-protection" && openssl pkcs12 -export -out data-protection.pfx -inkey data-protection-key.pem -in data-protection-cert.pem && base64 < data-protection.pfx | tr -d '\n'`

Create a Resend account, verify a sender domain, create an API key with sending access, and put the key only in `Resend__ApiKey`. Set `Resend__FromEmail` to an address on the verified domain. The API depends on `IEmailService`, so another provider can replace `ResendEmailService` without changing the account workflow. Transient Resend failures are retried three times with exponential backoff and jitter. Each send uses an idempotency key to prevent duplicate delivery during retries. Verification and password-reset emails are limited to one request per account per 60 seconds by default.

Cloudflare Turnstile protects forgot-password requests and sign-in after two failed password attempts. Set `Captcha__SecretKey` to the widget secret and `Captcha__AllowedHostnames__0` to the frontend hostname without a scheme or path. Signup does not require CAPTCHA. Local development uses Cloudflare's official test keys from ignored development configuration. Turnstile failures are rejected and recorded as safe audit events without storing the token.

OpenTelemetry tracing and metrics are enabled. Set the standard `OTEL_EXPORTER_OTLP_ENDPOINT` and exporter authentication variables only when an OTLP collector is configured. API responses include `X-Trace-Id`, and request/error logs use the same trace ID for correlation. Never expose exporter credentials to the frontend.

# Unit test

- using Moq & xUnit
- command to run: dotnet test UserManagement.UnitTests/UserManagement.UnitTests.csproj
- test cases cover signup verification, signin CAPTCHA/lockout, Turnstile validation, refresh-token rotation/reuse, session listing/revocation, password reset/change, personal-data export/deletion, audit enrichment/filtering/failure handling, get user, paginated users, update user, update image, and delete image

# assumption

- new user gets User role and not able to change role
- admin role is created from seed only (admin@admin.com)
- jwt token expire on 60 min
- password reset links expire after 30 minutes and verification links expire after 24 hours by default
- mongodb is available before api calls

# limitation

- render, mongodb, cloudflareR2 file storage all runs on free tier
- render will enter sleep mode when inactive and will require around 1 minute for cold start
- mongodb only allow whitelisted IP(s) to access, if /health is OK but database continue to fail, contact the author (henrylyh96@gmail.com)
- Cloudflare R2 includes 10 GB-month of storage in its free tier. Usage beyond this limit may incur charges.

# useful link

- https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/
- https://render.com/docs/configure-environment-variables
- https://render.com/docs/docker
- https://resend.com/docs/api-reference/emails/send-email
- https://developers.cloudflare.com/turnstile/get-started/

# postman collection

- Postman collection is available at the root folder 'User Management.postman_collection.json'
