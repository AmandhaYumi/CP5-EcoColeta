using EcoColeta.Domain;
using EcoColeta.Application;
using EcoColeta.Infrastructure;
namespace EcoColeta.UnitTests;
public sealed class DomainTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(1001)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void Rejects_invalid_weight(double weight) => Assert.Throws<ErroRegraDominio>(() => New(weight));
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(31)]
    public void Rejects_invalid_schedule(int days) => Assert.Throws<ErroRegraDominio>(() => new Coleta(Guid.NewGuid(), Guid.NewGuid(), TipoMaterial.Vidro, 1, Now.AddDays(days), Now));
    [Fact]
    public void Requires_utc() => Assert.Throws<ErroRegraDominio>(() => new Coleta(Guid.NewGuid(), Guid.NewGuid(), TipoMaterial.Vidro, 1, DateTime.SpecifyKind(Now.AddDays(1), DateTimeKind.Unspecified), Now));
    [Fact]
    public void Rejects_unknown_material() => Assert.Throws<ErroRegraDominio>(() => new Coleta(Guid.NewGuid(), Guid.NewGuid(), (TipoMaterial)99, 1, Now.AddDays(1), Now));
    [Fact]
    public void Completed_pickup_cannot_be_cancelled()
    { var p = New(); p.Concluir(); Assert.Equal(StatusColeta.Concluida, p.Status); Assert.Throws<ErroRegraDominio>(p.Cancelar); }
    [Fact]
    public void Cancelled_pickup_cannot_be_completed()
    { var p = New(); p.Cancelar(); Assert.Equal(StatusColeta.Cancelada, p.Status); Assert.Throws<ErroRegraDominio>(p.Concluir); }
    [Fact]
    public void Pagination_rounds_up_and_reports_item_count()
    { var p = new PageResult<int>([11], 2, 10, 11); Assert.Equal(2, p.TotalPages); Assert.Equal(1, p.ItemCount); Assert.True(p.HasPrevious); Assert.False(p.HasNext); }
    [Fact]
    public void Empty_page_has_no_next_page()
    { var p = new PageResult<int>([], 1, 10, 0); Assert.Equal(0, p.TotalPages); Assert.False(p.HasNext); Assert.False(p.HasPrevious); }
    [Fact]
    public void Passwords_are_salted_and_verified()
    { var service = new PasswordService(); var a = service.Hash("Example!Password2026"); Assert.NotEqual(a, service.Hash("Example!Password2026")); Assert.True(service.Verify(a, "Example!Password2026")); Assert.False(service.Verify(a, "wrong")); }
    [Fact]
    public void Failure_result_has_no_data()
    { var r = OperationResult<string>.Failure(new("test", "falha", ErrorKind.Conflict)); Assert.False(r.IsSuccess); Assert.Null(r.Data); Assert.NotNull(r.Error); }
    private static Coleta New(double weight = 1) => new(Guid.NewGuid(), Guid.NewGuid(), TipoMaterial.Vidro, weight, Now.AddDays(1), Now);
}
