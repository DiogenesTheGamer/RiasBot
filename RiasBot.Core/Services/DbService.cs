using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RiasBot.Commons.Attributes;
using RiasBot.Database;

namespace RiasBot.Services
{
    [Service]
    public class DbService
    {
        private readonly DbContextOptions<RiasContext> _options;

        public DbService(IBotCredentials creds, RLog log)
        {
            if (creds.DatabaseConfig != null)
            {
                var optionsBuilder = new DbContextOptionsBuilder<RiasContext>();

                var connectionString = new StringBuilder();
                connectionString.Append("Host=").Append(creds.DatabaseConfig.Host).Append(";");

                if (creds.DatabaseConfig.Port > 0)
                    connectionString.Append("Port=").Append(creds.DatabaseConfig.Port).Append(";");

                connectionString.Append("Database=").Append(creds.DatabaseConfig.Database).Append(";")
                    .Append("Username=").Append(creds.DatabaseConfig.Username).Append(";")
                    .Append("Password=").Append(creds.DatabaseConfig.Password);

                optionsBuilder.UseNpgsql(connectionString.ToString());
                _options = optionsBuilder.Options;
                log.Info("Connected to database PostreSQL");
            }
            else
            {
                log.Error("The database connection is not defined in credentials.json");
            }
        }

        public RiasContext GetDbContext()
        {
            if (_options is null)
                return null;

            var context = new RiasContext(_options);
            if (context.Database.GetPendingMigrations().Any())
            {
                var mContext = new RiasContext(_options);
                mContext.Database.Migrate();
                mContext.SaveChanges();
                mContext.Dispose();
            }

            return context;
        }
    }
}