namespace EcoColeta.Domain;
public sealed class ErroRegraDominio(string message) : Exception(message);
public enum TipoMaterial { Papel, Plastico, Vidro, Metal, Eletronicos }
public enum StatusColeta { Agendada, Concluida, Cancelada }
public sealed class PontoColeta
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = "";
    public string PostalCode { get; private set; } = "";
    public string Street { get; private set; } = "";
    public string City { get; private set; } = "";
    public string State { get; private set; } = "";
    private PontoColeta() { }
    public PontoColeta(string name, string postalCode, string street, string city, string state)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ErroRegraDominio("Informe um nome com até 100 caracteres.");
        Name = name.Trim(); PostalCode = postalCode; Street = street; City = city; State = state;
    }
}
public sealed class Usuario
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public string Role { get; private set; } = "Citizen";
    private Usuario() { }
    public Usuario(string email, string passwordHash, bool admin = false)
    { Email = email.Trim().ToLowerInvariant(); PasswordHash = passwordHash; Role = admin ? "Admin" : "Citizen"; }
}
public sealed class Coleta
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CollectionPointId { get; private set; }
    public Guid UserId { get; private set; }
    public TipoMaterial Material { get; private set; }
    public double WeightKg { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public StatusColeta Status { get; private set; } = StatusColeta.Agendada;
    private Coleta() { }
    public Coleta(Guid pointId, Guid userId, TipoMaterial material, double weightKg, DateTime scheduledAt, DateTime now)
    {
        if (!Enum.IsDefined(material)) throw new ErroRegraDominio("Material inválido.");
        if (!double.IsFinite(weightKg) || weightKg <= 0 || weightKg > 1000)
            throw new ErroRegraDominio("O peso deve ser maior que zero e de até 1000 kg.");
        if (scheduledAt.Kind != DateTimeKind.Utc || scheduledAt <= now || scheduledAt > now.AddDays(30))
            throw new ErroRegraDominio("Escolha uma data futura, em UTC, dentro dos próximos 30 dias.");
        CollectionPointId = pointId; UserId = userId; Material = material; WeightKg = weightKg; ScheduledAt = scheduledAt;
    }
    public void Concluir() { GarantirAgendada(); Status = StatusColeta.Concluida; }
    public void Cancelar() { GarantirAgendada(); Status = StatusColeta.Cancelada; }
    private void GarantirAgendada()
    { if (Status != StatusColeta.Agendada) throw new ErroRegraDominio("Esta coleta já foi concluída ou cancelada."); }
}
