using System;
using System.Collections.Generic;
using System.Text;

namespace CalculadoraFiscal.CalculadoraFiscal
{
        public class CalculadoraFiscal
        {
            private const decimal ExentoMax = 11000m;
            private const decimal TopeTramoMedio = 50000m;
            private const decimal tasaTramoMedio = 0.15m;
            private const decimal tasaTramoAlto = 0.25m;

            private const int quincenasPorAno = 24;


            public decimal CalcularISRQuincenal(decimal salarioBrutoQuincenal)
            {
                decimal salarioAnual = salarioBrutoQuincenal * quincenasPorAno;
                decimal isrAnual;

                if (salarioAnual <= ExentoMax)
                {
                    isrAnual = 0;
                }
                else if (salarioAnual <= TopeTramoMedio)
                {
                    isrAnual = (salarioAnual - ExentoMax) * tasaTramoMedio;
                }
                else
                {
                    decimal impuestoTramoMedio = (TopeTramoMedio - ExentoMax) * tasaTramoMedio;
                    decimal impuestoTramoAlto = (salarioAnual - TopeTramoMedio) * tasaTramoAlto;
                    isrAnual = impuestoTramoMedio + impuestoTramoAlto;
                }

                return isrAnual / quincenasPorAno;
            }

            public decimal CalcularSeguroSocial(decimal salarioBrutoQuincenal) => salarioBrutoQuincenal * 0.0975m;
            public decimal CalcularSeguroEducativo(decimal salarioBrutoQuincenal) => salarioBrutoQuincenal * 0.0125m;


        }
}
