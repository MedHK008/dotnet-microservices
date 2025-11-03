## Gateway Service

Ce service agit comme une passerelle HTTP unique vers les microservices du projet.

### Fonctionnement général
- Au démarrage, il lit la section `ServiceEndpoints` (`appsettings.json`) et configure un `HttpClient` nommé pour chaque microservice (`AuthService`, `CatalogService`, `CartService`).
- Une politique CORS permissive (`AllowAll`) autorise le front-end à appeler la passerelle depuis n'importe quelle origine.
- L'endpoint `GET /` renvoie un indicateur de santé simple `{ "status": "Gateway is running" }`.
- Toutes les autres requêtes sont capturées via `/{**catchAll}` puis comparées à la liste `routeMappings` afin de trouver le service cible en fonction du préfixe (`/api/auth`, `/api/products`, `/api/cart`, `/health`).
- Si aucun préfixe ne correspond, la passerelle retourne une réponse `404 Route not found`.

### Gestion de l'authentification
- Les chemins sensibles (tout sauf `/api/auth/login`, `/api/auth/register`, `/api/auth/validate`, `/api/auth/logout`) exigent un jeton Bearer valide.
- La passerelle extrait l'en-tête `Authorization`, contacte `AuthService` via `/api/auth/validate` et bloque la requête si le jeton est absent, invalide (`401`) ou si le service d'authentification est indisponible (`503`).

### Transfert des requêtes
- Après validation, la requête entrante est reconstruite (`HttpRequestMessage`) et envoyée au microservice cible : méthode HTTP, corps et en-têtes utiles sont copiés (hors `Host`).
- Les en-têtes de transfert (`Transfer-Encoding`, `Connection`) sont supprimés pour éviter les conflits, puis la réponse du service aval est renvoyée telle quelle au client.
- En cas d'indisponibilité du service cible, la passerelle renvoie `503 { message: "<Service> is unavailable" }`.

### Extension et maintenance
- Ajouter un nouveau microservice se fait en ajoutant son URL dans `ServiceEndpoints` et un préfixe dans `routeMappings`.
- Vérifiez que les URL configurées correspondent à l'environnement (local, Docker, déploiement) et ajoutez des tests d'intégration si de nouvelles règles d'authentification sont introduites.
