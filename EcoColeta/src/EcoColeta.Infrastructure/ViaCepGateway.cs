using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EcoColeta.Application;
namespace EcoColeta.Infrastructure;
public sealed class ViaCepGateway(HttpClient client) : IAddressGateway
{
    public async Task<OperationResult<AddressDto>> FindAsync(string postalCode, CancellationToken ct)
    {
        if (postalCode.Length != 8 || !postalCode.All(char.IsAsciiDigit))
            return OperationResult<AddressDto>.Failure(new("address.invalid", "Informe um CEP com 8 dígitos.", ErrorKind.Validation));
        try
        {
            using var response = await client.GetAsync($"{postalCode}/json/", ct);
            if (response.StatusCode == HttpStatusCode.BadRequest)
                return OperationResult<AddressDto>.Failure(new("address.invalid", "CEP inválido.", ErrorKind.Validation));
            if (!response.IsSuccessStatusCode) return Unavailable();
            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
            if (json is null) return Unavailable();
            var root = json.RootElement;
            if (root.TryGetProperty("erro", out var error) && (error.ValueKind == JsonValueKind.True || error.ToString() == "true"))
                return OperationResult<AddressDto>.Failure(new("address.not_found", "CEP não encontrado no ViaCEP.", ErrorKind.NotFound));
            if (!root.TryGetProperty("localidade", out var city) || !root.TryGetProperty("uf", out var state) ||
                string.IsNullOrWhiteSpace(city.GetString()) || string.IsNullOrWhiteSpace(state.GetString())) return Unavailable();
            var street = root.TryGetProperty("logradouro", out var s) ? s.GetString() ?? "" : "";
            return OperationResult<AddressDto>.Success(new(postalCode, street, city.GetString()!, state.GetString()!));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Unavailable(); }
        catch (HttpRequestException) { return Unavailable(); }
        catch (JsonException) { return Unavailable(); }
        catch (InvalidOperationException) { return Unavailable(); }
        catch (Polly.Timeout.TimeoutRejectedException) { return Unavailable(); }
        catch (Polly.CircuitBreaker.BrokenCircuitException) { return Unavailable(); }
    }
    private static OperationResult<AddressDto> Unavailable() => OperationResult<AddressDto>.Failure(
        new("external.unavailable", "Não foi possível consultar o ViaCEP. Tente novamente mais tarde.", ErrorKind.Unavailable));
}
