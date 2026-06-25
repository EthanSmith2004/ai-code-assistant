using System;
using AiCodeAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiCodeAssistant.Infrastructure.Migrations
{
    [DbContext(typeof(CodeSightDbContext))]
    [Migration("20260416120000_AddAuthenticationUserOwnership")]
    public partial class AddAuthenticationUserOwnership : Migration
    {
        private static readonly Guid LegacyUserId = new("00000000-0000-0000-0000-000000000001");

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.Sql(
                $"""
                INSERT INTO "Users" ("Id", "Email", "PasswordHash", "CreatedAt")
                VALUES ('{LegacyUserId}'::uuid, 'legacy@codesight.local', 'legacy-migration-placeholder', '2026-04-16T00:00:00Z'::timestamp with time zone);
                """);

            migrationBuilder.DropIndex(
                name: "IX_Projects_SourceIdentifier",
                table: "Projects");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql($"""UPDATE "Projects" SET "UserId" = '{LegacyUserId}'::uuid WHERE "UserId" IS NULL;""");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Projects",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UserId",
                table: "Projects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UserId_SourceIdentifier",
                table: "Projects",
                columns: new[] { "UserId", "SourceIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Users_UserId",
                table: "Projects",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Users_UserId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_UserId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_UserId_SourceIdentifier",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Projects");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_SourceIdentifier",
                table: "Projects",
                column: "SourceIdentifier",
                unique: true);
        }
    }
}
