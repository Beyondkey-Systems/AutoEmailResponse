
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Hosting;
using EmailResponseApi;
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var startup = new Startup(builder.Configuration);
        startup.ConfigureServices(builder.Services); // calling ConfigureServices method
        // Add services to the container.
        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddAuthorization();


        // Configuration setup
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        //var connectionString = configuration.GetConnectionString("DatabaseConnection");
        //builder.Services.AddDbContext<EmailPreProcessingContext>(options =>
        //    options.UseSqlServer(connectionString));

        var app = builder.Build();
        startup.Configure(app, builder.Environment); // calling Configure method
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();

        app.Run();
    }
}
