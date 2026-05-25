using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentAI.Migrations
{
	/// <inheritdoc />
	public partial class AddTicket : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.CreateTable(
				name: "Tickets",
				columns: table => new
				{
					Id = table.Column<int>(type: "int", nullable: false)
						.Annotation("SqlServer:Identity", "1, 1"),
					SysId = table.Column<string>(type: "nvarchar(max)", nullable: false),
					Number = table.Column<string>(type: "nvarchar(max)", nullable: false),
					Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
					Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
					State = table.Column<int>(type: "int", nullable: false),
					StateLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
					Priority = table.Column<int>(type: "int", nullable: false),
					PriorityLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
					OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
					ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
					LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_Tickets", x => x.Id);
				});
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
				name: "Tickets");
		}
	}
}