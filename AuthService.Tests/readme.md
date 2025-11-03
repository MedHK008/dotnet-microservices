# AuthService.Tests

Suite de tests unitaires (.NET xUnit) couvrant le flux d authentification et de gestion de jetons du service AuthService.

## Infrastructure de test
- **EF Core InMemory** : chaque test cree un `AuthDbContext` isole pour verifier les interactions avec la base sans SQL Server.
- **IConfiguration (Moq)** : fournit des valeurs JWT stables (`Key`, `Issuer`, `Audience`) afin de generer et valider des tokens dans un environnement controle.
- **AuthenticationService** : SUT instancie pour chaque scenario a partir du contexte et de la configuration prepares.

## Scenarios valides
- **RegisterAsync_ShouldCreateUserAndReturnToken** : enregistrement basique retourne un `AuthResponse` non vide.
- **RegisterAsync_ShouldPersistUserToDatabase** : verifie l ecriture de l utilisateur dans la base InMemory.
- **RegisterAsync_MultipleUsers_ShouldRegisterAllSuccessfully** : s assure que plusieurs comptes distincts peuvent etre crees.
- **LoginAsync_WithCorrectCredentials_ShouldReturnToken** : verifie qu un utilisateur valide recupere un nouveau JWT.
- **LoginAsync_WithMultipleUsers_ShouldAuthenticateCorrectly** : confirme que l authentification fonctionne pour plusieurs comptes distincts.
- **RegisterAsync_ShouldHashPassword** / **LoginAsync_ShouldVerifyPasswordCorrectly** : valident le hachage SHA256 et la comparaison.
- **Register / Login token tests** : confirment que les JWT generes ont le format attendu (3 segments) et que `ValidateTokenAsync` accepte les jetons emis par le service.
- **LogoutAsync_WithValidToken_ShouldRevokeToken** : s assure qu un token revoque est invalide au prochain controle.

## Scenarios d erreur
- **RegisterAsync_WithDuplicateEmail_ShouldReturnNull** : detecte les doublons d email et refuse l inscription.
- **LoginAsync_WithWrongPassword / NonexistentUser** : verifient que l auth renvoie `null` si les identifiants ne correspondent pas.
- **ValidateTokenAsync_WithInvalidToken / EmptyToken** : retourne `false` pour des jetons inexistants ou malformes.
- **LogoutAsync_WithUnknownToken_ShouldReturnFalse** : indique l absence du token dans la base.

## Cas limites
- Donnees extremes : emails vides, mots de passe tres longs, multiples enregistrements consecutifs.
- Validations de proprietes : ensure que `CreatedAt` est renseigne, que le hash differe du mot de passe clair.

## Execution
```powershell
cd AuthService.Tests
 dotnet test
```
Les tests s executent en memoire sans dependances externes ; assurez vous simplement que `dotnet test` est disponible dans le PATH.