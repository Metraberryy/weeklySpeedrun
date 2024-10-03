using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeeklyIL.Migrations
{
    /// <inheritdoc />
    public partial class AnnounceChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "AnnouncementsChannel",
                table: "Guilds",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnouncementsChannel",
                table: "Guilds");
        }
    }
}
