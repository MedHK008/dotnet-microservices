# CatalogService

Service de catalogue en .NET 9 qui expose une API REST minimale pour consulter les produits disponibles.

## Apercu
- Utilise Entity Framework Core avec SQL Server pour stocker les produits et gerer les migrations.
- Seed de donnees initiales dans `OnModelCreating` afin de fournir un catalogue de base.
- Endpoints REST sous le prefixe `/api/products` pour lister ou recuperer un produit par identifiant.
- Politique CORS permissive (`AllowAll`) pour autoriser les appels du front-end/gateway.

## Architecture et dependances
- **CatalogDbContext** : DbContext EF Core avec une `DbSet<Product>` et le seed des donnees.
- **Product** : entite de domaine (nom, description, prix decimal, image, stock, `CreatedAt`).
- **Program.cs** : configuration minimale (DI, CORS, controllers, migrations et endpoints).

## Configuration
- `ConnectionStrings:DefaultConnection` (appsettings.json) : chaine SQL Server cible (`catalogdb` dans Docker compose par defaut).
- Warning EF Core `PendingModelChangesWarning` ignore pour eviter des logs inutiles au runtime.
- CORS `AllowAll` active; a restreindre en production.

## Flux principal
1. Au demarrage, l application applique les migrations (`Database.Migrate`) afin de garantir le schema a jour.
2. EF Core seed automatiquement les produits definis dans `CatalogDbContext` si la table est vide.
3. Les requetes HTTP entrantes sont traitees par les handlers minimal API :
   - Liste complete -> lecture `ToListAsync`.
   - Detail -> `FindAsync` par cle primaire.
4. Les reponses sont serialisees en JSON avec les donnees du catalogue.

## Endpoints
| Methode | Route | Description | Reponse | Codes |
|---------|-------|-------------|---------|-------|
| GET | `/api/products` | Retourne la liste complete des produits | `IEnumerable<Product>` | 200 |
| GET | `/api/products/{id}` | Retourne un produit specifique | `Product` | 200, 404 |

## Schema de donnees
- Table `Products`
  - `Id` (PK, int)
  - `Name` (nvarchar)
  - `Description` (nvarchar)
  - `Price` (decimal(18,2))
  - `ImageUrl` (nvarchar)
  - `Stock` (int)
  - `CreatedAt` (datetime2, valeur par defaut `UtcNow`)

## Jeu de donnees initial
- 8 produits seeds : laptop, souris, clavier mecanique, ecran 4K, hub USB-C, webcam, casque, SSD externe.
- Chaque entree comprend `Price` precise (decimal), `Stock` et une `ImageUrl` publique.

## Gestion des erreurs
- `GET /api/products/{id}` retourne `404 Not Found` si l identifiant n existe pas.
- Les exceptions SQL/EF Core remonteront vers le middleware par defaut; envisager `UseExceptionHandler` si besoin de reponses RFC 7807.

## Demarrage local
```powershell
# Lancer SQL Server (exemple rapide Docker)
docker run -d -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=pswd" -p 1433:1433 --name catalog-sql mcr.microsoft.com/mssql/server:2022-latest

# Appliquer les migrations et lancer l API
dotnet tool install --global dotnet-ef 2>$null
set ASPNETCORE_ENVIRONMENT=Development
cd CatalogService
 dotnet ef database update
 dotnet run
```

Consulter ensuite `http://localhost:5000/api/products` (ou le port configure) pour verifier les donnees seeds.

## Points d extension
- Ajouter des operations de creation/mise a jour/suppression (POST/PUT/DELETE) avec validation.
- Appliquer un filtrage/pagination/sorting cote base ou via Dapper pour optimiser les requetes.
- Introduire un cache (Redis, MemoryCache) pour reduire la charge SQL.
- Ajouter des tests d integration couvrant le seed et les endpoints pour prevenir les regressions.
