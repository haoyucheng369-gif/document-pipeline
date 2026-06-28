FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["CloudDocumentPipeline.sln", "."]
COPY ["src/CloudDocumentPipeline.Worker/CloudDocumentPipeline.Worker.csproj", "src/CloudDocumentPipeline.Worker/"]
COPY ["src/CloudDocumentPipeline.Application/CloudDocumentPipeline.Application.csproj", "src/CloudDocumentPipeline.Application/"]
COPY ["src/CloudDocumentPipeline.Domain/CloudDocumentPipeline.Domain.csproj", "src/CloudDocumentPipeline.Domain/"]
COPY ["src/CloudDocumentPipeline.Infrastructure/CloudDocumentPipeline.Infrastructure.csproj", "src/CloudDocumentPipeline.Infrastructure/"]
RUN dotnet restore "src/CloudDocumentPipeline.Worker/CloudDocumentPipeline.Worker.csproj"

COPY . .
RUN dotnet publish "src/CloudDocumentPipeline.Worker/CloudDocumentPipeline.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CloudDocumentPipeline.Worker.dll"]
