# LFZ Plot Management

Login-gated internal web app for the Lagos Free Zone master plan: **viewers**
explore the plan, **requesters** submit plot requests, **allocators** approve or
reject them, and **admins** manage users, settings and exceptions.

Built with .NET 8 (clean architecture), SQL Server (spatial), Blazor Server and
a DWG→seed extraction pipeline. Maintenance is **data-only**: re-run the
extractor when the drawing changes, or use the Admin UI for tenant / status /
request maintenance.

## Solution structure

| Project | Purpose |
| --- | --- |
| `src/LFZ.Domain` | Entities, enums, `PlotRules` workflow invariants (no dependencies) |
| `src/LFZ.Application` | Use-case services, DTOs, `IApplicationDbContext` / `IJwtService` abstractions |
| `src/LFZ.Infrastructure` | EF Core (SQL Server + NetTopologySuite), Identity, JWT, migrations, seed |
| `src/LFZ.Api` | REST API + SignalR hub, JWT bearer auth, Swagger |
| `src/LFZ.Web` | Blazor Server app — interactive SVG map, dashboard, admin settings, cookie auth |
| `tools/LFZ.Tools.PlotExtractor` | DWG→DXF→seed pipeline (console app) |
| `tests/LFZ.Domain.Tests` | xUnit tests for the domain workflow rules |

Documentation: [docs/architecture.md](docs/architecture.md) ·
[docs/database-schema.md](docs/database-schema.md) ·
[docs/dwg-extraction-report.md](docs/dwg-extraction-report.md) ·
standalone prototype: `LFZ-prototype.html` (open directly in a browser — full
status workflow against the real DWG geometry, no backend).

## Run locally (macOS)

1. **SQL Server** (Docker):

   ```bash
   docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='YourStrong!Pass1' \
     -p 1433:1433 -d --name lfz-sql mcr.microsoft.com/mssql/server:2022-latest
   ```

2. **Configure** the connection string in `src/LFZ.Api/appsettings.json` and
   `src/LFZ.Web/appsettings.json` (or use user-secrets / env vars).

3. **Create the database:**

   ```bash
   dotnet tool install --global dotnet-ef --version "8.*"   # once
   dotnet ef database update --project src/LFZ.Infrastructure --startup-project src/LFZ.Api
   ```

4. **Run** (roles, pilot users, settings and the 55 plots seed on first start):

   ```bash
   dotnet run --project src/LFZ.Api     # Swagger: http://localhost:5088/swagger
   dotnet run --project src/LFZ.Web     # App:     http://localhost:5188
   ```

5. **Sign in** with a pilot account:

   | Role | Email | Password |
   | --- | --- | --- |
   | Viewer | `viewer@example.com` | `Viewer123` |
   | Requester | `requester@example.com` | `Requester123` |
   | Allocator | `allocator@example.com` | `Allocator123` |
   | Admin | `admin@example.com` | `Admin123` |

## Tests

```bash
dotnet test
```

## DWG extraction (data-only maintenance)

```bash
python3 -m venv .venv-cad && .venv-cad/bin/pip install aspose-cad shapely ezdxf   # once
.venv-cad/bin/python tools/convert_dwg_to_dxf.py "<drawing>.dwg" masterplan.dxf
cd tools/LFZ.Tools.PlotExtractor
dotnet run -- ../../masterplan.dxf --codes none        # complete zone, all phases
cp out/plots-seed.json ../../src/LFZ.Infrastructure/Seed/plots-seed.json
python3 ../tag_phases.py             # tag phases from extent WKTs
python3 ../extract_plot_labels.py    # real plot codes/names/land-use + true-metre scale
python3 ../extract_hatch_colors.py   # land-use hatch colours from the DWG legend
python3 ../build_prototype.py        # refresh LFZ-prototype.html
```

See [docs/dwg-extraction-report.md](docs/dwg-extraction-report.md) for the full
methodology and current results (414 rings → 124 seeded parcels, complete zone).

## Deploy to Azure

```bash
cd deploy
./deploy.sh <resource-group> [location]
```

Provisions an App Service plan with two Linux web apps (API + Web), Azure SQL
and Application Insights via [deploy/main.bicep](deploy/main.bicep), then
publishes both apps. Replace the placeholder secrets in
`main.parameters.json` with Key Vault references before production use.
