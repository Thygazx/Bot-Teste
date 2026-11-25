using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) {}

    public DbSet<Agendamento> Agendamentos { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<HorarioFuncionamento> HorarioFuncionamentos { get; set; }
    public DbSet<Servico> Servicos { get; set; }

}