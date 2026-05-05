using System;
using System.Collections.Generic;
using System.Text;

namespace CalculadoraFiscalUI.Services
{
    public class CalculadoraFiscal
    {
        private const decimal ExentoMax = 11000m;
        private const decimal TopeTramoMedio = 50000m;
        private const decimal TasaTramoMedio = 0.15m;
        private const decimal TasaTramoAlto = 0.25m;
        private const int QuincenasPorAño = 24;

        public decimal CalcularISRQuincenal(decimal salarioBrutoQuincenal)
        {
            decimal salarioAnual = salarioBrutoQuincenal * QuincenasPorAño;
            decimal isrAnual;

            if (salarioAnual <= ExentoMax) isrAnual = 0m;
            else if (salarioAnual <= TopeTramoMedio) isrAnual = (salarioAnual - ExentoMax) * TasaTramoMedio;
            else
            {
                decimal tramoMedio = (TopeTramoMedio - ExentoMax) * TasaTramoMedio;
                decimal tramoAlto = (salarioAnual - TopeTramoMedio) * TasaTramoAlto;
                isrAnual = tramoMedio + tramoAlto;
            }

            return isrAnual / QuincenasPorAño;
        }

        public decimal CalcularSeguroSocial(decimal salario) => salario * 0.0975m;
        public decimal CalcularSeguroEducativo(decimal salario) => salario * 0.0125m;
    }
}
