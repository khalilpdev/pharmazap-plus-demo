using System;
using System.Collections.Generic;
using System.Text;

namespace PharmazapPlus.Shared.Models.CRM;

public class ClienteObservacao
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public required Cliente Cliente { get; set; }
    public required string Titulo { get; set; }
    public required string Descricao { get; set; }
    public required string Tipo { get; set; } // Geral, Saúde, Financeiro, Atendimento
    public required string Prioridade { get; set; } // Baixa, Media, Alta
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataConclusao { get; set; }
    public bool Concluida { get; set; }
    public required string CriadoPor { get; set; }
    public required string Responsavel { get; set; }
}