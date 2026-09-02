using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BilliardSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                column: "Value",
                value: "v2.n24lY7ibAOG6xBOL/zcLuJe5tQjjpJ5jgMDEWK6MnNE=.f1P2zdJqTtmHkttu5sA2Yn15TXqxtnQQiz/psyTRCEE=");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("99999999-0000-0000-0000-000000000099"),
                column: "PasswordHash",
                value: "v2.7RRsRIn2boElw+OqOTKEMIrrplRA2cqxqyfHfXWIDXk=.nbHXdpPFyaxC3dvHO9QgK/UNNzpb+egBDQ0mK+ViWd4=");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_TransactionId",
                table: "IdempotencyKeys",
                column: "TransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyKeys");

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                column: "Value",
                value: "v2.UokvDi9IayJxNXYgJoo7DmAj7tkroSUGLWLjR80WacE=.wdxxZEJI4CXn1w5POzKuE4o8m6SnfF1gjEZpUUcjw/E=");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("99999999-0000-0000-0000-000000000099"),
                column: "PasswordHash",
                value: "v2.Bax0N9mL/dfSP5YSGkj/vRZlxXb4/hNIIQQHWhWcghM=.t+zcsMfAI4IUqodZMRT0pV/m7cQ+Gpki15sYQHmSNAg=");
        }
    }
}
