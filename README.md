# user-management-api

REST API for managing users, authentication, authorization, profile information, and profile images.

# Tech stack

.net 10, ASP.NET Core Web API,
JWT Bearer authentication, Role-based authorization
ASP.NET Core PasswordHasher<TUser> using PBKDF2
Database: MongoDB
Logging: Serilog
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

# Sign up

Validation->Unique Checking->hash password->create user(role: User)->insert db-> JWT (userid,email,name,role) -> return token and expiry(60min default)

# Sign in

Find email -> match password -> create jwt and return with expiry

# api flow

UseAuthentication -> validate signature, issuer, audience & expirt -> create user claims -> UseAuthorization check [Authorize] and verify enpoint role -> controller

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
- command to add index to email column, alternatively add from mongodb compass GUI

1. use UserManagement
2. db.users.createIndex({ Email: 1 }, { unique: true, name: "ux_users_email" })

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

# Unit test

- using Moq & xUnit
- command to run: dotnet test UserManagement.UnitTests/UserManagement.UnitTests.csproj
- test cases covers: signup, signin, get user, get users, update user, update image, delete image

# assumption

- new user gets User role and not able to change role
- admin role is created from seed only (admin@admin.com)
- jwt token expire on 60 min
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

# postman collection

- Postman collection is available at the root folder 'User Management.postman_collection.json'


# CI: publish branch packages

`.github/workflows/publish.yml` runs on pushes to **any branch**, plus manual dispatch. It never connects to your server.

1. Install latest stable .NET 10, validate, restore, build Release, test, and publish with `--no-build --no-restore`.
2. Validate runtime files and generated JSON; include `release.json` with the branch, commit, run and SDK version.
3. Package `.tar.gz` and `.tar.gz.sha256`; verify the archive and checksum.
4. Upload the package to GitHub Actions with **7-day retention**, plus test results separately.

Names include a sanitized branch name, a hash of the full branch name (avoids collisions), commit SHA, run ID and attempt. GitHub artifacts belong to individual runs: an identical artifact name in another run does not overwrite the first. CD resolves the latest successful run for the chosen branch instead. Older successful packages remain available during retention for manual recovery. A new push cancels outdated CI work on that branch; it does not delete an earlier successful package. Retention expires packages independently, so at least two versions are not guaranteed.

# CD: deploy a branch package

`.github/workflows/deploy.yml` is **manual only** and does not rebuild. To expose the Run workflow button, the workflow must exist on the repository's default branch.

1. Open **Actions → CD deploy API → Run workflow**.
2. Choose the trusted workflow/tooling branch in GitHub's **Use workflow from** selector. Normally use the default branch after these files are merged there.
3. Enter the exact source branch in the required **branch** field, for example `feature/initial` or `release/1.0.0`.
4. CD resolves the latest successful push/manual CI run for that branch from `publish.yml`, pins its run/artifact IDs, and shows its commit in the run summary. A newer failed or running CI run does not displace the previous successful run. If the selected successful run's package has expired or is missing, deployment fails and asks for a fresh CI run; it does not silently substitute an older package.
5. Download that exact artifact and validate its checksum and branch/commit/run metadata.
6. Join NetBird, upload using password-authenticated SFTP, and verify the checksum again on the server.
7. Run the deployment script from the trusted workflow branch with the **exact selected archive path**. Stop the service, replace application files, copy server-side Production settings, start the service and check `/health`. Restore previous application files and restart the previous service if deployment fails.

Deployments to this API are serialized across all source branches; a newer request does not cancel a running deployment. GitHub may replace an older pending request with the newest pending request. Deployment summaries identify the selected CI run and commit. An upload or deployment error leaves CD **failed**; its summary links the selected CI run through the earlier selection summary for manual recovery. CI artifacts remain in the successful CI run, not the failed CD run.

Configure **Settings → Environments → production → Environment variables** (all Variables, as requested):

| Name | Value |
| --- | --- |
| `NETBIRD_SETUP_KEY` | NetBird setup key; runner must be allowed to access the server's SSH port. |
| `SSH_PASSWORD` | SSH password for the upload/deployment account. |
| `SSH_KNOWN_HOSTS` | Verified host-key line for the exact hostname/IP used by `SSH_HOST`. |
| `SSH_HOST` | Server NetBird IP or private DNS hostname. |
| `SSH_USERNAME` | SSH account, for example `ubuntu`. |
| `SSH_PORT` | Optional; defaults to `22` if absent. |
| `UPLOAD_PATH` | Absolute writable folder, for example `/henry/release`; letters, digits, underscores, hyphens and slashes only. |

Initial `-` values are rejected until replaced. The same seven previously created Variables are reused; no additional server variables are needed. Values stored as Variables are readable configuration, even though this workflow masks the password/setup key in its logs.

Obtain the SSH host-key line through a trusted server connection, replacing `SERVER_NETBIRD_HOST` with the configured `SSH_HOST`:

```bash
awk -v host="SERVER_NETBIRD_HOST" '{ print host " " $1 " " $2 }' /etc/ssh/ssh_host_ed25519_key.pub
```

For a non-default port, use `[hostname]:port` as the first field. Store the complete output in `SSH_KNOWN_HOSTS`.

## Server prerequisites

- Existing `/etc/systemd/system/user-management-api.service`, .NET 10 ASP.NET Core runtime, Python 3.11+, `tar`, `gzip`, `sha256sum`, `flock`, and `curl`.
- Persistent config at `/henry/config/appsettings.Production.json` and writable `/henry` application/data paths for the SSH user.
- The SSH user must be allowed to start/stop **this service** without an interactive sudo password. Configure through `sudo visudo`, adjusting the account and executable path if necessary:

```sudoers
ubuntu ALL=(root) NOPASSWD: /usr/bin/systemctl start user-management-api.service, /usr/bin/systemctl stop user-management-api.service
```

- The deployment script uses `/henry/user-management-api`, `/henry/config/appsettings.Production.json`, and `http://127.0.0.1:5000/health`. CD uploads the current script to a temporary server folder and runs it; no server-side script update is required for CD. No database seeding is performed.

## Manual recovery

Download the package from its successful **CI run → Artifacts**, extract GitHub's ZIP, then upload both `.tar.gz` and `.tar.gz.sha256` into the server release folder. Use the current `deployment/post-user-management-api.sh` when deploying manually:

```bash
bash /henry/post-user-management-api.sh /henry/release/ACTUAL_ARCHIVE_FILENAME.tar.gz
```

Replace `ACTUAL_ARCHIVE_FILENAME` with the downloaded filename. Without an argument, the script selects the newest API archive in `/henry/release`. It requires the matching checksum and rejects unsafe archive paths before stopping the service. Keep the explicit path for branch-specific recovery.

Checksums detect corruption; they do not independently authenticate a package when an attacker can replace both files. Source-branch deployment runs that branch's application code on your server. Production application secrets stay in server configuration and must not be committed into release packages.
