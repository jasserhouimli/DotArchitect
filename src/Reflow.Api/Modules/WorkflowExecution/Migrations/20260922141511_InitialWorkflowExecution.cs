using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reflow.Api.Modules.WorkflowExecution.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkflowExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflow_execution");

            migrationBuilder.CreateTable(
                name: "ExecutionLogs",
                schema: "workflow_execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskAttempts",
                schema: "workflow_execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    Log = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskRuns",
                schema: "workflow_execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "text", nullable: false),
                    NodeType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRuns",
                schema: "workflow_execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionLogs_WorkflowRunId",
                schema: "workflow_execution",
                table: "ExecutionLogs",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttempts_TaskRunId",
                schema: "workflow_execution",
                table: "TaskAttempts",
                column: "TaskRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskRuns_WorkflowRunId",
                schema: "workflow_execution",
                table: "TaskRuns",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskRuns_WorkflowRunId_Status",
                schema: "workflow_execution",
                table: "TaskRuns",
                columns: new[] { "WorkflowRunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_Status",
                schema: "workflow_execution",
                table: "WorkflowRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_WorkflowId",
                schema: "workflow_execution",
                table: "WorkflowRuns",
                column: "WorkflowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionLogs",
                schema: "workflow_execution");

            migrationBuilder.DropTable(
                name: "TaskAttempts",
                schema: "workflow_execution");

            migrationBuilder.DropTable(
                name: "TaskRuns",
                schema: "workflow_execution");

            migrationBuilder.DropTable(
                name: "WorkflowRuns",
                schema: "workflow_execution");
        }
    }
}
