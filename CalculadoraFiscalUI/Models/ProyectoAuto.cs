using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CalculadoraFiscalUI.Models
{
    public class ProyectoAuto
    {
        public string Nombre { get; set; } = "Mi Auto";
        public decimal FondoActual { get; set; }
        public decimal AporteQuincenal { get; set; }
        public ObservableCollection<ItemChecklistAuto> Checklist { get; set; } = new();
    }
}
