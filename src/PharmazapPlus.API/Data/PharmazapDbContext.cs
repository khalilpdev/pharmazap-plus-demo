using Microsoft.EntityFrameworkCore;
using PharmazapPlus.Shared.Enums;
using PharmazapPlus.Shared.Models;
using PharmazapPlus.Shared.Models.Agendamentos;
using PharmazapPlus.Shared.Models.Campanhas;
using PharmazapPlus.Shared.Models.CRM;
using PharmazapPlus.Shared.Models.Core;
using PharmazapPlus.Shared.Models.Mensagens;
using PharmazapPlus.Shared.Models.Vendas;

namespace PharmazapPlus.API.Data;

public class PharmazapDbContext : DbContext
{
    public PharmazapDbContext(DbContextOptions<PharmazapDbContext> options)
        : base(options)
    {
    }

    // Tabelas
    public DbSet<Farmacia> Farmacias { get; set; }
    public DbSet<FarmaciaIntegracoes> FarmaciaIntegracoes { get; set; }
    public DbSet<Canal> Canais { get; set; }
    public DbSet<UsuarioFarmacia> UsuariosFarmacia { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Atendimento> Atendimentos { get; set; }
    public DbSet<Mensagem> Mensagens { get; set; }
    public DbSet<AgendamentoRecorrente> AgendamentosRecorrentes { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<PedidoItem> PedidoItens { get; set; }
    public DbSet<ClienteTag> ClienteTags { get; set; }
    public DbSet<ClienteObservacao> ClienteObservacoes { get; set; }
    public DbSet<Departamento> Departamentos { get; set; }
    public DbSet<Etiqueta> Etiquetas { get; set; }
    public DbSet<MotivoEncerramento> MotivosEncerramento { get; set; }
    public DbSet<AtividadeRecente> AtividadesRecentes { get; set; }
    public DbSet<Campanha> Campanhas { get; set; }
    public DbSet<MensagemPreDefinida> MensagensPreDefinidas { get; set; }
    public DbSet<RemetenteBloqueado> RemetentesBlockeados { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurações das entidades

        // Farmacia
        modelBuilder.Entity<Farmacia>(entity =>
        {
            entity.HasIndex(e => e.Cnpj).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.DataCadastro).HasDefaultValueSql("NOW()");
        });

        // FarmaciaIntegracoes (1:1 com Farmacia)
        modelBuilder.Entity<FarmaciaIntegracoes>(entity =>
        {
            entity.HasIndex(e => e.FarmaciaId).IsUnique();
            entity.HasIndex(e => e.ChaveWhatsApp).IsUnique();

            entity.HasOne(e => e.Farmacia)
                  .WithOne(f => f.Integracoes)
                  .HasForeignKey<FarmaciaIntegracoes>(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // UsuarioFarmacia
        modelBuilder.Entity<UsuarioFarmacia>(entity =>
        {
            entity.HasIndex(e => new { e.FarmaciaId, e.Email }).IsUnique();
            entity.Property(e => e.DataCadastro).HasDefaultValueSql("NOW()");
            entity.Property(e => e.Theme).HasDefaultValue("Light");

            entity.HasOne(e => e.Farmacia)
                  .WithMany(f => f.Usuarios)
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Departamento)
                  .WithMany()
                  .HasForeignKey(e => e.DepartamentoId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Cliente
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasIndex(e => new { e.FarmaciaId, e.WhatsAppId });
            entity.HasIndex(e => new { e.FarmaciaId, e.Cpf }).IsUnique();
            entity.HasIndex(e => new { e.FarmaciaId, e.Email });
            entity.Property(e => e.DataCadastro).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Farmacia)
                  .WithMany(f => f.Clientes)
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Atendimento
        modelBuilder.Entity<Atendimento>(entity =>
        {
            entity.HasIndex(e => new { e.FarmaciaId, e.WhatsAppId });
            entity.HasIndex(e => e.StatusEnum);
            entity.Property(e => e.DataInicio).HasDefaultValueSql("NOW()");

            // Mapeia o enum para a coluna "Status" já existente no banco, armazenando como string
            entity.Property(e => e.StatusEnum)
                  .HasColumnName("Status")
                  .HasConversion<string>()
                  .IsRequired();

            // Propriedade computada — ignorar no mapeamento
            entity.Ignore(e => e.Status);

            entity.HasOne(e => e.Farmacia)
                  .WithMany(f => f.Atendimentos)
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cliente)
                  .WithMany(c => c.Atendimentos)
                  .HasForeignKey(e => e.ClienteId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Atendente)
                  .WithMany(u => u.Atendimentos)
                  .HasForeignKey(e => e.AtendenteId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Mensagem
        modelBuilder.Entity<Mensagem>(entity =>
        {
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.AtendimentoId);
            entity.HasIndex(e => e.ReceivedAt);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ReceivedAt).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Atendimento)
                  .WithMany(a => a.Mensagens)
                  .HasForeignKey(e => e.AtendimentoId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AgendamentoRecorrente
        modelBuilder.Entity<AgendamentoRecorrente>(entity =>
        {
            entity.HasIndex(e => e.ProximoDisparo);
            entity.HasIndex(e => new { e.ClienteId, e.Ativo });
            entity.Property(e => e.DataCriacao).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Cliente)
                  .WithMany(c => c.Agendamentos)
                  .HasForeignKey(e => e.ClienteId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Farmacia)
                  .WithMany(f => f.Agendamentos)
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Atendimento)
                  .WithMany(a => a.Agendamentos)
                  .HasForeignKey(e => e.AtendimentoId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Pedido
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.HasIndex(e => e.NumeroPedido).IsUnique();
            entity.HasIndex(e => new { e.FarmaciaId, e.ClienteId });
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.DataPedido).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Cliente)
                  .WithMany(c => c.Pedidos)
                  .HasForeignKey(e => e.ClienteId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Farmacia)
                  .WithMany(f => f.Pedidos)
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // PedidoItem
        modelBuilder.Entity<PedidoItem>(entity =>
        {
            entity.HasOne(e => e.Pedido)
                  .WithMany(p => p.Itens)
                  .HasForeignKey(e => e.PedidoId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ClienteTag
        modelBuilder.Entity<ClienteTag>(entity =>
        {
            entity.HasIndex(e => new { e.ClienteId, e.Tag }).IsUnique();

            entity.HasOne(e => e.Cliente)
                  .WithMany(c => c.Tags)
                  .HasForeignKey(e => e.ClienteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ClienteObservacao
        modelBuilder.Entity<ClienteObservacao>(entity =>
        {
            entity.HasIndex(e => e.ClienteId);
            entity.HasIndex(e => e.Concluida);
            entity.Property(e => e.DataCriacao).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Cliente)
                  .WithMany(c => c.Observacoes)
                  .HasForeignKey(e => e.ClienteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Departamento
        modelBuilder.Entity<Departamento>(entity =>
        {
            entity.HasIndex(e => new { e.FarmaciaId, e.Nome }).IsUnique();
            entity.HasIndex(e => new { e.FarmaciaId, e.Ativo });

            entity.HasOne(e => e.Farmacia)
                  .WithMany(f => f.Departamentos)
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Etiqueta
        modelBuilder.Entity<Etiqueta>(entity =>
        {
            entity.HasIndex(e => new { e.FarmaciaId, e.Nome }).IsUnique();
            entity.HasIndex(e => new { e.FarmaciaId, e.Ativo });

            entity.HasOne(e => e.Farmacia)
                  .WithMany(f => f.Etiquetas)
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Cliente -> Etiqueta
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasOne(e => e.Etiqueta)
                  .WithMany(et => et.Clientes)
                  .HasForeignKey(e => e.EtiquetaId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Atendimento -> Etiqueta
        modelBuilder.Entity<Atendimento>(entity =>
        {
            entity.HasOne(e => e.Etiqueta)
                  .WithMany(et => et.Atendimentos)
                  .HasForeignKey(e => e.EtiquetaId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.MotivoEncerramento)
                  .WithMany()
                  .HasForeignKey(e => e.MotivoEncerramentoId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // MotivoEncerramento
        modelBuilder.Entity<MotivoEncerramento>(entity =>
        {
            entity.HasIndex(e => new { e.FarmaciaId, e.Nome }).IsUnique();
            entity.HasIndex(e => new { e.FarmaciaId, e.Ativo });

            entity.HasOne(e => e.Farmacia)
                  .WithMany(f => f.MotivosEncerramento)
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AtividadeRecente
        modelBuilder.Entity<AtividadeRecente>(entity =>
        {
            entity.HasIndex(e => new { e.FarmaciaId, e.DataHora });
            entity.HasIndex(e => new { e.UsuarioId, e.DataHora });
            entity.Property(e => e.DataHora).HasDefaultValueSql("NOW()");
        });

        // Campanha
        modelBuilder.Entity<Campanha>(entity =>
        {
            entity.HasIndex(e => new { e.FarmaciaId, e.Status });
            entity.HasIndex(e => e.DataAgendamento);
            entity.Property(e => e.DataCriacao).HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Farmacia)
                  .WithMany(f => f.Campanhas)
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // MensagemPreDefinida
        modelBuilder.Entity<MensagemPreDefinida>(entity =>
        {
            entity.HasIndex(e => new { e.FarmaciaId, e.Titulo }).IsUnique();
            entity.HasIndex(e => new { e.FarmaciaId, e.Ativo });

            entity.HasOne(e => e.Farmacia)
                  .WithMany(f => f.MensagensPreDefinidas)
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // RemetenteBloqueado
        modelBuilder.Entity<RemetenteBloqueado>(entity =>
        {
            // Índice único para (NumeroNormalizado, FarmaciaId) para permitir bloqueios local e global simultâneos
            entity.HasIndex(e => new { e.NumeroNormalizado, e.FarmaciaId }).IsUnique();
            entity.HasIndex(e => new { e.Ativo, e.DataBloqueio });
            entity.HasIndex(e => e.FarmaciaId);
            entity.HasIndex(e => e.Escopo);

            // FK para Farmacia (nullable para bloqueios globais)
            entity.HasOne(e => e.Farmacia)
                  .WithMany()
                  .HasForeignKey(e => e.FarmaciaId)
                  .OnDelete(DeleteBehavior.Cascade);

            // FK para quem bloqueou
            entity.HasOne(e => e.BloqueadoPor)
                  .WithMany()
                  .HasForeignKey(e => e.BloqueadoPorId)
                  .OnDelete(DeleteBehavior.Restrict);

            // FK para quem desbloqueou (nullable)
            entity.HasOne(e => e.DesbloqueadoPor)
                  .WithMany()
                  .HasForeignKey(e => e.DesbloqueadoPorId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Enum como string
            entity.Property(e => e.Escopo)
                  .HasConversion<string>();
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Atualizar timestamps automáticos
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entityEntry in entries)
        {
            if (entityEntry.State == EntityState.Added)
            {
                ((BaseEntity)entityEntry.Entity).CreatedAt = DateTime.UtcNow;
            }

            ((BaseEntity)entityEntry.Entity).UpdatedAt = DateTime.UtcNow;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

}
