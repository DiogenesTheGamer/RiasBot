using Microsoft.EntityFrameworkCore;
using RiasBot.Database.Models;

namespace RiasBot.Database
{
//    if you want to make a migration with changes made and be applied to the database you must uncomment the following lines
//    and set the string connection in UseNpgsql without getting them from the credentials.json
//    because you will use dotnet ef migrations add <MigrationName> in the console to create a migration that will be applied at the next run of the bot
//
//    public class RiasContextFactory : IDesignTimeDbContextFactory<RiasContext>
//    {
//        public RiasContext CreateDbContext(string[] args)
//        {
//            var optionsBuilder = new DbContextOptionsBuilder<RiasContext>();
//            optionsBuilder.UseNpgsql("");
//            var ctx = new RiasContext(optionsBuilder.Options);
//            return ctx;
//        }
//    }
    public class RiasContext : DbContext
    {
        public DbSet<GuildConfig> Guilds { get; set; }
        public DbSet<UserConfig> Users { get; set; }
        public DbSet<UserGuildConfig> UserGuilds { get; set; }
        public DbSet<Warnings> Warnings { get; set; }
        public DbSet<XpSystem> XpSystem { get; set; }
        public DbSet<Waifus> Waifus { get; set; }
        public DbSet<Patreon> Patreon { get; set; }
        public DbSet<SelfAssignableRoles> SelfAssignableRoles { get; set; }
        public DbSet<XpRolesSystem> XpRolesSystem { get; set; }
        public DbSet<Profile> Profile { get; set; }
        public DbSet<MuteTimers> MuteTimers { get; set; }
        public DbSet<Dailies> Dailies { get; set; }

        public RiasContext(DbContextOptions options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var guildEntity = modelBuilder.Entity<GuildConfig>();
            guildEntity.HasIndex(c => c.GuildId).IsUnique();
            
            var userEntity = modelBuilder.Entity<UserConfig>();
            userEntity.HasIndex(c => c.UserId).IsUnique();
            
            modelBuilder.Entity<UserGuildConfig>();
            modelBuilder.Entity<Warnings>();
            modelBuilder.Entity<XpSystem>();
            modelBuilder.Entity<Waifus>();

            var patreon = modelBuilder.Entity<Patreon>();
            patreon.HasIndex(c => c.UserId).IsUnique();
            
            modelBuilder.Entity<SelfAssignableRoles>();
            modelBuilder.Entity<XpRolesSystem>();
            
            var profile = modelBuilder.Entity<Profile>();
            profile.HasIndex(c => c.UserId).IsUnique();
            
            modelBuilder.Entity<MuteTimers>();
            modelBuilder.Entity<Dailies>();
        }
    }
}