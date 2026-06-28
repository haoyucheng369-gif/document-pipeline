FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["CloudDocumentPipeline.sln", "."]
COPY ["src/CloudDocumentPipeline.Api/CloudDocumentPipeline.Api.csproj", "src/CloudDocumentPipeline.Api/"]
COPY ["src/CloudDocumentPipeline.Application/CloudDocumentPipeline.Application.csproj", "src/CloudDocumentPipeline.Application/"]
COPY ["src/CloudDocumentPipeline.Domain/CloudDocumentPipeline.Domain.csproj", "src/CloudDocumentPipeline.Domain/"]
COPY ["src/CloudDocumentPipeline.Infrastructure/CloudDocumentPipeline.Infrastructure.csproj", "src/CloudDocumentPipeline.Infrastructure/"]
RUN dotnet restore "src/CloudDocumentPipeline.Api/CloudDocumentPipeline.Api.csproj"

COPY . .
RUN dotnet publish "src/CloudDocumentPipeline.Api/CloudDocumentPipeline.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CloudDocumentPipeline.Api.dll"]
