using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CalculadoraFiscalUI.Models
{
    public class SubMetaAhorro
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal MontoObjetivo { get; set; }
        public decimal MontoActual { get; set; }
        public string Icono { get; set; } = "🎯";

        public double Porcentaje => MontoObjetivo > 0
            ? Math.Min((double)MontoActual / (double)MontoObjetivo * 100, 100)
            : 0;

        public bool Completada => MontoObjetivo > 0 && MontoActual >= MontoObjetivo;
    }

    public class MetaAhorro
    {
        public string Nombre { get; set; } = "Metas Combinadas (Casa + Moto)";
        public decimal MontoObjetivo { get; set; }
        public decimal MontoActual { get; set; }
        public decimal AportePorQuincena { get; set; } = 325m; // Promedio o compatibilidad
        public decimal AporteQ1 { get; set; } = 300m;
        public decimal AporteQ2 { get; set; } = 300m;
        public decimal MontoDecimo { get; set; } = 588m;
        public DateTime FechaLimite { get; set; } = new DateTime(2027, 10, 30);
        public bool ActivarIncrementoFuturo { get; set; } = true;
        public decimal IncrementoQuincenalFuturo { get; set; } = 138m;
        public DateTime FechaInicioIncremento { get; set; } = new DateTime(2027, 4, 15);
        public decimal AporteQuincenalPostEntrega { get; set; } = 200m;
        public bool ActivarVacaciones { get; set; } = true;
        public decimal MontoVacaciones { get; set; } = 1776m;
        public decimal MontoVacaciones2026 { get; set; } = 888m;
        public int MesVacaciones2026 { get; set; } = 10;
        public int MesVacacionesAnual { get; set; } = 4;
        public List<SubMetaAhorro> SubMetas { get; set; } = new();
        public List<HistorialAhorro> Historial { get; set; } = new();
    }

    public class HistorialAhorro
    {
        public DateTime Fecha { get; set; }
        public int Anio { get; set; }
        public int Quincena { get; set; }
        public decimal Monto { get; set; }
        public string Tipo { get; set; } = "Quincenal"; // "Quincenal", "Décimo", "Extra", "Vacaciones"
        public string MetaNombre { get; set; } = "General";
        public string SubMetaNombre { get; set; } = "Todas / General";
    }

    public class FilaProyeccionAhorro
    {
        public DateTime Fecha { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public decimal AporteQuincenal { get; set; }
        public decimal MontoDecimo { get; set; }
        public decimal MontoVacaciones { get; set; }
        public decimal TotalQuincena => AporteQuincenal + MontoDecimo + MontoVacaciones;
        public decimal Acumulado { get; set; }
        public string Hito { get; set; } = string.Empty;
    }
}
