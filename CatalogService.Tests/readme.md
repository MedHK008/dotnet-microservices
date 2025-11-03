# CatalogService.Tests

Suite de tests xUnit centrée sur le modèle `Product` et l utilisation du `CatalogDbContext` en base mémoire pour le service Catalogue.

## Infrastructure de test
- **EF Core InMemory** : chaque test construit un `CatalogDbContext` isolé (databaseName auto-généré) pour explorer les opérations CRUD sans SQL Server.
- Les tests manipulent directement `DbSet<Product>` afin de valider le comportement EF (ajout, mise à jour, suppression, requêtes LINQ).

## Thématiques validées
- **Création** : `AddProduct_WithValidData_ShouldSucceed`, `AddProduct_ShouldSetCreatedAtTimestamp` et variantes `Theory` vérifient la persistance initiale, la valeur `CreatedAt` et le support de multiples entrées.
- **Lecture** : scénarios `GetAllProducts`, `GetProductById`, `GetProductsByPrice/Name` assurent le filtrage via LINQ et la gestion des ID inexistants (retour `null`).
- **Mise à jour** : tests `UpdateProduct*` confirment l écriture des modifications, l évolution des stocks et la conservation de `CreatedAt`.
- **Suppression** : `DeleteProduct_*` contrôle que l entité ciblée disparaît sans impacter les autres, en couvrant aussi les suppressions multiples.
- **Gestion de stock** : scénarios `GetLowStockProducts`, `GetOutOfStockProducts`, `CanBuyProduct_*` simulent la logique métier autour des seuils de stock.
- **Propriétés limites** : tests sur prix/stock négatifs ou très élevés ainsi qu un `BulkOperations` massifs pour vérifier la robustesse du contexte InMemory.
- **Scénarios complexes** : `ComplexScenario_AddUpdateDelete_ShouldWorkCorrectly` enchaîne insertion, modification et suppression pour s assurer que le contexte gère bien un cycle complet.

## Objectifs global
- Détecter les régressions dans la configuration EF (type `decimal(18,2)`, timestamps).
- Illustrer comment construire des requêtes LINQ sur le catalogue et vérifier les cas limite (stocks faibles, ID absent).

## Exécution
```powershell
cd CatalogService.Tests
 dotnet test
```
Aucun service externe n est requis : l intégralité des cas utilise la base InMemory fournie par EF Core.