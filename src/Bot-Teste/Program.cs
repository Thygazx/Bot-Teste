using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=agendamento.db"));
var app = builder.Build();

app.MapGet("/servicos", async (AppDbContext db) => await db.Servicos.ToListAsync());
app.MapPost("/servicos", async (Servico Servicos, AppDbContext db) =>
{
    var jaExisteServicos = await db.Servicos.AnyAsync(p => p.Nome == Servicos.Nome);
    {   
    if(jaExisteServicos)
    return Results.Conflict("Esse serviço já existe");
    }

    db.Servicos.Add(Servicos);
    await db.SaveChangesAsync();
    return Results.Created($"/servicos/{Servicos.Id}", Servicos);


});

app.MapGet("/horarios", async (AppDbContext db) => await db.HorarioFuncionamentos.ToListAsync());
app.MapPost("/horarios", async (HorarioFuncionamento horario, AppDbContext db) =>
{
    var jaExisteHorarios = await db.HorarioFuncionamentos.AnyAsync(h => h.DiaDaSemana == horario.DiaDaSemana);
    if(jaExisteHorarios)
    {
        return Results.Conflict("Esse horario já existe");
    }
    db.HorarioFuncionamentos.Add(horario);
    await db.SaveChangesAsync();
    return Results.Created($"/horarios/{horario.Id}", horario);
});

app.MapGet("/horarios-livres", async (DateOnly data, int servicoId, AppDbContext db) =>
{
    // 1. descobrir o dia da semana da data
    var dia = data.DayOfWeek;
    
    // 2. buscar a linha do horário desse dia
        var horario = await db.HorarioFuncionamentos.FirstOrDefaultAsync(hr => hr.DiaDaSemana == dia);

    // 3. se não achou, devolver "fechado"
    if(horario == null)
    {
        return Results.NotFound("To te falando");
    }

    // 4. se achou, devolver a linha
      return Results.Ok(horario.Abertura);
});


app.Run();
