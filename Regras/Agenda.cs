using Microsoft.EntityFrameworkCore;

// Regras de agenda usadas tanto pela rota de horários livres
// quanto pela rota que cria agendamentos (a lógica é escrita uma vez só).
public static class Agenda
{
    // Devolve os intervalos (início e fim) já ocupados em um dia.
    // Só agendamentos Confirmados bloqueiam o horário.
    public static async Task<List<(DateTime Inicio, DateTime Fim)>> ObterOcupadosAsync(
        AppDbContext db, DateOnly data)
    {
        var inicioDia = data.ToDateTime(TimeOnly.MinValue);
        var fimDia = inicioDia.AddDays(1);

        var agendamentos = await db.Agendamentos
            .Where(a => a.Status == StatusAgendamento.Confirmado
                     && a.Inicio >= inicioDia
                     && a.Inicio < fimDia)
            .ToListAsync();

        // O fim de cada agendamento = início + duração do serviço dele.
        var servicos = await db.Servicos.ToDictionaryAsync(s => s.Id);

        var ocupados = new List<(DateTime Inicio, DateTime Fim)>();
        foreach (var agendamento in agendamentos)
        {
            if (servicos.TryGetValue(agendamento.IdServico, out var servico))
            {
                ocupados.Add((agendamento.Inicio, agendamento.Inicio.AddMinutes(servico.TempoMin)));
            }
        }

        return ocupados;
    }

    // Dois intervalos colidem quando cada um começa antes de o outro terminar.
    // Terminar às 10:00 e começar às 10:00 NÃO é colisão.
    public static bool Colide(
        List<(DateTime Inicio, DateTime Fim)> ocupados, DateTime inicio, DateTime fim)
    {
        return ocupados.Any(o => inicio < o.Fim && o.Inicio < fim);
    }
}
