using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcoColeta.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Points",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    PostalCode = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Street = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    City = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    State = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Points", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Email = table.Column<string>(type: "NVARCHAR2(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Role = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pickups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CollectionPointId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    UserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Material = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    WeightKg = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pickups", x => x.Id);
                    table.CheckConstraint("CK_Pickup_Weight", "\"WeightKg\" > 0 AND \"WeightKg\" <= 1000");
                    table.ForeignKey(
                        name: "FK_Pickups_Points_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalTable: "Points",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pickups_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pickups_CollectionPointId",
                table: "Pickups",
                column: "CollectionPointId");

            migrationBuilder.CreateIndex(
                name: "IX_Pickups_ScheduledAt_Id",
                table: "Pickups",
                columns: new[] { "ScheduledAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Pickups_UserId_ScheduledAt_Id",
                table: "Pickups",
                columns: new[] { "UserId", "ScheduledAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Points_PostalCode_Name",
                table: "Points",
                columns: new[] { "PostalCode", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pickups");

            migrationBuilder.DropTable(
                name: "Points");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
