FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY --from=frontend package.json package-lock.json ./
RUN npm ci
COPY --from=frontend . .
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/CreditScoring.Api/CreditScoring.Api.csproj src/CreditScoring.Api/
RUN dotnet restore src/CreditScoring.Api/CreditScoring.Api.csproj
COPY src/CreditScoring.Api/ src/CreditScoring.Api/
RUN dotnet publish src/CreditScoring.Api/CreditScoring.Api.csproj -c Release -o /app --no-restore \
    && rm -rf /app/wwwroot/app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
COPY --from=frontend-build /backend/src/CreditScoring.Api/wwwroot/app ./wwwroot/app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Development
EXPOSE 8080
ENTRYPOINT ["dotnet", "CreditScoring.Api.dll"]
