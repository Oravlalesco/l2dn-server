using L2Dn.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NLog;
using Npgsql;

namespace L2Dn.GameServer.Db;

public sealed class DesignTimeGameServerDbContextFactory: IDesignTimeDbContextFactory<GameServerDbContext>
{
    public GameServerDbContext CreateDbContext(string[] args)
    {
        ConfigBase config = ConfigurationUtil.LoadConfig<ConfigBase>();
        return new GameServerDbContext(GetOptions(config.Database));
    }

    public static DbContextOptions<GameServerDbContext> GetOptions(DatabaseConfig config)
    {
        NpgsqlConnectionStringBuilder sb = new()
        {
            Host = config.Server,
            Database = config.DatabaseName,
            Username = config.UserName,
            Password = config.Password
        };

        DbContextOptionsBuilder<GameServerDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(sb.ToString());

        Microsoft.Extensions.Logging.LogLevel logLevel = (Microsoft.Extensions.Logging.LogLevel)config.LogLevel.Ordinal;
        Logger logger = LogManager.GetLogger("Database");
        optionsBuilder.LogTo((_, level) => level >= logLevel,
            data => { logger.Log(LogLevel.FromOrdinal((int)data.LogLevel), data.ToString()); }).EnableDetailedErrors();
        
        optionsBuilder.EnableThreadSafetyChecks(false);

        // EF 9+: Migrate() falla si el snapshot difiere del modelo (p. ej. renombre Character → DbCharacter sin nueva migración).
        // Ignorar permite aplicar migraciones pendientes y arrancar; lo correcto a medio plazo es `dotnet ef migrations add`.
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        
        return optionsBuilder.Options;
    }
}