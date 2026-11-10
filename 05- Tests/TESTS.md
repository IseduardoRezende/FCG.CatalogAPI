# Testes — FCG.CatalogAPI

Este arquivo lista os cenários de teste do microsserviço de catálogo, pedidos e biblioteca. Os nomes seguem o padrão `Metodo_Cenario_ResultadoEsperado`.

| Suíte | Projeto | Ferramentas | Execuções |
|-------|---------|-------------|-----------|
| Unitários | `05- Tests/FCG.Catalog.UnitTests` | xUnit v3, Moq | 43 |
| Integração | `05- Tests/FCG.Catalog.IntegrationTests` | WebApplicationFactory, Testcontainers (PostgreSQL 17), MassTransit Test Harness | 18 |

A contagem de execuções inclui cada caso de `[Theory]`.

## Como executar

```powershell
dotnet test --project "05- Tests/FCG.Catalog.UnitTests/FCG.Catalog.UnitTests.csproj"
dotnet test --project "05- Tests/FCG.Catalog.IntegrationTests/FCG.Catalog.IntegrationTests.csproj"   # requer Docker em execução
```

---

## Testes unitários

### `Order` (entidade de domínio)

| Teste | Regra de negócio verificada |
|-------|-----------------------------|
| `NewOrder_ByDefault_StartsAsPending` | Todo pedido nasce `Pending`. |
| `ApplyPaymentResult_OnPendingOrderWithApprovedPayment_MarksAsApprovedWithProcessingDate` | Um pagamento aprovado leva o pedido a `Approved` e registra a data de processamento. |
| `ApplyPaymentResult_OnPendingOrderWithRejectedPayment_MarksAsRejected` | Um pagamento recusado leva o pedido a `Rejected`. |
| `ApplyPaymentResult_OnAlreadyProcessedOrder_KeepsOriginalStatus` (2 casos) | Um pedido já processado não muda de status. É a base da idempotência. |

### `OrderService` (compra assíncrona)

| Teste | Regra de negócio verificada |
|-------|-----------------------------|
| `PlaceAsync_WithAvailableGame_CreatesPendingOrderWithGameNameAndPriceSnapshot` | O pedido guarda o nome e o preço do jogo no momento da compra. |
| `PlaceAsync_WithAvailableGame_PublishesOrderPlacedEventWithOrderData` | Publica o `OrderPlacedEvent` com o pedido, o usuário, o e-mail, o jogo e o preço. |
| `PlaceAsync_WithAvailableGame_PersistsOrderBeforePublishingEvent` | O pedido é salvo antes da publicação, para que o Payments nunca receba um pedido inexistente. |
| `PlaceAsync_WithNonexistentGame_ReturnsNotFoundAndDoesNotPublish` | Não é possível comprar um jogo inexistente. |
| `PlaceAsync_WithGameAlreadyInLibrary_ReturnsConflictAndDoesNotPublish` | Não é possível comprar um jogo que já está na biblioteca. |
| `PlaceAsync_WithPendingOrderForSameGame_ReturnsConflictAndDoesNotPublish` | Não é possível abrir um segundo pedido enquanto o primeiro está `Pending`. |
| `PlaceAsync_WithInvalidGameId_ReturnsInvalid` (2 casos) | O `gameId` precisa ser positivo. |
| `PlaceAsync_WithTokenWithoutEmail_ReturnsInvalidBecauseNotificationNeedsRecipient` | O token precisa trazer o e-mail, que é o destinatário da confirmação. |
| `ApplyPaymentResultAsync_WithApprovedPayment_MarksOrderApprovedAndAddsGameToLibrary` | Quando o pagamento é aprovado, o pedido vira `Approved` e o jogo entra na biblioteca com o preço pago e o pedido de origem. |
| `ApplyPaymentResultAsync_WithRejectedPayment_MarksOrderRejectedAndDoesNotAddToLibrary` | Quando o pagamento é recusado, o pedido vira `Rejected` e a biblioteca não muda. |
| `ApplyPaymentResultAsync_WhenOrderWasAlreadyProcessed_IgnoresDuplicatedEvent` (2 casos) | Um evento repetido não altera nada (idempotência). |
| `ApplyPaymentResultAsync_WhenOrderDoesNotExist_ReturnsFalseWithoutSaving` | Um evento de pedido desconhecido é ignorado. |
| `ApplyPaymentResultAsync_ApprovedWhenGameAlreadyInLibrary_DoesNotDuplicateLibraryEntry` | A biblioteca nunca recebe o mesmo jogo duas vezes. |
| `GetByIdAsync_ForOrderOfAnotherUser_ReturnsForbidden` | Um usuário só vê os próprios pedidos. |
| `GetByIdAsync_AsAdministrator_ReturnsOrderOfAnyUser` | O administrador vê qualquer pedido. |
| `GetByIdAsync_WhenOrderDoesNotExist_ReturnsNotFound` | Um pedido inexistente retorna `NotFound`. |
| `GetPagedAsync_AsCommonUserWithoutFilter_ListsOnlyOwnOrders` | A listagem de um usuário comum é restrita aos próprios pedidos. |
| `GetPagedAsync_AsCommonUserFilteringAnotherUser_ReturnsForbidden` | Um usuário comum não filtra pedidos de outro usuário. |

### `GameService` (CRUD)

