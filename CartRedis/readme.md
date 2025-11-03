# CartRedis Service

Service minimal API en .NET 9 dédié à la gestion des paniers utilisateurs, persistant les données dans Redis.

## Aperçu
- Expose des endpoints REST `api/cart` permettant de consulter et modifier le panier d'un utilisateur.
- Utilise `StackExchange.Redis` pour stocker chaque panier en tant que document JSON compact.
- Sérialisation HTTP configurée en camelCase et ignorante des valeurs nulles.
- Fournit une documentation OpenAPI (Swagger) en environnement `Development`.

## Architecture et dépendances
- **Redis** : base clé/valeur servant de stockage principal. La chaîne de connexion est lue depuis `ConnectionStrings:Redis`.
- **ICartStore / RedisCartStore** : couche d'accès abstraite isolant la logique Redis.
- **Contracts** : objets de transport (`CartItemRequest`, `UpdateCartItemRequest`, `CartResponse`) définissant la surface API.
- **Models** : structures persistées (`CartDocument`, `CartItem`).
- **StackExchange.Redis** : connexion multiplexer unique injectée via DI.

## Configuration
- Fichier `appsettings.json` (production par défaut) : `redis:6379` — présuppose un hostname Docker.
- Fichier `appsettings.Development.json` : `localhost:6379` pour un Redis local.
- La connexion est établie au démarrage (`ConnectionMultiplexer.Connect`). `AbortOnConnectFail` est désactivé pour autoriser les reconnections.

## Endpoints
| Méthode | Route | Description | Conditions de validation |
|---------|-------|-------------|---------------------------|
| GET | `/api/cart/{userId}` | Récupère le panier (`CartResponse`) | `userId` non vide |
| POST | `/api/cart/{userId}/items` | Ajoute ou incrémente un article | `userId` non vide, `quantity > 0`, données produit requises |
| PUT | `/api/cart/{userId}/items/{productId}` | Met à jour la quantité | `quantity >= 0`, supprime l'article si 0 |
| DELETE | `/api/cart/{userId}/items/{productId}` | Supprime un article | Renvoie 404 si aucun article supprimé |
| DELETE | `/api/cart/{userId}` | Vide complètement le panier | Renvoie 204 (No Content) |
| GET | `/health` | Indicateur de santé simple | - |

Tous les endpoints exposent un schéma OpenAPI quand `ASPNETCORE_ENVIRONMENT=Development`.

## Stockage dans Redis
- Les clés suivent le format `cart:{userId}` (userId converti en minuscules et trims).
- La valeur est un JSON conforme à `CartDocument` :
  ```json
  {
    "items": [
      {
        "productId": 42,
        "productName": "Console",
        "price": 299.99,
        "quantity": 2,
        "imageUrl": "https://cdn.example.com/console.jpg"
      }
    ]
  }
  ```
- Si un panier devient vide, la clé est supprimée pour réduire l'empreinte mémoire.

## Flux de traitement principal
1. Validation des entrées (`userId`, `CartItemRequest`, `UpdateCartItemRequest`).
2. Chargement du panier courant via `GetCartAsync` (désérialisation JSON ou création d'un panier vide).
3. Application de la mutation demandée (ajout, incrément, mise à jour, suppression, purge).
4. Persistance dans Redis (`StringSetAsync`) ou suppression de la clé si le panier ne contient aucun article.
5. Construction de la réponse `CartResponse` (quantités totales, prix total) renvoyée au client.

## Gestion des erreurs
- Retour `400 Bad Request` lorsque `userId` est vide ou que les contraintes de quantité/contrat ne sont pas respectées.
- Retour `404 Not Found` si l'article ciblé n'existe pas (update/delete) ou si aucune suppression n'a eu lieu.
- Exceptions Redis non récupérées sont encapsulées par le middleware `UseExceptionHandler`, générant un problème JSON conforme RFC 7807.

## Exécution locale
```powershell
# Démarre Redis via Docker (exemple)
docker run -d -p 6379:6379 --name redis redis:7-alpine

# Lance l'API en mode développement
set ASPNETCORE_ENVIRONMENT=Development
cd CartRedis
 dotnet run
```

Consultez ensuite `https://localhost:5001/swagger` (ou le port configuré) pour explorer l'OpenAPI.

## Points d'extension
- Ajouter de nouveaux champs produit : élargir `CartItem`, mettre à jour la logique de sérialisation.
- Introduire une expiration panier : configurer `StringSetAsync` avec un TTL.
- Instrumenter le service : brancher des métriques (Prometheus) ou logs structurés.
- Renforcer la validation côté contrat (DataAnnotations supplémentaires, FluentValidation, etc.).
