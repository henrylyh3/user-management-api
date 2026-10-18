# user-management-api

.net (10)

# Command to run

dotnet run --project src/UserManagement.Web/UserManagement.Web.csproj --launch-profile http

# Command to run seed

dotnet run --project src/UserManagement.Seed

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

# normal api call

UseAuthentication -> validate signature, issuer, audience & expirt -> create user claims -> UseAuthorization check [Authorize] and verify enpoint role -> controller

# with sorage service

update profile-> update name/email -> return updated profile + image
update profile image -> store image -> update imageurl -> delete previous image(if any) -> return updated profile + image
remove image -> remove image, delete imageurl -> return updated profile + image(null)
get user -> get profile + image

# render variable

ASPNETCORE_ENVIRONMENT =
Cors\_\_AllowedOrigins\_\_0
Jwt\_\_Key
MongoDB\_\_ConnectionString
MongoDB\_\_DatabaseName
CloudflareR2\_\_AccessKeyId
CloudflareR2\_\_SecretAccessKey
CloudflareR2\_\_BucketName
CloudflareR2\_\_PublicBaseUrl
CloudflareR2\_\_S3Endpoint
FileStorage\_\_Provider=CloudflareR2

# Encryption

ASP .Net Core built in PasswordHasher -> generate random salt -> self algorithm to create hash

# file storage (Local / CloudflareR2)

public api -> browser displays the uploaded image
s3 api -> authenticated upload and deletion