| Teste | Regra verificada |
|-------|------------------|
| `CreateAsync_WithValidData_PersistsTrimmedGame` | O jogo é criado com o nome e a descrição normalizados. |
| `CreateAsync_WithNonPositivePrice_ReturnsInvalidAndDoesNotPersist` (2 casos) | O preço precisa ser maior que zero. |
| `CreateAsync_WithEmptyName_ReturnsInvalid` | O nome é obrigatório. |
| `CreateAsync_WithDescriptionLongerThan500Characters_ReturnsInvalid` | A descrição tem no máximo 500 caracteres. |
| `UpdateAsync_WithValidData_UpdatesNameDescriptionAndPrice` | A edição atualiza todos os campos. |
| `UpdateAsync_WhenGameDoesNotExist_ReturnsNotFound` | Editar um jogo inexistente retorna `NotFound`. |
| `DeleteAsync_WhenGameExists_MarksGameAsDeleted` | A exclusão é lógica. |
| `DeleteAsync_WhenGameDoesNotExist_ReturnsNotFound` | Excluir um jogo inexistente retorna `NotFound`. |
| `GetByIdAsync_WhenGameDoesNotExist_ReturnsNotFound` | Consultar um jogo inexistente retorna `NotFound`. |

### `UserGameService` (biblioteca)

| Teste | Regra verificada |
|-------|------------------|
| `GetLibraryAsync_AsCommonUserWithoutFilter_ReturnsOnlyOwnLibrary` | Um usuário comum sempre vê apenas a própria biblioteca. Isso corrige uma falha do monolito, em que sem o filtro ele via todas. |
| `GetLibraryAsync_AsCommonUserRequestingAnotherUsersLibrary_ReturnsForbidden` | Um usuário comum não acessa a biblioteca de outro usuário. |
| `GetLibraryAsync_AsAdministratorWithoutFilter_ListsAllLibraries` | O administrador vê todas as bibliotecas. |
| `GetLibraryAsync_AsAdministratorFilteringByUser_ListsThatUsersLibrary` | O administrador filtra por usuário. |
| `GetByIdAsync_ForOwnPurchase_ReturnsPurchase` | O usuário vê a própria compra. |
| `GetByIdAsync_ForPurchaseOfAnotherUser_ReturnsForbidden` | O usuário não vê a compra de outro usuário. |
| `GetByIdAsync_WhenPurchaseDoesNotExist_ReturnsNotFound` | Uma compra inexistente retorna `NotFound`. |

### `PaymentProcessedConsumer`

| Teste | Regra verificada |
|-------|------------------|
| `Consume_WithPaymentProcessedEvent_DelegatesToOrderService` | O consumer é fino e delega a regra para o `OrderService`. |

---

## Testes de integração

Os testes sobem a API real com `WebApplicationFactory` em um PostgreSQL do Testcontainers, com as migrations aplicadas. O MassTransit Test Harness substitui o RabbitMQ: verifica o `OrderPlacedEvent` publicado e injeta o `PaymentProcessedEvent` que, em produção, viria do Payments. Os tokens JWT são gerados no próprio teste com a mesma chave e o mesmo issuer do UsersAPI.

| Teste | Cenário ponta a ponta |
|-------|-----------------------|
| `CreateGame_WithoutToken_Returns401` | A criação de jogo exige autenticação. |
| `CreateGame_WithCommonUserToken_Returns403` | A criação de jogo é exclusiva do administrador. |
| `CreateGame_WithInvalidPrice_Returns422` | Um preço zero é rejeitado. |
| `GameCrud_WithAdministratorToken_CreatesReadsUpdatesAndSoftDeletes` | CRUD completo via HTTP; depois da exclusão a consulta retorna 404. |
| `ListGames_WithCommonUserToken_Returns200` | Qualquer usuário autenticado lista os jogos. |
| `Health_WhenDatabaseIsReachable_Returns200` | O `/health` responde OK. |
| `PlaceOrder_ForAvailableGame_Returns202WithPendingOrder` | `POST /orders` responde 202 com o pedido `Pending`, o preço e o e-mail do token. |
| `PlaceOrder_ForAvailableGame_PublishesOrderPlacedEventWithPriceAndEmail` | O `OrderPlacedEvent` é publicado no broker. |
| `PlaceOrder_WhileAnotherOrderForSameGameIsPending_Returns409` | Um segundo pedido do mesmo jogo é bloqueado enquanto o primeiro está pendente. |
| `PlaceOrder_ForNonexistentGame_Returns404` | Um jogo inexistente retorna 404. |
| `PlaceOrder_WithoutToken_Returns401` | A compra exige autenticação. |
| `ApprovedPayment_ForPendingOrder_ApprovesOrderAndAddsGameToLibrary` | Um `PaymentProcessedEvent` Approved deixa o pedido `Approved` e coloca o jogo em `GET /user-games`. |
| `ApprovedPayment_ThenOrderingSameGameAgain_Returns409BecauseGameIsOwned` | Não é possível recomprar um jogo que já está na biblioteca. |
| `RejectedPayment_ForPendingOrder_RejectsOrderAndKeepsLibraryEmpty` | Com Rejected, o pedido fica `Rejected` e a biblioteca continua vazia. |
| `RejectedPayment_ThenOrderingSameGameAgain_AllowsNewOrder` | Depois de uma recusa, o usuário pode tentar comprar de novo. |
| `DuplicatedApprovedPayment_ForSameOrder_AddsGameToLibraryOnlyOnce` | Um evento entregue duas vezes resulta em uma única entrada na biblioteca. |
| `GetLibrary_ForAnotherUserAsCommonUser_Returns403` | Um usuário comum não acessa a biblioteca de outro. |
| `GetOrder_OfAnotherUser_Returns403` | Um usuário comum não acessa o pedido de outro. |
