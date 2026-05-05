using CalculadoraFiscalUI.clases;
using System;
using System.Collections.Generic;
using System.Text;

namespace CalculadoraFiscalUI.Models
{
    public class DatosQuincena
    {
        public PeriodoQuincenal Periodo { get; set; } = new();
        public decimal SalarioFinal { get; set; }
        public List<Gasto> Gastos { get; set; } = new();
        public DateTime FechaGuardado { get; set; }

    }
}
