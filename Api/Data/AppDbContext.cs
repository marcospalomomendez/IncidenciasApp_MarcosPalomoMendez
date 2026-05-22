using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Incidencia> Incidencias { get; set; }
    public DbSet<Comentario> Comentarios { get; set; }
    public DbSet<HistorialEstado> HistorialEstados { get; set; }
    public DbSet<Notificacion> Notificaciones { get; set; }
    public DbSet<AuditoriaEntry> Auditoria { get; set; }
    public DbSet<SuscripcionIncidencia> Suscripciones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Incidencia tiene dos relaciones con Usuario, hay que especificarlas
        modelBuilder.Entity<Incidencia>()
            .HasOne(i => i.UsuarioCreador)
            .WithMany()
            .HasForeignKey(i => i.UsuarioCreadorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Incidencia>()
            .HasOne(i => i.TecnicoAsignado)
            .WithMany()
            .HasForeignKey(i => i.TecnicoAsignadoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditoriaEntry>()
            .HasOne(a => a.Incidencia)
            .WithMany()
            .HasForeignKey(a => a.IncidenciaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SuscripcionIncidencia>()
            .HasIndex(s => new { s.UsuarioId, s.IncidenciaId })
            .IsUnique();

        modelBuilder.Entity<SuscripcionIncidencia>()
            .HasOne(s => s.Incidencia)
            .WithMany()
            .HasForeignKey(s => s.IncidenciaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SuscripcionIncidencia>()
            .HasOne(s => s.Usuario)
            .WithMany()
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}