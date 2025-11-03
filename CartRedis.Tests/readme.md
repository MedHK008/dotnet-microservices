# CartRedis.Tests

Batterie de tests unitaires verifiant le comportement de `RedisCartStore`, la couche d acces Redis utilisee par le service CartRedis.

## Infrastructure de test
- **Moq** simule `IConnectionMultiplexer` et `IDatabase` afin de controler les lectures/ ecritures Redis sans serveur externe.
- **JsonSerializerOptions (Web)** reproduit la configuration de production pour serialiser/desserialiser les documents panier lors des assertions.
- Les methodes du mock `IDatabase` sont configurees pour inspecter les cles (`cart:{userId}`), les `StringSetAsync`, `StringGetAsync` et `KeyDeleteAsync`.

## Scenarios couverts
- **GetCartAsync_ReturnsEmptyCart_WhenKeyMissing** : confirme qu un panier inexistant renvoie une liste vide plutot qu une erreur.
- **AddOrIncrementItemAsync_PersistsMergedCart** : verifie que l ajout initial persiste bien un document avec la quantite attendue et que la bonne cle Redis est utilisee.
- **AddOrIncrementItemAsync_UpdatesImageUrl_WhenProductExists** : couvre la fusion d un article existant (quantite + meta-donnees).
- **UpdateItemQuantityAsync_DeletesKey_WhenQuantityZero** : assure que fixer la quantite a 0 supprime la cle Redis pour eviter les paniers vides stockes inutilement.
- **RemoveItemAsync_RemovesEntry_WhenProductExists** : teste la suppression selective d un article et la persistence du panier restant.
- **ClearCartAsync_DeletesKey** : verifie que la purge d un panier supprime la cle Redis.

## Bonnes pratiques verifiees
- Usage des cles normalisees (`cart:user@example.com`).
- Respect des methodes asynchrones Redis (`StringSetAsync`, `KeyDeleteAsync`).
- Gestion des cas limites : panier absent, suppression d un article inexistant (renvoie `false`).

## Execution
```powershell
cd CartRedis.Tests
 dotnet test
```
Les tests se contentent d executer le code applicatif en memoire; aucune instance Redis n est requise.