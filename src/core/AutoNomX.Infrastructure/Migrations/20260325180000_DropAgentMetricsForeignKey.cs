using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoNomX.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropAgentMetricsForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_metrics_agents_AgentId",
                table: "agent_metrics");

            migrationBuilder.DropIndex(
                name: "IX_agent_metrics_AgentId_ModelUsed",
                table: "agent_metrics");

            migrationBuilder.CreateIndex(
                name: "IX_agent_metrics_AgentId_ModelUsed",
                table: "agent_metrics",
                columns: new[] { "AgentId", "ModelUsed" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agent_metrics_AgentId_ModelUsed",
                table: "agent_metrics");

            migrationBuilder.CreateIndex(
                name: "IX_agent_metrics_AgentId_ModelUsed",
                table: "agent_metrics",
                columns: new[] { "AgentId", "ModelUsed" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_metrics_agents_AgentId",
                table: "agent_metrics",
                column: "AgentId",
                principalTable: "agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
