> ARCHIVED: historical notes, may not match current code. See README.md.

# Database Setup Guide

## Current Status

✅ **API Server**: Running successfully on `http://localhost:5167`
✅ **Health Endpoint**: Working (`/health`)
✅ **OpenAPI Schema**: Available at `/openapi/v1.json`
✅ **EF Core Migrations**: Created successfully
❌ **Database**: Authentication failing - postgres password not recognized

## The Issue

The .NET application is unable to connect to PostgreSQL because the password for the `postgres` user is not matching what was provided (`root@123`).

**Error Message**: `FATAL: password authentication failed for user "postgres"`

## Solutions

### Option 1: Reset PostgreSQL postgres User Password (Recommended)

If you have access to Windows with admin privileges:

1. Open Services (services.msc) and stop the PostgreSQL service:
   - `postgresql-x64-18`

2. Open Command Prompt as Administrator and navigate to PostgreSQL:
   ```cmd
   "C:\Program Files\PostgreSQL\18\bin\postgres.exe" --single -D "C:\Program Files\PostgreSQL\18\data" template1
   ```

3. At the prompt, run:
   ```sql
   ALTER USER postgres WITH PASSWORD 'root@123';
   \q
   ```

4. Restart the PostgreSQL service

5. Try the .NET application again

### Option 2: Use DBeaver/pgAdmin (Visual Method)

1. Open DBeaver or pgAdmin (which you already have running)
2. Right-click on the `postgres` server
3. Properties → Connection → Set the password to `root@123`
4. Test the connection
5. Navigate to: Tools → Database Tools → SQL Console
6. Run these commands:
   ```sql
   CREATE DATABASE IF NOT EXISTS employee_management_dev;
   CREATE USER IF NOT EXISTS employee_mgmt_user WITH PASSWORD 'dev_password';
   ALTER DATABASE employee_management_dev OWNER TO employee_mgmt_user;
   GRANT ALL PRIVILEGES ON DATABASE employee_management_dev TO employee_mgmt_user;
   ```

### Option 3: Check PostgreSQL Installation

The password might have been set during installation. Common defaults:
- postgres
- admin
- (empty)
- The username field password from installation
- System user password

Try each in the connection string: `Password=<your-password>` in `appsettings.Development.json`

## Current Connection String

Located in: `EmployeeManagement/src/EmployeeManagement.Api/appsettings.Development.json`

```json
"DefaultConnection": "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=<your-password>;Sslmode=disable"
```

## What Happens Once Database Works

1. On next API startup, the migrations will run automatically
2. The `employee_management_dev` database will be created
3. The `Employees` table will be created with all constraints
4. The API endpoints will become fully functional:
   - POST   `/api/employees` - Create employee
   - GET    `/api/employees` - List employees
   - GET    `/api/employees/{id}` - Get specific employee
   - PUT    `/api/employees/{id}` - Update employee
   - DELETE `/api/employees/{id}` - Delete employee
   - GET    `/health` - Health check

## Testing the API (Without Database)

While waiting for database setup, the API is already running:

```bash
# Test health endpoint
curl http://localhost:5167/health

# Test OpenAPI schema
curl http://localhost:5167/openapi/v1.json
```

## Next Steps

1. **Determine the correct postgres password** and update the connection string
2. **Restart the API** - migrations will run automatically on startup
3. **Test the endpoints** with the provided .http file
4. **Build the React UI** - comprehensive feature specs are ready

## Files Modified

- ✅ `EmployeeManagement/src/EmployeeManagement.Api/appsettings.Development.json` - Updated connection string
- ✅ `EmployeeManagement/src/EmployeeManagement.Api/Program.cs` - Added database initialization
- ✅ `EmployeeManagement/src/EmployeeManagement.Infrastructure/Migrations/` - Created initial migration

## Support

For PostgreSQL on Windows, you can also:
- Check `Program Files\PostgreSQL\18\data\pg_log\` for error logs
- Use pgAdmin's query tool to test connection
- Use `psql` CLI if password is known
