using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    public partial class LinkOrderLineItemToProduct : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "OrderLineItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLineItems_ProductId",
                table: "OrderLineItems",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderLineItems_Products_ProductId",
                table: "OrderLineItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderLineItems_Products_ProductId",
                table: "OrderLineItems");
            migrationBuilder.DropIndex(
                name: "IX_OrderLineItems_ProductId",
                table: "OrderLineItems");
            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "OrderLineItems");
        }
    }
}
