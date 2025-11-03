# AuthService

Service d authentification en .NET 9 qui expose une API REST minimale pour l enregistrement, la connexion, la validation et la revocation de jetons JWT.

## Apercu
- Utilise Entity Framework Core avec SQL Server pour stocker les utilisateurs et leurs jetons actifs.
- Genere des jetons JWT signes HMAC SHA256 que les autres services peuvent valider.
- Persist les jetons emis afin de pouvoir les invalider lors d un logout.
- Publie des endpoints REST simples sous le prefixe `/api/auth`.

## Architecture et dependances
- **AuthDbContext** : DbContext EF Core avec les entites `User` et `UserToken` (relation 1-n).
- **AuthenticationService** : logique metier (password hashing SHA256, generation et validation JWT, gestion des tokens en base).
- **DTOs** : structures d echanges HTTP (`RegisterRequest`, `LoginRequest`, `AuthResponse`, `TokenValidationRequest`, `LogoutRequest`).
- **Program.cs** : configuration de l application minimale, enregistrement DI, CORS, endpoints.

## Configuration
- `ConnectionStrings:DefaultConnection` (appsettings.json) : chaine SQL Server (Docker par defaut `authdb`).
- `Jwt:Key` : cle symetrique (minimum 32 caracteres recommande).
- `Jwt:Issuer` et `Jwt:Audience` : valeurs utilisees pour le controle des claims JWT.
- CORS `AllowAll` autorise toutes origines/methodes/headers (adapter en production).

## Flux principal
1. A chaque demarrage, l application applique les migrations EF Core (`Database.Migrate`) afin de creer ou mettre a jour le schema SQL.
2. Les requetes arrivent sur `/api/auth/...` et sont routees vers `AuthenticationService`.
3. Pour `register` et `login`, le mot de passe est hash et compare, un jeton JWT est cree puis enregistre.
4. Pour `validate`, l API verifie l existence du jeton en base puis laisse `JwtSecurityTokenHandler` confirmer la signature, l audience, l issuer et la validite temporelle.
5. Pour `logout`, le jeton correspondant est supprime de `UserTokens`, ce qui le rend invalide pour les appels futurs.

## Endpoints
| Methode | Route | Description | Reponse | Codes |
|---------|-------|-------------|---------|-------|
| POST | `/api/auth/register` | Cree un nouvel utilisateur et retourne un JWT | `AuthResponse` | 200, 400 |
| POST | `/api/auth/login` | Authentifie un utilisateur existant | `AuthResponse` | 200, 401 |
| POST | `/api/auth/validate` | Valide un jeton JWT (utilise par la gateway) | `{ valid: true }` | 200, 401 |
| POST | `/api/auth/logout` | Revoque un jeton prealablement emis | `{ loggedOut: true }` | 200, 404 |

## Schema base de donnees
- Table `Users`
  - `Id` (PK, int)
  - `Email` (unique implicite via logique applicative)
  - `PasswordHash` (SHA256 Base64)
  - `CreatedAt`
- Table `UserTokens`
  - `Id` (PK)
  - `Token` (varchar 450, index unique)
  - `CreatedAt`
  - `UserId` (FK vers `Users`, cascade delete)

## Gestion des mots de passe et jetons
- Hash des mots de passe via `SHA256`.
- JWT signe avec `Jwt:Key` et expire apres 7 jours (`DateTime.UtcNow.AddDays(7)`).
- Les jetons existants d un utilisateur sont retires lors de la generation d un nouveau jeton (`IssueTokenAsync`) pour limiter les sessions paralleles.

## Gestion des erreurs
- `register` renvoie 400 si l utilisateur existe deja.
- `login` renvoie 401 si les identifiants sont invalides.
- `validate` renvoie 401 si le jeton est manquant, inconnu ou invalide (signature, issuer, audience, expiration).
- `logout` renvoie 404 si le jeton n est pas retrouve.

## Demarrage local
```powershell
# Lancer une instance SQL Server (exemple Docker Compose ou conteneur de test)
docker run -d -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=pswd" -p 1433:1433 --name auth-sql mcr.microsoft.com/mssql/server:2022-latest

# Appliquer les migrations et lancer l API en mode Development
set ASPNETCORE_ENVIRONMENT=Development
cd AuthService
 dotnet ef database update
 dotnet run
```

## Points d extension
- Ajouter la verification de confirmation email et reinitialisation de mot de passe.
- Ajouter des roles/claims supplementaires dans le JWT.
- Restreindre CORS et ajouter HTTPS obligatoire en production.
