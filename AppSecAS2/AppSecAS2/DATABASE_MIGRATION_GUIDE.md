# Database Migration Instructions

## Overview
This document provides instructions for creating and applying the database migration for the authentication system.

## Migration Commands

### 1. Create Migration

Open Package Manager Console or Terminal in the project directory and run:

```powershell
Add-Migration AddAuthenticationFeatures
```

Or using .NET CLI:

```bash
dotnet ef migrations add AddAuthenticationFeatures
```

### 2. Update Database

Apply the migration to the database:

```powershell
Update-Database
```

Or using .NET CLI:

```bash
dotnet ef database update
```

---

## What This Migration Adds

### Member Table Updates

The migration will add the following columns to the `Members` table:

```sql
ALTER TABLE Members ADD
    FailedLoginCount INT NOT NULL DEFAULT 0,
    LockoutEndUtc DATETIME2 NULL,
    CurrentSessionId NVARCHAR(100) NULL;
```

**Columns:**
- `FailedLoginCount` - Tracks number of failed login attempts
- `LockoutEndUtc` - UTC timestamp when lockout ends (NULL if not locked)
- `CurrentSessionId` - Stores current session GUID for multiple login detection

### New AuditLogs Table

Creates a new table for audit logging:

```sql
CREATE TABLE AuditLogs (
    Id INT PRIMARY KEY IDENTITY(1,1),
    MemberId INT NULL,
    Action NVARCHAR(100) NOT NULL,
    TimestampUtc DATETIME2 NOT NULL,
    IPAddress NVARCHAR(45) NULL,
    UserAgent NVARCHAR(500) NULL,
    Details NVARCHAR(1000) NULL,
    CONSTRAINT FK_AuditLogs_Members_MemberId 
        FOREIGN KEY (MemberId) 
        REFERENCES Members(MemberId) 
        ON DELETE SET NULL
);

CREATE INDEX IX_AuditLog_MemberId ON AuditLogs(MemberId);
CREATE INDEX IX_AuditLog_TimestampUtc ON AuditLogs(TimestampUtc);
```

**Columns:**
- `Id` - Primary key
- `MemberId` - Foreign key to Members table (nullable for anonymous events)
- `Action` - Action performed (LoginSuccess, LoginFailed, Logout, etc.)
- `TimestampUtc` - When the action occurred (UTC)
- `IPAddress` - IP address of the client
- `UserAgent` - Browser/device information
- `Details` - Additional information about the action

**Indexes:**
- `IX_AuditLog_MemberId` - For faster queries by member
- `IX_AuditLog_TimestampUtc` - For date range queries

**Relationships:**
- Foreign key with `ON DELETE SET NULL` - If member is deleted, audit logs remain but MemberId is set to NULL

---

## Verify Migration

### Check Migration Status

```powershell
Get-Migration
```

Or:

```bash
dotnet ef migrations list
```

### View SQL Script (Without Applying)

```powershell
Script-Migration -From 0
```

Or:

```bash
dotnet ef migrations script
```

### Rollback Migration (If Needed)

If you need to rollback:

```powershell
Update-Database -Migration PreviousMigrationName
Remove-Migration
```

Or:

```bash
dotnet ef database update PreviousMigrationName
dotnet ef migrations remove
```

---

## Verify Database Changes

### Check Member Table

```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Members'
AND COLUMN_NAME IN ('FailedLoginCount', 'LockoutEndUtc', 'CurrentSessionId');
```

Expected result:
```
FailedLoginCount    | int       | NO
LockoutEndUtc       | datetime2 | YES
CurrentSessionId    | nvarchar  | YES
```

### Check AuditLogs Table

```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'AuditLogs';
```

### Check Indexes

```sql
SELECT 
    i.name AS IndexName,
    c.name AS ColumnName,
    t.name AS TableName
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name IN ('Members', 'AuditLogs')
ORDER BY t.name, i.name;
```

---

## Seed Data (Optional)

If you want to seed initial data for testing:

```sql
-- Example: Add a test audit log entry
INSERT INTO AuditLogs (MemberId, Action, TimestampUtc, IPAddress, UserAgent)
VALUES (NULL, 'SystemStartup', GETUTCDATE(), '127.0.0.1', 'System');
```

---

## Troubleshooting

### Issue: "The type 'AuditLog' was not found"

**Solution:** Build the project first:
```powershell
dotnet build
```

Then run the migration command again.

### Issue: "Foreign key constraint failed"

**Solution:** Ensure all Members have valid data before adding foreign keys.

### Issue: "Column already exists"

**Solution:** Check if migration was already applied:
```powershell
Get-Migration
```

If applied, you may need to rollback and recreate the migration.

### Issue: "Unable to connect to database"

**Solution:** Check connection string in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BookwormsOnline;..."
  }
}
```

---

## Production Deployment

### Best Practices

1. **Test migration in development first**
2. **Backup database before applying to production**
3. **Review generated SQL script**
4. **Apply during maintenance window**
5. **Monitor application after deployment**

### Generate SQL Script for DBA

```powershell
Script-Migration -From LastProductionMigration -To AddAuthenticationFeatures -Output migration.sql
```

Or:

```bash
dotnet ef migrations script LastProductionMigration AddAuthenticationFeatures -o migration.sql
```

---

## Summary

After running these migrations, you will have:

? **Member Table** updated with lockout and session fields
? **AuditLogs Table** created for security logging
? **Indexes** for optimized queries
? **Foreign Key** relationship configured
? **ON DELETE SET NULL** for data integrity

The database is now ready for the authentication system! ??
