using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseServerApi.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherRelatedFieldsToDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GeneratedDate",
                table: "Vouchers",
                newName: "GeneratedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Vouchers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GeneratedAt",
                table: "Vouchers",
                newName: "GeneratedDate");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Vouchers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
