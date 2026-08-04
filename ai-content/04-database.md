# 04 - Modelo de Base de Datos (Database Model)

## Motor de Base de Datos
- **Principal**: SQLite via Entity Framework Core 9.
- **Archivo local**: `billiard_system.db` ubicado en el directorio de la aplicación.
- **Portabilidad**: Mantiene abstracciones limpias mediante DbContext, facilitando migración transparente a PostgreSQL/SQL Server si se escala a la nube.

## Tablas y Estructura

### 1. `Users`
- `Id` (INTEGER, PK, AutoIncrement)
- `Username` (TEXT, Unique, NotNull)
- `PasswordHash` (TEXT, NotNull)
- `FullName` (TEXT, NotNull)
- `Role` (TEXT, NotNull) -- 'Administrador', 'Empleado'
- `IsActive` (INTEGER/BOOLEAN, Default 1)
- `CreatedAt` (TEXT/DATETIME)

### 2. `Settings`
- `Id` (INTEGER, PK)
- `BusinessName` (TEXT, Default 'Billar Tres Bandas')
- `BusinessLogoUrl` (TEXT)
- `DefaultHourlyRate` (DECIMAL, NotNull)
- `DefaultReplaySeconds` (INTEGER, Default 60)
- `MaxReplaySeconds` (INTEGER, Default 180) -- 30s, 60s, 120s, 180s, 300s
- `ReplayQuality` (TEXT, Default '720p')
- `PrimaryColor` (TEXT, Default '#0F5132')

### 3. `Tables`
- `Id` (INTEGER, PK)
- `Number` (INTEGER, Unique, NotNull)
- `Name` (TEXT, NotNull)
- `HourlyRate` (DECIMAL, NotNull)
- `Status` (TEXT, NotNull) -- 'Free', 'Occupied', 'WaiterRequested', 'CheckRequested'
- `CurrentSessionId` (INTEGER, Nullable)

### 4. `Categories`
- `Id` (INTEGER, PK)
- `Name` (TEXT, NotNull) -- 'Cervezas', 'Licores', 'Pasabocas', 'Sin Alcohol'

### 5. `Products`
- `Id` (INTEGER, PK)
- `CategoryId` (INTEGER, FK -> Categories.Id)
- `Name` (TEXT, NotNull)
- `Price` (DECIMAL, NotNull)
- `IsActive` (INTEGER/BOOLEAN, Default 1)

### 6. `MatchHistories`
- `Id` (INTEGER, PK)
- `TableId` (INTEGER, FK -> Tables.Id)
- `TableName` (TEXT, NotNull)
- `GameMode` (TEXT, NotNull) -- 'Managed', 'FreeMode'
- `StartTime` (TEXT/DATETIME, NotNull)
- `EndTime` (TEXT/DATETIME, NotNull)
- `DurationSeconds` (INTEGER, NotNull)
- `FormattedDuration` (TEXT, NotNull)
- `Player1Name` (TEXT, NotNull)
- `Player1Score` (INTEGER, NotNull)
- `Player2Name` (TEXT, NotNull)
- `Player2Score` (INTEGER, NotNull)
- `WinnerName` (TEXT, NotNull)
- `TotalCarambolas` (INTEGER, NotNull)
- `HourlyRateUsed` (DECIMAL, NotNull)
- `TimeCost` (DECIMAL, NotNull)
- `ConsumptionCost` (DECIMAL, NotNull)
- `TotalPaidAmount` (DECIMAL, NotNull)
- `ClosedByRole` (TEXT, NotNull) -- 'Administrador', 'Empleado', 'Usuario'
- `ClosedByName` (TEXT, NotNull)
- `SystemVersion` (TEXT, NotNull)

### 7. `MatchScoreLogs`
- `Id` (INTEGER, PK)
- `MatchHistoryId` (INTEGER, FK -> MatchHistories.Id)
- `PlayerNumber` (INTEGER, NotNull) -- 1 o 2
- `PointsDelta` (INTEGER, NotNull) -- +1, +2, +3, +5, -1
- `ScoreAfter` (INTEGER, NotNull)
- `Timestamp` (TEXT/DATETIME, NotNull)

### 8. `MatchConsumptions`
- `Id` (INTEGER, PK)
- `MatchHistoryId` (INTEGER, FK -> MatchHistories.Id)
- `ProductId` (INTEGER, FK -> Products.Id)
- `ProductName` (TEXT, NotNull)
- `UnitPrice` (DECIMAL, NotNull)
- `Quantity` (INTEGER, NotNull)
- `SubTotal` (DECIMAL, NotNull)
- `OrderedAt` (TEXT/DATETIME, NotNull)
- `AddedByName` (TEXT, NotNull)

### 9. `AuditLogs`
- `Id` (INTEGER, PK)
- `UserId` (INTEGER, Nullable, FK -> Users.Id)
- `UserName` (TEXT, NotNull)
- `Action` (TEXT, NotNull) -- 'ADD_CONSUMPTION', 'START_SESSION', 'CLOSE_SESSION'...
- `EntityType` (TEXT, NotNull)
- `Details` (TEXT, NotNull)
- `Timestamp` (TEXT/DATETIME, NotNull)
