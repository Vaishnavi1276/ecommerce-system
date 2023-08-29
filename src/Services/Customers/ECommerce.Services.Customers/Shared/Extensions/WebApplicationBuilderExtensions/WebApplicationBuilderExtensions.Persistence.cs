using BuildingBlocks.Abstractions.Domain.Events.Internal;
using BuildingBlocks.Abstractions.Persistence;
using BuildingBlocks.Persistence.EfCore.Postgres;
using BuildingBlocks.Persistence.Mongo;
using BuildingBlocks.Web.Workers;
using ECommerce.Services.Catalogs.Shared.Data;
using ECommerce.Services.Customers.Customers.Data.Repositories.Mongo;
using ECommerce.Services.Customers.Customers.Data.UOW.Mongo;
using ECommerce.Services.Customers.RestockSubscriptions.Data.Repositories.Mongo;
using ECommerce.Services.Customers.Shared.Contracts;
using ECommerce.Services.Customers.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Services.Customers.Shared.Extensions.WebApplicationBuilderExtensions;

public static partial class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddStorage(this WebApplicationBuilder builder)
    {
        AddPostgresWriteStorage(builder.Services, builder.Configuration);
        AddMongoReadStorage(builder.Services, builder.Configuration);

        return builder;
    }

    private static void AddPostgresWriteStorage(IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>($"{nameof(PostgresOptions)}:{nameof(PostgresOptions.UseInMemory)}"))
        {
            services.AddDbContext<CustomersDbContext>(
                options => options.UseInMemoryDatabase("ECommerce.Services.ECommerce.Services.Customers")
            );

            services.AddScoped<IDbFacadeResolver>(provider => provider.GetService<CustomersDbContext>()!);
            services.AddScoped<IDomainEventContext>(provider => provider.GetService<CustomersDbContext>()!);
        }
        else
        {
            services.AddPostgresDbContext<CustomersDbContext>();

            services.AddHostedService<MigrationWorker>();
            services.AddHostedService<SeedWorker>();

            // add migrations and seeders dependencies, or we could add seeders inner each modules
            services.AddScoped<IMigrationExecutor, CustomersMigrationExecutor>();
            // services.AddScoped<IDataSeeder, Seeder>();
        }

        services.AddScoped<ICustomersDbContext>(provider => provider.GetRequiredService<CustomersDbContext>());
    }

    private static void AddMongoReadStorage(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddMongoDbContext<CustomersReadDbContext>()
            .AddTransient<ICustomerReadRepository, CustomerReadRepository>()
            .AddTransient<IRestockSubscriptionReadRepository, RestockSubscriptionReadRepository>()
            .AddTransient<ICustomersReadUnitOfWork, CustomersReadUnitOfWork>();
    }
}
