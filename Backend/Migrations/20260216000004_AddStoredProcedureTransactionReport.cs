using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    public partial class AddStoredProcedureTransactionReport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE sp_GetTransactionReport
  @StartDate DATE,
  @EndDate DATE,
  @Status INT = NULL,
  @CustomerId UNIQUEIDENTIFIER = NULL
AS
SELECT c.Name, c.Email, c.CountryCode, o.Id AS OrderId, o.Status, o.CreatedAt, o.CurrencyCode, o.TotalAmount,
  li.ProductSku, li.Quantity, li.UnitPrice
FROM Orders o
JOIN Customers c ON c.Id = o.CustomerId
JOIN OrderLineItems li ON li.OrderId = o.Id
WHERE o.CreatedAt >= @StartDate AND o.CreatedAt < DATEADD(DAY, 1, @EndDate)
  AND (@Status IS NULL OR o.Status = @Status)
  AND (@CustomerId IS NULL OR o.CustomerId = @CustomerId)
ORDER BY o.CreatedAt, li.Id;

SELECT COUNT(DISTINCT o.Id) AS TotalOrders, COALESCE(SUM(o.TotalAmount), 0) AS GrandTotalAmount
FROM Orders o
WHERE o.CreatedAt >= @StartDate AND o.CreatedAt < DATEADD(DAY, 1, @EndDate)
  AND (@Status IS NULL OR o.Status = @Status)
  AND (@CustomerId IS NULL OR o.CustomerId = @CustomerId);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS sp_GetTransactionReport;");
        }
    }
}
