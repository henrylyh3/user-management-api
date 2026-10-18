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

# Database: mongodb

- database is using mongo db free tier cluster, if the application having issue with connection with the database, please connect author (henrylyh96@gmail.com) to whitelist the ip
- image of database table('Users table.png') and index list('Users index.png') is provided on the root folder
- command to add index to email column, alternatively add from mongodb compass GUI

1. use UserManagement
2. db.users.createIndex({ Email: 1 }, { unique: true, name: "ux_users_email" })

# assumption

- new user gets User role and not able to change role
- admin role is created from seed only (admin@admin.com)
- jwt token expire on 60 min
- mongodb is available before api calls

# useful link

- https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/
- https://render.com/docs/configure-environment-variables
- https://render.com/docs/docker

# postman collection

- Postman collection is available at the root folder 'User Management.postman_collection.json'
