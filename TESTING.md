# Multi-Sport Platform Testing Guide

This document provides a comprehensive guide to setting up, testing, and verifying the Multi-Sport Platform locally using Docker.

## Prerequisites

Before you begin, ensure you have the following installed:

- [Docker Desktop](https://www.docker.com/products/docker-desktop) (version 20.10+)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/) (for frontend applications)
- [Git](https://git-scm.com/)

## Overview

The platform consists of:
- **Backend**: ASP.NET Core 9 Web API (`apps/api`)
- **Database**: PostgreSQL 16
- **Cache** (optional): Redis 7
- **Frontend**: 
  - Admin Web Application (`apps/admin-web`) - Next.js 14
  - Public Web Application (`apps/public-web`) - Next.js 14
  - Mobile Application (`apps/mobile`) - Flutter (not covered in this guide)

## Step 1: Environment Setup

### Environment Variables

The application requires the following environment variables. These are already set in the `docker-compose.yml` file, but you can override them if needed:

```env
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=db;Database=sports_platform;Username=postgres;Password=postgres123
JwtSettings__Secret=YourSuperSecretKeyThatIsAtLeast32CharactersLong!
JwtSettings__Issuer=SportsPlatform
JwtSettings__Audience=SportsPlatformUsers
JwtSettings__ExpiryMinutes=60
```

### Clone the Repository

If you haven't already cloned the repository:

```bash
git clone <repository-url>
cd multi_sport_platform/sports-platform
```

## Step 2: Starting the Environment with Docker Compose

The platform uses Docker Compose to orchestrate the services.

### Start All Services

```bash
docker-compose up -d
```

This will start:
- `api`: The ASP.NET Core Web API (port 8080)
- `db`: PostgreSQL database (port 5432)
- `redis`: Redis cache (port 6379) - optional but recommended

### Verify Services are Running

Check the status of the containers:

```bash
docker-compose ps
```

You should see all services in the "Up" state.

### Check Logs

To view logs for a specific service:

```bash
docker-compose logs -f api
docker-compose logs -f db
docker-compose logs -f redis
```

### Health Checks

The API service includes a health check endpoint. Wait for the API to be healthy (this may take a minute as the database initializes):

```bash
curl http://localhost:8080/health
```

You should see a response like:
```json
{"status":"Healthy"}
```

The database health check can be verified with:
```bash
docker-compose exec db pg_isready -U postgres
```

## Step 3: Running Backend Tests

The backend includes unit and integration tests in the `backend/Tests` directory.

### Run All Tests

```bash
cd backend
dotnet test SportsPlatform.Tests/SportsPlatform.Tests.csproj
```

### Run Specific Test Categories

You can run specific test categories using categories:

```bash
# Run only unit tests
dotnet test SportsPlatform.Tests/SportsPlatform.Tests.csproj --filter "Category=Unit"

# Run only integration tests
dotnet test SportsPlatform.Tests/SportsPlatform.Tests.csproj --filter "Category=Integration"
```

### Test Coverage

To generate a coverage report:

```bash
dotnet test SportsPlatform.Tests/SportsPlatform.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=lcov
```

The coverage report will be available in the `TestResults` directory.

## Step 4: Running Frontend Tests

The frontend applications (admin-web and public-web) are built with Next.js and include Jest and React Testing Library for unit and integration tests.

### Admin Web Application

```bash
cd apps/admin-web
npm install  # if not already installed
npm test     # or npm run test
```

### Public Web Application

```bash
cd apps/public-web
npm install  # if not already installed
npm test     # or npm run test
```

### End-to-End (E2E) Tests

The frontend applications also include Cypress end-to-end tests. To run them:

```bash
# Make sure the application is running (docker-compose up -d)
cd apps/admin-web
npm run cypress:open   # opens Cypress UI
# or
npm run cypress:run    # runs headless
```

Repeat for `public-web` if E2E tests exist.

## Step 5: Verifying the System is Fully Running

### API Endpoints

Once the API is running, you can test key endpoints:

1. **Swagger UI** (API Documentation):
   ```
   http://localhost:8080/swagger
   ```

2. **Tenants Endpoint** (requires authentication - see below):
   ```
   GET http://localhost:8080/api/tenants
   ```

### Authentication

To test authenticated endpoints, you'll need to obtain a JWT token:

1. Register a tenant (if not already seeded):
   ```bash
   curl -X POST http://localhost:8080/api/tenants/register \
     -H "Content-Type: application/json" \
     -d '{"name":"Test Tenant","email":"admin@test.com","password":"Password123!"}'
   ```

2. Login to get a token:
   ```bash
   curl -X POST http://localhost:8080/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"admin@test.com","password":"Password123!"}'
   ```

   This will return a token that you can use in the Authorization header:
   ```
   Authorization: Bearer <token>
   ```

### Frontend Applications

1. **Admin Web Application**:
   ```
   http://localhost:3000
   ```
   (Note: The admin-web runs on port 3000 by default when started with `npm run dev`)

2. **Public Web Application**:
   ```
   http://localhost:3001
   ```
   (Note: The public-web runs on port 3001 by default when started with `npm run dev`)

> **Note**: The docker-compose file only starts the backend services. To test the frontends locally, you'll need to start them separately:
> ```bash
> cd apps/admin-web
> npm run dev
> 
> cd apps/public-web
> npm run dev
> ```

### Database Verification

You can connect to the PostgreSQL database to verify data:

```bash
# Using psql (if installed)
psql -h localhost -p 5432 -U postgres -d sports_platform
Password: postgres123

# Or using a GUI tool like DBeaver, DataGrip, etc.
```

## Step 6: Stopping and Cleaning Up

### Stop Services

```bash
docker-compose down
```

### Remove Volumes (Data)

```bash
docker-compose down -v
```

> **Warning**: This will delete all data in the database and Redis cache.

## Troubleshooting

### Common Issues

1. **Port Conflicts**
   - If ports 8080, 5432, or 6379 are already in use, either stop the conflicting services or change the ports in `docker-compose.yml`.

2. **Database Connection Issues**
   - Ensure the `db` service is healthy: `docker-compose ps db`
   - Check the database logs: `docker-compose logs db`

3. **API Not Starting**
   - Check the API logs: `docker-compose logs api`
   - Verify environment variables are set correctly

4. **Frontend Cannot Connect to API**
   - Ensure the API is running and accessible from the frontend network
   - Check CORS settings in the API if running frontend separately

### Getting Logs

```bash
# Follow logs for all services
docker-compose logs -f

# Follow logs for a specific service
docker-compose logs -f api
```

## Additional Notes

### Seed Data

The database is seeded with initial data on startup. You can find the seed data in:
`sports-platform/backend/Infrastructure/SportsPlatform.Infrastructure/Persistence/SeedData/SeedData.cs`

### Customizing Docker Compose

For development, you might want to use `docker-compose.dev.yml` which includes volume mounts for live code reloading:
```bash
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

### Testing Email Functionality

The application uses a mock email provider in development. Emails can be viewed in the console output of the API container.

## Conclusion

By following this guide, you should be able to:
1. Start the entire platform using Docker Compose
2. Run backend and frontend tests
3. Verify that all services are healthy and accessible
4. Perform basic API and frontend checks

For further assistance, consult the project documentation in the `docs/` directory or contact the development team.