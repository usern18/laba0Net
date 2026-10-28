# Component diagram after OrderingService extraction

```mermaid
flowchart LR
	Client[Client application] --> Gateway[API Gateway / Ocelot :5000]
	Gateway --> Auth[AuthService :5001]
	Gateway --> Catalog[CatalogService :5002]
	Gateway --> Ordering[OrderingService :5003]
	Gateway --> Monolith[Remaining monolith :5100]
	Auth --> AuthDb[(Auth SQLite DB)]
	Catalog --> CatalogDb[(Catalog SQLite DB)]
	Ordering --> OrdersDb[(Ordering SQLite DB)]
	Ordering -. HTTP item lookup with 2s timeout .-> Catalog
	Auth -. issues JWT .-> Client
	Ordering -. validates JWT locally .-> Jwt[Shared Jwt settings]
	Catalog -. validates JWT locally .-> Jwt
```

`/api/orders` is routed to OrderingService before the gateway catch-all route to the monolith. OrderingService owns the Orders table; `ProductId` is a value reference only and there is no cross-service database foreign key.
