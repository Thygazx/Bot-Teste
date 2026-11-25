public class Agendamento
{
    public int Id { get; set; }
    public int IdCliente { get; set; }
    public int IdServico { get; set; }
    public DateTime Inicio { get; set; }
    public enum StatusAgendamento
    {
        Confirmado,
        Cancelado,
        Reagendado,
    }
    public StatusAgendamento Status { get; set; }
}