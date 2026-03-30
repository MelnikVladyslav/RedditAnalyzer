# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY RedditAnalyzer/RedditAnalyzer.csproj RedditAnalyzer/
RUN dotnet restore RedditAnalyzer/RedditAnalyzer.csproj

COPY RedditAnalyzer/ RedditAnalyzer/
RUN dotnet publish RedditAnalyzer/RedditAnalyzer.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Create directory for log file
RUN mkdir -p /app/logs

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "RedditAnalyzer.dll"]
