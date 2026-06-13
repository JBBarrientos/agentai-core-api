using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentAI.Migrations
{
    /// <inheritdoc />
    public partial class FixPriorityLabelMediumToModerate_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Tickets SET PriorityLabel = 'Moderate' WHERE PriorityLabel = 'Medium';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
