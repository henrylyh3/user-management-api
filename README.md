# user-management-api

.net
Command to run:
dotnet new globaljson --sdk-version 10.0.100dotnet dev-certs https --trust
dotnet run --project src/UserManagement.Web/UserManagement.Web.csproj --launch-profile https

Controller -> IUserService -> UserService -> IUserRepository -> UserRepository -> MongoDBClient

Dependency
Web → Application + Core
Application → DataAccess + Core
DataAccess → Core
Core → nothing

# Sign up

Validation->Unique Checking->hash password->create user(role: User)->insert db-> JWT (userid,email,name,role) -> return token and expiry(60min default)

# Sign in

Find email -> match password -> create jwt and return with expiry

# normal api call

UseAuthentication -> validate signature, issuer, audience & expirt -> create user claims -> UseAuthorization check [Authorize] and verify enpoint role -> controller
