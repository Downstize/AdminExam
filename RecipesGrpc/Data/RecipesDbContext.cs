using Microsoft.EntityFrameworkCore;
using RecipesGrpc.Model;

public class RecipesDbContext : DbContext
{
    public DbSet<RecipeModel> Recipes { get; set; }

    public RecipesDbContext(DbContextOptions<RecipesDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecipeModel>().HasData(
            new RecipeModel { Id = 1, Name = "Борщ", Ingredients = "Свекла, капуста, картофель, морковь, лук, мясо, томатная паста", PrepTime = 20, CookTime = 60, Instructions = "Подготовить овощи, сварить мясной бульон, добавить овощи, приправить." },
            new RecipeModel { Id = 2, Name = "Оливье", Ingredients = "Картофель, морковь, яйца, колбаса, солёные огурцы, зелёный горошек, майонез", PrepTime = 30, CookTime = 10, Instructions = "Отварить картофель, морковь и яйца, нарезать все ингредиенты, смешать с майонезом." },
            new RecipeModel { Id = 3, Name = "Пельмени", Ingredients = "Мука, яйца, вода, фарш (свинина и говядина), лук, специи", PrepTime = 40, CookTime = 15, Instructions = "Замесить тесто, сформировать пельмени с фаршем, отварить в кипящей воде." },
            new RecipeModel { Id = 4, Name = "Шашлык", Ingredients = "Свинина, лук, уксус, соль, специи", PrepTime = 240, CookTime = 30, Instructions = "Замариновать мясо, нанизать на шампуры, обжарить на углях." },
            new RecipeModel { Id = 5, Name = "Вареники с картошкой", Ingredients = "Мука, яйца, вода, картофель, лук, сливочное масло", PrepTime = 30, CookTime = 15, Instructions = "Замесить тесто, приготовить начинку из картофеля и лука, сформировать вареники, отварить." },
            new RecipeModel { Id = 6, Name = "Блины", Ingredients = "Мука, молоко, яйца, сахар, соль, растительное масло", PrepTime = 10, CookTime = 20, Instructions = "Замесить тесто, жарить блины на сковороде." },
            new RecipeModel { Id = 7, Name = "Холодец", Ingredients = "Свиные ножки, говядина, лук, морковь, чеснок, специи", PrepTime = 30, CookTime = 240, Instructions = "Варить мясо до готовности, процедить бульон, добавить чеснок, разлить по формам и остудить." },
            new RecipeModel { Id = 8, Name = "Солянка", Ingredients = "Копчёности, колбаса, мясо, картофель, солёные огурцы, томатная паста, маслины", PrepTime = 20, CookTime = 40, Instructions = "Обжарить копчёности, добавить овощи, сварить суп, украсить маслинами." },
            new RecipeModel { Id = 9, Name = "Плов", Ingredients = "Рис, мясо, морковь, лук, чеснок, специи", PrepTime = 20, CookTime = 60, Instructions = "Обжарить мясо с овощами, добавить рис, залить водой, готовить до готовности." },
            new RecipeModel { Id = 10, Name = "Куриный суп с лапшой", Ingredients = "Курица, лапша, картофель, морковь, лук, специи", PrepTime = 15, CookTime = 40, Instructions = "Сварить курицу, добавить овощи и лапшу, варить до готовности." }
        );
    }
}
