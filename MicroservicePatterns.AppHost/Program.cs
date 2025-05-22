var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CQRS_Library_BorrowingService>("cqrs-library-borrowingservice");

builder.AddProject<Projects.CQRS_Library_BorrowingHistoryService>("cqrs-library-borrowinghistoryservice");

builder.AddProject<Projects.CQRS_Library_BorrowerService>("cqrs-library-borrowerservice");

builder.AddProject<Projects.CQRS_Library_BookService>("cqrs-library-bookservice");

builder.Build().Run();
