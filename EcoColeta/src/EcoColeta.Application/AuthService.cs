using EcoColeta.Domain;
namespace EcoColeta.Application;
public sealed class AuthService(IUsuarioRepository repository, IPasswordService passwords, ITokenService tokens)
{
    public Task<OperationResult<UsuarioDto>> AdicionarAsync(SolicitarCadastro request, CancellationToken ct) => ServiceExecution.Run(async () =>
    {
        if (await repository.FindAsync(request.Email, ct) is not null)
            return OperationResult<UsuarioDto>.Failure(new("user.duplicate", "E-mail já cadastrado.", ErrorKind.Conflict));
        var user = new Usuario(request.Email, passwords.Hash(request.Password));
        await repository.AddAsync(user, ct);
        return OperationResult<UsuarioDto>.Success(new(user.Id, user.Email, user.Role));
    });
    public Task<OperationResult<TokenDto>> AutenticarAsync(SolicitarLogin request, CancellationToken ct) => ServiceExecution.Run(async () =>
    {
        var user = await repository.FindAsync(request.Email, ct);
        var valid = passwords.Verify(user?.PasswordHash ?? DummyHash, request.Password);
        return user is not null && valid ? OperationResult<TokenDto>.Success(tokens.Issue(user)) :
            OperationResult<TokenDto>.Failure(new("auth.invalid_credentials", "E-mail ou senha incorretos.", ErrorKind.Unauthorized));
    });
    private readonly string DummyHash = passwords.Hash("unrelated-dummy-password-for-timing");
}
