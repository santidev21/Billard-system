# Stage 1: Build Angular
FROM node:22-alpine AS frontend-build
WORKDIR /app/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build -- --configuration production

# Stage 2: Build API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /app
COPY backend/BilliardSystem.slnx ./
COPY backend/src/BilliardSystem.API/BilliardSystem.API.csproj src/BilliardSystem.API/
COPY backend/src/BilliardSystem.Application/BilliardSystem.Application.csproj src/BilliardSystem.Application/
COPY backend/src/BilliardSystem.Domain/BilliardSystem.Domain.csproj src/BilliardSystem.Domain/
COPY backend/src/BilliardSystem.Infrastructure/BilliardSystem.Infrastructure.csproj src/BilliardSystem.Infrastructure/
RUN dotnet restore src/BilliardSystem.API/BilliardSystem.API.csproj
COPY backend/src/ src/
RUN dotnet publish src/BilliardSystem.API/BilliardSystem.API.csproj -c Release -o /app/publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api-build /app/publish ./
COPY --from=frontend-build /app/frontend/dist/billiard-frontend/browser/ ./wwwroot/
RUN mkdir -p /app/data
EXPOSE 5000
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "BilliardSystem.API.dll"]
