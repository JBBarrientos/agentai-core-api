using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentAI.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentAuditModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRuns_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentRunId = table.Column<int>(type: "int", nullable: false),
                    AgentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InputData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentSteps_AgentRuns_AgentRunId",
                        column: x => x.AgentRunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KbUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentStepId = table.Column<int>(type: "int", nullable: false),
                    ExternalArticleId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArticleTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultedInResolution = table.Column<bool>(type: "bit", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KbUsages_AgentSteps_AgentStepId",
                        column: x => x.AgentStepId,
                        principalTable: "AgentSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_TicketId",
                table: "AgentRuns",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSteps_AgentRunId",
                table: "AgentSteps",
                column: "AgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_KbUsages_AgentStepId",
                table: "KbUsages",
                column: "AgentStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "KbUsages");

            migrationBuilder.DropTable(
                name: "AgentSteps");

            migrationBuilder.DropTable(
                name: "AgentRuns");
        }
    }
}
