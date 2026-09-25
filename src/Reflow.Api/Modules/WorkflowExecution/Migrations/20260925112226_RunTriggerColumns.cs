using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reflow.Api.Modules.WorkflowExecution.Migrations
{
    /// <inheritdoc />
    public partial class RunTriggerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TriggerKind",
                schema: "workflow_execution",
                table: "WorkflowRuns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TriggerName",
                schema: "workflow_execution",
                table: "WorkflowRuns",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerPayloadJson",
                schema: "workflow_execution",
                table: "WorkflowRuns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TriggerKind",
                schema: "workflow_execution",
                table: "WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "TriggerName",
                schema: "workflow_execution",
                table: "WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "TriggerPayloadJson",
                schema: "workflow_execution",
                table: "WorkflowRuns");
        }
    }
}
