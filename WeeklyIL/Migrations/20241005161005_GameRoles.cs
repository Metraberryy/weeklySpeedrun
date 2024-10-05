using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeeklyIL.Migrations
{
    /// <inheritdoc />
    public partial class GameRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AchievementRole");

            migrationBuilder.AddColumn<string>(
                name: "Game",
                table: "Weeks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "GameRole",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Game = table.Column<string>(type: "TEXT", nullable: false),
                    GuildEntityId = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameRole_Guilds_GuildEntityId",
                        column: x => x.GuildEntityId,
                        principalTable: "Guilds",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WeeklyRole",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Requirement = table.Column<uint>(type: "INTEGER", nullable: false),
                    GuildEntityId = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyRole_Guilds_GuildEntityId",
                        column: x => x.GuildEntityId,
                        principalTable: "Guilds",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameRole_GuildEntityId",
                table: "GameRole",
                column: "GuildEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyRole_GuildEntityId",
                table: "WeeklyRole",
                column: "GuildEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameRole");

            migrationBuilder.DropTable(
                name: "WeeklyRole");

            migrationBuilder.DropColumn(
                name: "Game",
                table: "Weeks");

            migrationBuilder.CreateTable(
                name: "AchievementRole",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildEntityId = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Requirement = table.Column<uint>(type: "INTEGER", nullable: false),
                    RoleId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementRole", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AchievementRole_Guilds_GuildEntityId",
                        column: x => x.GuildEntityId,
                        principalTable: "Guilds",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AchievementRole_GuildEntityId",
                table: "AchievementRole",
                column: "GuildEntityId");
        }
    }
}
