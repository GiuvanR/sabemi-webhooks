using Microsoft.EntityFrameworkCore;
using SabemiWebhooks.Api.Models;

namespace SabemiWebhooks.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<EventoLogBruto> EventosLogBruto => Set<EventoLogBruto>();
    public DbSet<StatusContrato> StatusContratos => Set<StatusContrato>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventoLogBruto>(entity =>
        {
            entity.ToTable("eventos_log_bruto");
            entity.HasKey(e => e.Id);

            // Chave de idempotência: nenhum id_transacao pode se repetir.
            entity.HasIndex(e => e.IdTransacao)
                  .IsUnique()
                  .HasDatabaseName("ix_eventos_log_bruto_id_transacao");

            entity.HasIndex(e => e.IdContrato);
            entity.HasIndex(e => e.StatusProcessamento);

            entity.Property(e => e.IdTransacao).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IdContrato).HasMaxLength(100).IsRequired();
            entity.Property(e => e.StatusBanco).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Valor).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<StatusContrato>(entity =>
        {
            entity.ToTable("status_contrato");
            entity.HasKey(e => e.IdContrato);
            entity.Property(e => e.IdContrato).HasMaxLength(100);
            entity.Property(e => e.UltimoValorPago).HasColumnType("numeric(18,2)");
        });
    }
}
