FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["DocFlowCloud.sln", "."]
COPY ["src/DocFlowCloud.NotificationService/DocFlowCloud.NotificationService.csproj", "src/DocFlowCloud.NotificationService/"]
COPY ["src/DocFlowCloud.Application/DocFlowCloud.Application.csproj", "src/DocFlowCloud.Application/"]
COPY ["src/DocFlowCloud.Domain/DocFlowCloud.Domain.csproj", "src/DocFlowCloud.Domain/"]
COPY ["src/DocFlowCloud.Infrastructure/DocFlowCloud.Infrastructure.csproj", "src/DocFlowCloud.Infrastructure/"]
RUN dotnet restore "src/DocFlowCloud.NotificationService/DocFlowCloud.NotificationService.csproj"

COPY . .
RUN dotnet publish "src/DocFlowCloud.NotificationService/DocFlowCloud.NotificationService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DocFlowCloud.NotificationService.dll"]
