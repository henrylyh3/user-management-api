# user-management-api

.net

# Command to run

dotnet new globaljson --sdk-version 10.0.100dotnet dev-certs https --trust
dotnet run --project src/UserManagement.Web/UserManagement.Web.csproj --launch-profile https

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
