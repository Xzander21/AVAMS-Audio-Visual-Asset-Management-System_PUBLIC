using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AVEquipmentManager.API.Data;

/// <summary>
/// SaveChanges interceptor that maintains the optimistic-concurrency token
/// for every entity that has a <c>RowVersion</c> property. SQLite has no
/// native rowversion type, so we set the byte[] explicitly on every Add
/// or Modify just before EF emits its UPDATE/INSERT — that gives the
/// usual "WHERE RowVersion = @original" semantic and triggers a clean
/// <see cref="DbUpdateConcurrencyException"/> when two writers race.
///
/// Wired in Program.cs:
///   builder.Services.AddSingleton&lt;RowVersionInterceptor&gt;();
///   builder.Services.AddDbContext&lt;AppDbContext&gt;((sp, opt) =&gt;
///       opt.UseSqlite(conn)
///          .AddInterceptors(sp.GetRequiredService&lt;RowVersionInterceptor&gt;()));
/// </summary>
public sealed class RowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private static void Apply(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            var rv = entry.Metadata.FindProperty("RowVersion");
            if (rv is null) continue;

            entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
        }
    }
}
