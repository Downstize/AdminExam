using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RecipesGrpc.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Ingredients = table.Column<string>(type: "text", nullable: false),
                    PrepTime = table.Column<int>(type: "integer", nullable: false),
                    CookTime = table.Column<int>(type: "integer", nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Recipes",
                columns: new[] { "Id", "CookTime", "Ingredients", "Instructions", "Name", "PrepTime" },
                values: new object[,]
                {
                    { 1, 60, "Свекла, капуста, картофель, морковь, лук, мясо, томатная паста", "Подготовить овощи, сварить мясной бульон, добавить овощи, приправить.", "Борщ", 20 },
                    { 2, 10, "Картофель, морковь, яйца, колбаса, солёные огурцы, зелёный горошек, майонез", "Отварить картофель, морковь и яйца, нарезать все ингредиенты, смешать с майонезом.", "Оливье", 30 },
                    { 3, 15, "Мука, яйца, вода, фарш (свинина и говядина), лук, специи", "Замесить тесто, сформировать пельмени с фаршем, отварить в кипящей воде.", "Пельмени", 40 },
                    { 4, 30, "Свинина, лук, уксус, соль, специи", "Замариновать мясо, нанизать на шампуры, обжарить на углях.", "Шашлык", 240 },
                    { 5, 15, "Мука, яйца, вода, картофель, лук, сливочное масло", "Замесить тесто, приготовить начинку из картофеля и лука, сформировать вареники, отварить.", "Вареники с картошкой", 30 },
                    { 6, 20, "Мука, молоко, яйца, сахар, соль, растительное масло", "Замесить тесто, жарить блины на сковороде.", "Блины", 10 },
                    { 7, 240, "Свиные ножки, говядина, лук, морковь, чеснок, специи", "Варить мясо до готовности, процедить бульон, добавить чеснок, разлить по формам и остудить.", "Холодец", 30 },
                    { 8, 40, "Копчёности, колбаса, мясо, картофель, солёные огурцы, томатная паста, маслины", "Обжарить копчёности, добавить овощи, сварить суп, украсить маслинами.", "Солянка", 20 },
                    { 9, 60, "Рис, мясо, морковь, лук, чеснок, специи", "Обжарить мясо с овощами, добавить рис, залить водой, готовить до готовности.", "Плов", 20 },
                    { 10, 40, "Курица, лапша, картофель, морковь, лук, специи", "Сварить курицу, добавить овощи и лапшу, варить до готовности.", "Куриный суп с лапшой", 15 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recipes");
        }
    }
}
