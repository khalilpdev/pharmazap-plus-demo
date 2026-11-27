#nullable disable
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using PharmazapPlus.API.Filters;

namespace PharmazapPlus.API.Tests.Filters;

/// <summary>
/// Unit tests for <see cref="BearerAuthOperationFilter"/>.
/// Verifica que o filtro adiciona o requisito Bearer apenas em endpoints protegidos
/// e respeita <see cref="AllowAnonymousAttribute"/> no método e na classe.
/// </summary>
public class BearerAuthOperationFilterTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static BearerAuthOperationFilter CreateFilter() => new();

    /// <summary>
    /// Cria um <see cref="OperationFilterContext"/> para o método de destino informado.
    /// </summary>
    private static OperationFilterContext BuildContext(System.Reflection.MethodInfo method)
    {
        var apiDescription = new ApiDescription();
        var schemaRepo      = new SchemaRepository();

        // SchemaGenerator não é usado pelo nosso filtro, então um mock simples serve.
        var schemaGenerator = new Moq.Mock<ISchemaGenerator>().Object;
        var document        = new OpenApiDocument();

        return new OperationFilterContext(apiDescription, schemaGenerator, schemaRepo, document, method);
    }

    // ── Dummy controllers usados como alvos de reflexão ───────────────────────

    private class ProtectedController
    {
        public void ProtectedAction() { }
    }

    [AllowAnonymous]
    private class AnonController
    {
        public void AnonAction() { }
    }

    private class MixedController
    {
        public void ProtectedAction() { }

        [AllowAnonymous]
        public void AnonAction() { }
    }

    // ── Testes ────────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_ProtectedEndpoint_AddsBearerSecurityRequirement()
    {
        // Arrange
        var filter    = CreateFilter();
        var method    = typeof(ProtectedController).GetMethod(nameof(ProtectedController.ProtectedAction))!;
        var context   = BuildContext(method);
        var operation = new OpenApiOperation();

        // Act
        filter.Apply(operation, context);

        // Assert
        operation.Security.Should().NotBeNullOrEmpty("endpoint protegido deve ter requisito de segurança");
        operation.Security.Should().HaveCount(1);
        var requirement = operation.Security[0];
        var schemeRef   = requirement.Keys.Single();
        schemeRef.Reference!.Id.Should().Be(JwtBearerDefaults.AuthenticationScheme,
            "o esquema deve referenciar 'Bearer'");
    }

    [Fact]
    public void Apply_MethodWithAllowAnonymous_DoesNotAddSecurityRequirement()
    {
        // Arrange
        var filter    = CreateFilter();
        var method    = typeof(MixedController).GetMethod(nameof(MixedController.AnonAction))!;
        var context   = BuildContext(method);
        var operation = new OpenApiOperation();

        // Act
        filter.Apply(operation, context);

        // Assert
        operation.Security.Should().BeNullOrEmpty(
            "endpoints [AllowAnonymous] não devem exigir autenticação");
    }

    [Fact]
    public void Apply_ClassWithAllowAnonymous_DoesNotAddSecurityRequirement()
    {
        // Arrange – o método pertence a uma classe decorada com [AllowAnonymous]
        var filter    = CreateFilter();
        var method    = typeof(AnonController).GetMethod(nameof(AnonController.AnonAction))!;
        var context   = BuildContext(method);
        var operation = new OpenApiOperation();

        // Act
        filter.Apply(operation, context);

        // Assert
        operation.Security.Should().BeNullOrEmpty(
            "classe decorada com [AllowAnonymous] não deve exigir autenticação em nenhum método");
    }

    [Fact]
    public void Apply_ProtectedMethod_SecurityRequirementReferencesBearerScheme()
    {
        // Arrange
        var filter    = CreateFilter();
        var method    = typeof(ProtectedController).GetMethod(nameof(ProtectedController.ProtectedAction))!;
        var context   = BuildContext(method);
        var operation = new OpenApiOperation();

        // Act
        filter.Apply(operation, context);

        // Assert – chave do dicionário é um OpenApiSecuritySchemeReference cujo Id == "Bearer"
        var schemeRef = operation.Security[0].Keys.Single();
        schemeRef.Reference!.Id.Should().Be(JwtBearerDefaults.AuthenticationScheme,
            "o Id do esquema de segurança deve ser 'Bearer'");
        schemeRef.Reference.Should().NotBeNull();
    }

    [Fact]
    public void Apply_ProtectedMethod_SecurityRequirementScopesAreEmpty()
    {
        // Arrange
        var filter    = CreateFilter();
        var method    = typeof(ProtectedController).GetMethod(nameof(ProtectedController.ProtectedAction))!;
        var context   = BuildContext(method);
        var operation = new OpenApiOperation();

        // Act
        filter.Apply(operation, context);

        // Assert – JWT Bearer não usa scopes; a lista deve estar vazia
        var scopes = operation.Security[0].Values.Single();
        scopes.Should().BeEmpty("JWT Bearer não requer escopos OAuth");
    }

    [Fact]
    public void Apply_OperationAlreadyHasSecurity_AppendsRequirementWithoutOverwriting()
    {
        // Arrange
        var filter    = CreateFilter();
        var method    = typeof(ProtectedController).GetMethod(nameof(ProtectedController.ProtectedAction))!;
        var context   = BuildContext(method);
        var operation = new OpenApiOperation
        {
            Security =
            [
                new OpenApiSecurityRequirement
                {
                    { new OpenApiSecuritySchemeReference("ApiKey"), new List<string>() }
                }
            ]
        };

        // Act
        filter.Apply(operation, context);

        // Assert – o requisito Bearer é adicionado sem remover o existente
        operation.Security.Should().HaveCount(2, "deve haver dois requisitos de segurança");
        var bearerRequirement = operation.Security
            .FirstOrDefault(r => r.Keys.Any(k => k.Reference?.Id == JwtBearerDefaults.AuthenticationScheme));
        bearerRequirement.Should().NotBeNull("deve conter o requisito Bearer");
    }

    [Fact]
    public void Apply_ProtectedMethodInMixedController_AddsBearerRequirement()
    {
        // Arrange – método sem [AllowAnonymous] em classe sem o atributo
        var filter    = CreateFilter();
        var method    = typeof(MixedController).GetMethod(nameof(MixedController.ProtectedAction))!;
        var context   = BuildContext(method);
        var operation = new OpenApiOperation();

        // Act
        filter.Apply(operation, context);

        // Assert
        operation.Security.Should().NotBeNullOrEmpty(
            "método sem [AllowAnonymous] deve ter requisito de segurança");
    }
}
#nullable restore
