public class HorarioFuncionamento
{
    public int Id { get; set; }
    public DayOfWeek DiaDaSemana { get; set; }
    public TimeOnly Abertura {get; set; }
    public TimeOnly Fechamento {get; set; }


}