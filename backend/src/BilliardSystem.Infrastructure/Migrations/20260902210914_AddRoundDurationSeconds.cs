using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BilliardSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundDurationSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "MatchRounds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "MatchRounds");

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
                column: "Value",
                value: "v2./69jQ5ktXiudDc6z+hTg2/V5BQTbXb5Nw0+qya8zcXc=.lElFgK+XGj4GKaqFbOrfwZPVr8+o55WnWoOnlltG13k=");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("99999999-0000-0000-0000-000000000099"),
                column: "PasswordHash",
                value: "v2.GOnB5HfSBQFflA94xhKpcFB2g6e8T3IX1PqR7/q4DUM=.vtqDQzBDi9WYStn0IuShJ0SXsRpIBDCTBgFPJsbnu5s=");
        }
    }
}
