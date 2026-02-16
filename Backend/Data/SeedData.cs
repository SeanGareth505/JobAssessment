using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public static class SeedData
{
    private static readonly (string Code, string[] Currencies)[] SadcCountries =
    [
        ("AO", ["AOA"]),
        ("BW", ["BWP"]),
        ("KM", ["KMF"]),
        ("CD", ["CDF"]),
        ("SZ", ["SZL"]),
        ("LS", ["LSL"]),
        ("MG", ["MGA"]),
        ("MW", ["MWK"]),
        ("MU", ["MUR"]),
        ("MZ", ["MZN"]),
        ("NA", ["NAD"]),
        ("SC", ["SCR"]),
        ("ZA", ["ZAR"]),
        ("TZ", ["TZS"]),
        ("ZM", ["ZMW"]),
        ("ZW", ["ZWL", "USD"])
    ];

    public static async Task SeedIfEmptyAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var orderCount = await db.Orders.CountAsync(ct);
        if (orderCount > 0)
        {
            logger.LogInformation("Seed skipped: database already has {Count} orders.", orderCount);
            return;
        }

        logger.LogInformation("Seeding database: products, customers (SADC), and ≥1,000 orders...");

        var products = await EnsureProductsAsync(db, ct);
        var customerIds = await EnsureCustomersAsync(db, ct);

        const int targetOrders = 1050;
        var random = new Random(42);
        var orders = new List<Order>();
        var lineItems = new List<OrderLineItem>();
        var outbox = new List<OutboxMessage>();

        for (var i = 0; i < targetOrders; i++)
        {
            var customerIdx = random.Next(customerIds.Count);
            var (customerId, countryCode, currencyCode) = customerIds[customerIdx];
            var orderId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow.AddDays(-random.Next(0, 90));

            var numLines = random.Next(1, 4);
            decimal totalAmount = 0;
            for (var j = 0; j < numLines; j++)
            {
                var (productId, sku) = products[random.Next(products.Count)];
                var qty = random.Next(1, 5);
                var unitPrice = (decimal)(random.Next(10, 500) + random.NextDouble());
                totalAmount += qty * unitPrice;
                lineItems.Add(new OrderLineItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = productId,
                    ProductSku = sku,
                    Quantity = qty,
                    UnitPrice = Math.Round(unitPrice, 2)
                });
            }

            var order = new Order
            {
                Id = orderId,
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                CreatedAt = createdAt,
                CurrencyCode = currencyCode,
                TotalAmount = Math.Round(totalAmount, 2)
            };
            orders.Add(order);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                order.Id,
                order.CustomerId,
                order.TotalAmount,
                order.CurrencyCode,
                OccurredAtUtc = order.CreatedAt,
                LineItems = lineItems.Where(li => li.OrderId == orderId).Select(li => new { li.Id, li.OrderId, li.ProductSku, li.Quantity, li.UnitPrice }).ToList()
            }, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            outbox.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                AggregateType = "Order",
                AggregateId = orderId,
                Type = "OrderCreated",
                Payload = payload,
                OccurredAtUtc = order.CreatedAt,
                ProcessedAtUtc = null,
                Version = 1
            });
        }

        await db.OrderLineItems.AddRangeAsync(lineItems, ct);
        await db.Orders.AddRangeAsync(orders, ct);
        await db.OutboxMessages.AddRangeAsync(outbox, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seed complete: {Orders} orders, {LineItems} line items, {Outbox} outbox messages.",
            orders.Count, lineItems.Count, outbox.Count);
    }

    private static async Task<List<(Guid Id, string Sku)>> EnsureProductsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Products.AnyAsync(ct))
        {
            var list = await db.Products.Select(p => new { p.Id, p.Sku }).ToListAsync(ct);
            return list.ConvertAll(p => (p.Id, p.Sku));
        }

        var skus = new[] { "WIDGET-A", "WIDGET-B", "GADGET-1", "GADGET-2", "TOOL-X", "TOOL-Y", "PART-100", "PART-200", "SVC-BASIC", "SVC-PRO", "ITEM-01", "ITEM-02", "ITEM-03", "ITEM-04", "ITEM-05" };
        var products = skus.Select(sku => new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = $"Product {sku}",
            CreatedAt = DateTime.UtcNow
        }).ToList();
        await db.Products.AddRangeAsync(products, ct);
        await db.SaveChangesAsync(ct);
        return products.Select(p => (p.Id, p.Sku)).ToList();
    }

    private static async Task<List<(Guid CustomerId, string CountryCode, string CurrencyCode)>> EnsureCustomersAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Customers.AnyAsync(ct))
        {
            var customers = await db.Customers.Select(c => new { c.Id, c.CountryCode }).ToListAsync(ct);
            var dict = SadcCountries.ToDictionary(s => s.Code, s => s.Currencies[0], StringComparer.OrdinalIgnoreCase);
            return customers.ConvertAll(c => (c.Id, c.CountryCode, dict.GetValueOrDefault(c.CountryCode, "ZAR")));
        }

        var list = new List<(Guid, string, string)>();
        var countryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AO"] = "Angola",
            ["BW"] = "Botswana",
            ["KM"] = "Comoros",
            ["CD"] = "DRC",
            ["SZ"] = "Eswatini",
            ["LS"] = "Lesotho",
            ["MG"] = "Madagascar",
            ["MW"] = "Malawi",
            ["MU"] = "Mauritius",
            ["MZ"] = "Mozambique",
            ["NA"] = "Namibia",
            ["SC"] = "Seychelles",
            ["ZA"] = "South Africa",
            ["TZ"] = "Tanzania",
            ["ZM"] = "Zambia",
            ["ZW"] = "Zimbabwe"
        };

        foreach (var (code, currencies) in SadcCountries)
        {
            for (var i = 0; i < 2; i++)
            {
                var id = Guid.NewGuid();
                var name = $"{countryNames.GetValueOrDefault(code, code)} Customer {i + 1}";
                var email = $"seed-{code.ToLowerInvariant()}-{i}@example.com";
                db.Customers.Add(new Customer
                {
                    Id = id,
                    Name = name,
                    Email = email,
                    CountryCode = code,
                    CreatedAt = DateTime.UtcNow
                });
                list.Add((id, code, currencies[0]));
            }
        }

        await db.SaveChangesAsync(ct);
        return list;
    }
}
