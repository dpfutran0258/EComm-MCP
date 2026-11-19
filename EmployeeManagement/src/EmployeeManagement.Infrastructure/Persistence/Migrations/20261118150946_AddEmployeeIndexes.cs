using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmployeeManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_employees_department",
                table: "employees",
                column: "department");

            migrationBuilder.CreateIndex(
                name: "IX_employees_full_name_department",
                table: "employees",
                columns: new[] { "full_name", "department" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_employees_department",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "IX_employees_full_name_department",
                table: "employees");
        }
    }
}
