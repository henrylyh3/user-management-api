# user-management-api

.net

Controller -> IUserService -> UserService -> IUserRepository -> UserRepository -> MongoDBClient

Dependency
Web → Application + Core
Application → DataAccess + Core
DataAccess → Core
Core → nothing
