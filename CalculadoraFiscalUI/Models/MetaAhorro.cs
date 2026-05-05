using System;
using System.Collections.Generic;
using System.Text;

namespace CalculadoraFiscalUI.Models
{
    public class MetaAhorro
    {
        public string Nombre { get; set; } = "Mi Meta";
        public decimal MontoObjetivo { get; set; }
        public decimal MontoActual { get; set; }
        public decimal AportePorQuincena { get; set; }
        public List<HistorialAhorro> Historial { get; set; } = new();
    }

    public class HistorialAhorro
    {
        public DateTime Fecha { get; set; }
        public int Anio { get; set; }
        public int Quincena { get; set; }
        public decimal Monto { get; set; }
    }
}
