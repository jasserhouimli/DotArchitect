using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reflow.Api.Modules.WorkflowExecution.Migrations
{
    /// <inheritdoc />
    public partial class ExecutionEngineV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EdgesJson",
                schema: "workflow_execution",
                table: "WorkflowRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfigJson",
                schema: "workflow_execution",
                table: "TaskRuns",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NotBefore",
                schema: "workflow_execution",
                table: "TaskRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputJson",
                schema: "workflow_execution",
                table: "TaskRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "workflow_execution",
                table: "TaskRuns",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EdgesJson",
                schema: "workflow_execution",
                table: "WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "ConfigJson",
                schema: "workflow_execution",
                table: "TaskRuns");

            migrationBuilder.DropColumn(
                name: "NotBefore",
                schema: "workflow_execution",
                table: "TaskRuns");

            migrationBuilder.DropColumn(
                name: "OutputJson",
                schema: "workflow_execution",
                table: "TaskRuns");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "workflow_execution",
                table: "TaskRuns");
        }
    }
}
