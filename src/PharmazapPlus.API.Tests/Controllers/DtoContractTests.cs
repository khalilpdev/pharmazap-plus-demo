using System.Text.Json;
using FluentAssertions;
using PharmazapPlus.API.Controllers;
using PharmazapPlus.API.Services.CRM;
using PharmazapPlus.API.Services.WhatsApp;
using PharmazapPlus.Shared.Models.Mensagens;

namespace PharmazapPlus.API.Tests.Controllers;

/// <summary>
/// Testes de contrato dos DTOs migrados para record.
/// Garante que a serialização/desserialização JSON é preservada após a migração.
/// </summary>
public class DtoContractTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // ── LoginRequest ─────────────────────────────────────────────────

    [Fact]
    public void LoginRequest_Serializes_CamelCase()
    {
        var dto = new LoginRequest { Email = "user@test.com", Password = "pass123" };
        var json = JsonSerializer.Serialize(dto, _options);

        json.Should().Contain("\"email\"");
        json.Should().Contain("\"password\"");
        json.Should().Contain("user@test.com");
    }

    [Fact]
    public void LoginRequest_Deserializes_FromCamelCase()
    {
        var json = """{"email":"user@test.com","password":"pass123"}""";
        var dto = JsonSerializer.Deserialize<LoginRequest>(json, _options);

        dto.Should().NotBeNull();
        dto!.Email.Should().Be("user@test.com");
        dto.Password.Should().Be("pass123");
    }

    [Fact]
    public void LoginRequest_IsRecord_SupportsValueEquality()
    {
        var a = new LoginRequest { Email = "a@b.com", Password = "pw" };
        var b = new LoginRequest { Email = "a@b.com", Password = "pw" };

        a.Should().Be(b);
    }

    // ── RegisterFarmaciaRequest ───────────────────────────────────────

    [Fact]
    public void RegisterFarmaciaRequest_Serializes_AllFields()
    {
        var dto = new RegisterFarmaciaRequest
        {
            Nome = "Farmacia ABC",
            Cnpj = "00.000.000/0001-00",
            Telefone = "(11)99999-9999",
            Email = "contato@abc.com",
            Endereco = "Rua A, 1",
            LogoUrl = "/uploads/logos/logo.png",
            NomeContato = "João",
            EmailContato = "joao@abc.com",
            TelefoneContato = "(11)99999-9998",
            Plano = "Basic"
        };
        var json = JsonSerializer.Serialize(dto, _options);
        var restored = JsonSerializer.Deserialize<RegisterFarmaciaRequest>(json, _options);

        restored.Should().NotBeNull();
        restored!.Nome.Should().Be("Farmacia ABC");
        restored.Plano.Should().Be("Basic");
        restored.LogoUrl.Should().Be("/uploads/logos/logo.png");
    }

    // ── ResetPasswordRequest ──────────────────────────────────────────

    [Fact]
    public void ResetPasswordRequest_DefaultValue_IsEmpty()
    {
        var dto = new ResetPasswordRequest();
        dto.NewPassword.Should().Be("");
    }

    [Fact]
    public void ResetPasswordRequest_RoundTrip_PreservesValue()
    {
        var dto = new ResetPasswordRequest { NewPassword = "Str0ng@Pass" };
        var json = JsonSerializer.Serialize(dto, _options);
        var restored = JsonSerializer.Deserialize<ResetPasswordRequest>(json, _options);

        restored!.NewPassword.Should().Be("Str0ng@Pass");
    }

    // ── UpdateThemeRequest ────────────────────────────────────────────

    [Fact]
    public void UpdateThemeRequest_RoundTrip_PreservesTheme()
    {
        var dto = new UpdateThemeRequest { Theme = "Dark" };
        var json = JsonSerializer.Serialize(dto, _options);
        var restored = JsonSerializer.Deserialize<UpdateThemeRequest>(json, _options);

        restored!.Theme.Should().Be("Dark");
    }

    // ── ClienteUpdateDto ─────────────────────────────────────────────

    [Fact]
    public void ClienteUpdateDto_IsRecord_SupportsValueEquality()
    {
        var a = new ClienteUpdateDto { Nome = "Maria", Email = "m@test.com" };
        var b = new ClienteUpdateDto { Nome = "Maria", Email = "m@test.com" };

        a.Should().Be(b);
    }

    [Fact]
    public void ClienteUpdateDto_Deserializes_NullableFields()
    {
        var json = """{"nome":"João","email":null,"usoContinuo":true}""";
        var dto = JsonSerializer.Deserialize<ClienteUpdateDto>(json, _options);

        dto.Should().NotBeNull();
        dto!.Nome.Should().Be("João");
        dto.Email.Should().BeNull();
        dto.UsoContinuo.Should().BeTrue();
    }

    // ── ClienteObservacaoDto ──────────────────────────────────────────

    [Fact]
    public void ClienteObservacaoDto_RoundTrip_PreservesAllFields()
    {
        var dto = new ClienteObservacaoDto
        {
            Titulo = "Alergia",
            Descricao = "Alergia a penicilina",
            Tipo = "Saude",
            Prioridade = "Alta",
            Responsavel = "Dr. Silva"
        };
        var json = JsonSerializer.Serialize(dto, _options);
        var restored = JsonSerializer.Deserialize<ClienteObservacaoDto>(json, _options);

        restored!.Titulo.Should().Be("Alergia");
        restored.Prioridade.Should().Be("Alta");
    }

    // ── ClienteObservacaoUpdateDto ────────────────────────────────────

    [Fact]
    public void ClienteObservacaoUpdateDto_NullableFields_SerializeCorrectly()
    {
        var dto = new ClienteObservacaoUpdateDto { Descricao = "Atualizado", Concluida = true };
        var json = JsonSerializer.Serialize(dto, _options);

        json.Should().Contain("\"descricao\"");
        json.Should().Contain("\"concluida\"");
    }

    // ── WhatsAppMessageResponse ───────────────────────────────────────

    [Fact]
    public void WhatsAppMessageResponse_DefaultMetadata_IsEmpty()
    {
        var dto = new WhatsAppMessageResponse();
        dto.Metadata.Should().NotBeNull();
        dto.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void WhatsAppMessageResponse_RoundTrip_PreservesMetadata()
    {
        var dto = new WhatsAppMessageResponse
        {
            Success = true,
            MessageId = "msg-123",
            Metadata = new Dictionary<string, object> { { "key", "value" } }
        };
        var json = JsonSerializer.Serialize(dto, _options);
        json.Should().Contain("\"success\"");
        json.Should().Contain("msg-123");
    }

    // ── AtendimentoDto ────────────────────────────────────────────────

    [Fact]
    public void AtendimentoDto_RoundTrip_PreservesFields()
    {
        var now = DateTime.UtcNow;
        var dto = new AtendimentoDto
        {
            Id = 42,
            Status = "EmAtendimento",
            WhatsAppId = "5511999999999",
            Canal = "WhatsApp",
            DataInicio = now,
            ClienteNome = "Maria",
            TotalMensagens = 5
        };
        var json = JsonSerializer.Serialize(dto, _options);
        var restored = JsonSerializer.Deserialize<AtendimentoDto>(json, _options);

        restored!.Id.Should().Be(42);
        restored.ClienteNome.Should().Be("Maria");
        restored.TotalMensagens.Should().Be(5);
    }

    [Fact]
    public void AtendimentoDto_ToAtendimento_MapsFieldsCorrectly()
    {
        var now = DateTime.UtcNow;
        var dto = new AtendimentoDto
        {
            Id = 10,
            Status = "Aberto",
            WhatsAppId = "5511111111111",
            Canal = "WhatsApp",
            DataInicio = now,
            ClienteNome = "Pedro",
            AtendenteNome = "Ana",
            UltimaMensagem = "Olá",
            UltimaMensagemEm = now
        };

        var atendimento = dto.ToAtendimento();

        atendimento.Id.Should().Be(10);
        atendimento.Cliente.Should().NotBeNull();
        atendimento.Cliente!.Nome.Should().Be("Pedro");
        atendimento.Atendente.Should().NotBeNull();
        atendimento.Atendente!.Nome.Should().Be("Ana");
        atendimento.Mensagens.Should().HaveCount(1);
    }

    [Fact]
    public void AtendimentoDto_ToAtendimento_FormatsPhoneWhenClienteNomeIsNull()
    {
        var dto = new AtendimentoDto
        {
            Id = 5,
            WhatsAppId = "5562991236502",
            DataInicio = DateTime.UtcNow
        };

        var atendimento = dto.ToAtendimento();

        atendimento.Cliente.Should().NotBeNull();
        atendimento.Cliente!.Nome.Should().Be("(62) 99123-6502");
    }

    // ── SendTextRequest ───────────────────────────────────────────────

    [Fact]
    public void SendTextRequest_IsRecord_SupportsValueEquality()
    {
        var a = new SendTextRequest { To = "+5511999999999", Text = "Olá" };
        var b = new SendTextRequest { To = "+5511999999999", Text = "Olá" };

        a.Should().Be(b);
    }

    // ── SimularMensagemRequest ────────────────────────────────────────

    [Fact]
    public void SimularMensagemRequest_RoundTrip_PreservesFields()
    {
        var dto = new SimularMensagemRequest { FarmaciaId = 1, From = "5511999999999", Text = "Teste", IsGroup = true };
        var json = JsonSerializer.Serialize(dto, _options);
        var restored = JsonSerializer.Deserialize<SimularMensagemRequest>(json, _options);

        restored!.FarmaciaId.Should().Be(1);
        restored.From.Should().Be("5511999999999");
        restored.Text.Should().Be("Teste");
        restored.IsGroup.Should().BeTrue();
    }

    // ── CreateUsuarioRequest ──────────────────────────────────────────

    [Fact]
    public void CreateUsuarioRequest_DefaultRole_IsAtendente()
    {
        var dto = new CreateUsuarioRequest();
        dto.Role.Should().Be("Atendente");
        dto.Nome.Should().Be("");
        dto.Email.Should().Be("");
        dto.Password.Should().Be("");
    }

    [Fact]
    public void CreateUsuarioRequest_RoundTrip_PreservesNullableFields()
    {
        var dto = new CreateUsuarioRequest
        {
            Nome = "Novo Atendente",
            Email = "atendente@farm.com",
            Password = "Pass@123",
            FarmaciaId = 5,
            Turno = "Manhã"
        };
        var json = JsonSerializer.Serialize(dto, _options);
        var restored = JsonSerializer.Deserialize<CreateUsuarioRequest>(json, _options);

        restored!.FarmaciaId.Should().Be(5);
        restored.Turno.Should().Be("Manhã");
    }

    // ── SendTemplateRequest ───────────────────────────────────────────

    [Fact]
    public void SendTemplateRequest_DefaultLanguageCode_IsPtBr()
    {
        var dto = new SendTemplateRequest { To = "+5511999999999", TemplateName = "welcome" };
        dto.LanguageCode.Should().Be("pt_BR", "o idioma padrão deve ser pt_BR");
    }

    [Fact]
    public void SendTemplateRequest_Parameters_IsNullable()
    {
        // Parâmetros de template são opcionais — a propriedade deve aceitar null
        var dto = new SendTemplateRequest { To = "+5511999999999", TemplateName = "welcome" };
        dto.Parameters.Should().BeNull("Parameters é nullable e não deve ter valor padrão");
    }

    [Fact]
    public void SendTemplateRequest_WithParameters_SerializesCorrectly()
    {
        var dto = new SendTemplateRequest
        {
            To = "+5511999999999",
            TemplateName = "lembrete_consulta",
            LanguageCode = "pt_BR",
            Parameters = new Dictionary<string, object>
            {
                { "nome", "João" },
                { "horario", "14:00" }
            }
        };
        var json = JsonSerializer.Serialize(dto, _options);
        var restored = JsonSerializer.Deserialize<SendTemplateRequest>(json, _options);

        json.Should().Contain("\"templateName\"");
        json.Should().Contain("lembrete_consulta");
        json.Should().Contain("\"parameters\"");
        json.Should().Contain("\"nome\"");
        restored.Should().NotBeNull();
        restored!.Parameters.Should().ContainKey("nome");
    }

    [Fact]
    public void SendTemplateRequest_WithNullParameters_OmitsParametersFieldWhenWritingNull()
    {
        // Com DefaultIgnoreCondition = WhenWritingNull, o campo não deve aparecer no JSON
        var optionsWithIgnoreNull = new JsonSerializerOptions(_options)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var dto = new SendTemplateRequest { To = "+5511999999999", TemplateName = "welcome" };
        var json = JsonSerializer.Serialize(dto, optionsWithIgnoreNull);

        json.Should().NotContain("\"parameters\"",
            "campo nullable omitido com WhenWritingNull não deve aparecer no JSON");
    }

    [Fact]
    public void SendTemplateRequest_Deserializes_WithoutParameters()
    {
        var json = """{"to":"+5511999999999","templateName":"welcome","languageCode":"pt_BR"}""";
        var dto  = JsonSerializer.Deserialize<SendTemplateRequest>(json, _options);

        dto.Should().NotBeNull();
        dto!.TemplateName.Should().Be("welcome");
        dto.Parameters.Should().BeNull("ausência do campo deve resultar em null");
    }
}
