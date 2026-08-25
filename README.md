# ApricotFramework.DataProtection

[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.DataProtection.svg?label=ApricotFramework.DataProtection)](https://www.nuget.org/packages/ApricotFramework.DataProtection/)
[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.DataProtection.AspNetCore.svg?label=ApricotFramework.DataProtection.AspNetCore)](https://www.nuget.org/packages/ApricotFramework.DataProtection.AspNetCore/)
[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.DataProtection.DataOps.svg?label=ApricotFramework.DataProtection.DataOps)](https://www.nuget.org/packages/ApricotFramework.DataProtection.DataOps/)
[![CI](https://github.com/project-apricot/data-protection/actions/workflows/ci.yml/badge.svg)](https://github.com/project-apricot/data-protection/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](https://github.com/project-apricot/data-protection/blob/main/LICENSE)

Relational storage for the ASP.NET Core Data Protection key ring, using plain ADO.NET: one table,
four statements, no Entity Framework and no database driver. Keys survive a restart and are shared
across instances, so a load-balanced app can read its own cookies.

`ApricotFramework.DataProtection` is the **zero-dependency** core.

## Install

```bash
dotnet add package ApricotFramework.DataProtection
dotnet add package ApricotFramework.DataProtection.AspNetCore
```

## Usage

```csharp
using ApricotFramework.DataProtection.AspNetCore.Extensions;

builder.Services.AddDataProtectionCore(builder.Configuration)
    .PersistKeysToRelationalStore(_ => new MySqlConnection(connectionString))
    .ProtectKeysWithCertificate(certificate);
```

```json
{
  "DataProtection": {
    "Application": "/app",
    "Storage": {
      "Relational": { "Dialect": "MySql", "TableName": "DataProtectionKeys" }
    }
  }
}
```

The column names are fixed and match the official Entity Framework Core provider's entity, so one
table can be read by either implementation. Only the table and schema are configurable.

## The caveat that matters

Specifying any explicit key storage location makes the framework **deregister its default at-rest
key encryption**, so key material is written unprotected unless you say otherwise. That is true of
every storage provider, not just this one. Watch for this at startup:

```
warn: No XML encryptor configured. Key {...} may be persisted to storage in unencrypted form.
```

Chain `ProtectKeysWithCertificate`, `ProtectKeysWithAzureKeyVault` or another `ProtectKeysWith*` to
fix it. This library never configures one for you — but since it is what caused the situation, it
will not let the question go unanswered: **a store registered without an encryptor fails at
startup.** Where that is genuinely intended, say so:

```csharp
    .AllowUnprotectedKeys();   // storage encrypts on your behalf, or it is development
```

## Docs

<https://projectapricot.dev/docs/data-protection>
