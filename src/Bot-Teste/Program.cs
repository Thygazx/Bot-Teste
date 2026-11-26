using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=agendamento.db"));
var app = builder.Build();

// ======================= SERVIÇOS =======================

app.MapGet("/servicos", async (AppDbContext db) => await db.Servicos.ToListAsync());

app.MapPost("/servicos", async (Servico servico, AppDbContext db) =>
{
    // Duração zero ou negativa quebraria o cálculo dos encaixes.
    if (servico.TempoMin <= 0)
    {
        return Results.BadRequest("A duração precisa ser maior que zero");
    }

    // Compara sem diferenciar maiúsculas de minúsculas ("Consulta" = "consulta").
    var nome = servico.Nome.ToLower();
    var jaExiste = await db.Servicos.AnyAsync(s => s.Nome.ToLower() == nome);
    if (jaExiste)
    {
        return Results.Conflict("Esse serviço já existe");
    }

    db.Servicos.Add(servico);
    await db.SaveChangesAsync();
    return Results.Created($"/servicos/{servico.Id}", servico);
});

// ================ HORÁRIOS DE FUNCIONAMENTO ================

app.MapGet("/horarios", async (AppDbContext db) => await db.HorarioFuncionamentos.ToListAsync());

app.MapPost("/horarios", async (HorarioFuncionamento horario, AppDbContext db) =>
{
    if (horario.Abertura >= horario.Fechamento)
    {
        return Results.BadRequest("A abertura precisa ser antes do fechamento");
    }

    var jaExiste = await db.HorarioFuncionamentos.AnyAsync(h => h.DiaDaSemana == horario.DiaDaSemana);
    if (jaExiste)
    {
        return Results.Conflict("Esse dia já tem horário cadastrado");
    }

    db.HorarioFuncionamentos.Add(horario);
    await db.SaveChangesAsync();
    return Results.Created($"/horarios/{horario.Id}", horario);
});

// ======================= CLIENTES =======================

app.MapGet("/clientes", async (AppDbContext db) => await db.Clientes.ToListAsync());

app.MapPost("/clientes", async (Cliente cliente, AppDbContext db) =>
{
    // O telefone é o que identifica uma pessoa no WhatsApp.
    var jaExiste = await db.Clientes.AnyAsync(c => c.Telefone == cliente.Telefone);
    if (jaExiste)
    {
        return Results.Conflict("Já existe um cliente com esse telefone");
    }

    db.Clientes.Add(cliente);
    await db.SaveChangesAsync();
    return Results.Created($"/clientes/{cliente.Id}", cliente);
});

// ===================== AGENDAMENTOS =====================

app.MapGet("/agendamentos", async (AppDbContext db) => await db.Agendamentos.ToListAsync());

app.MapPost("/agendamentos", async (Agendamento agendamento, AppDbContext db) =>
{
    // O banco não confere as ligações, então a rota confere.
    var clienteExiste = await db.Clientes.AnyAsync(c => c.Id == agendamento.IdCliente);
    if (!clienteExiste)
    {
        return Results.BadRequest("Cliente não existe");
    }

    var servico = await db.Servicos.FirstOrDefaultAsync(s => s.Id == agendamento.IdServico);
    if (servico == null)
    {
        return Results.BadRequest("Serviço não existe");
    }

    // A clínica abre nesse dia da semana?
    var dia = agendamento.Inicio.DayOfWeek;
    var funcionamento = await db.HorarioFuncionamentos.FirstOrDefaultAsync(h => h.DiaDaSemana == dia);
    if (funcionamento == null)
    {
        return Results.BadRequest("A clínica não funciona nesse dia");
    }

    // O atendimento começa e termina dentro do horário de funcionamento?
    var inicioHora = TimeOnly.FromDateTime(agendamento.Inicio);
    var fimHora = inicioHora.AddMinutes(servico.TempoMin);
    if (inicioHora < funcionamento.Abertura || fimHora <= inicioHora || fimHora > funcionamento.Fechamento)
    {
        return Results.BadRequest("Fora do horário de funcionamento");
    }

    // Só agendamentos confirmados disputam horário.
    if (agendamento.Status == StatusAgendamento.Confirmado)
    {
        var ocupados = await Agenda.ObterOcupadosAsync(db, DateOnly.FromDateTime(agendamento.Inicio));
        var fim = agendamento.Inicio.AddMinutes(servico.TempoMin);
        if (Agenda.Colide(ocupados, agendamento.Inicio, fim))
        {
            return Results.Conflict("Esse horário já está ocupado");
        }
    }

    db.Agendamentos.Add(agendamento);
    await db.SaveChangesAsync();
    return Results.Created($"/agendamentos/{agendamento.Id}", agendamento);
});

// Cancelar não apaga: muda o status e libera o horário.
app.MapPut("/agendamentos/{id}/cancelar", async (int id, AppDbContext db) =>
{
    var agendamento = await db.Agendamentos.FindAsync(id);
    if (agendamento == null)
    {
        return Results.NotFound("Agendamento não encontrado");
    }

    agendamento.Status = StatusAgendamento.Cancelado;
    await db.SaveChangesAsync();
    return Results.Ok(agendamento);
});

// ==================== HORÁRIOS LIVRES ====================

app.MapGet("/horarios-livres", async (DateOnly data, int servicoId, AppDbContext db) =>
{
    // 1. A clínica abre nesse dia?
    var dia = data.DayOfWeek;
    var funcionamento = await db.HorarioFuncionamentos.FirstOrDefaultAsync(h => h.DiaDaSemana == dia);
    if (funcionamento == null)
    {
        return Results.NotFound("fechado");
    }

    // 2. O serviço existe?
    var servico = await db.Servicos.FirstOrDefaultAsync(s => s.Id == servicoId);
    if (servico == null)
    {
        return Results.NotFound("Serviço não encontrado");
    }
    if (servico.TempoMin <= 0)
    {
        return Results.BadRequest("Serviço com duração inválida");
    }

    // 3. O que já está ocupado nesse dia?
    var ocupados = await Agenda.ObterOcupadosAsync(db, data);

    // 4. Percorre o dia em encaixes do tamanho do serviço, pulando os que colidem.
    var livres = new List<TimeOnly>();
    var atual = funcionamento.Abertura;

    while (true)
    {
        var fimEncaixe = atual.AddMinutes(servico.TempoMin);

        // Para quando passa do fechamento (ou dá a volta na meia-noite).
        if (fimEncaixe <= atual || fimEncaixe > funcionamento.Fechamento)
        {
            break;
        }

        var inicio = data.ToDateTime(atual);
        var fim = data.ToDateTime(fimEncaixe);
        if (!Agenda.Colide(ocupados, inicio, fim))
        {
            livres.Add(atual);
        }

        atual = fimEncaixe;
    }

    return Results.Ok(livres);
});

app.Run();