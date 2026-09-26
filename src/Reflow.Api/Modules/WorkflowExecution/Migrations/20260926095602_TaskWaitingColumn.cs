using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reflow.Api.Modules.WorkflowExecution.Migrations
{
    /// <inheritdoc />
    public partial class TaskWaitingColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WaitingOnRunId",
                schema: "workflow_execution",
                table: "TaskRuns",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WaitingOnRunId",
                schema: "workflow_execution",
                table: "TaskRuns");
        }
    }
}
