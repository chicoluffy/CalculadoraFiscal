using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace CalculadoraFiscalUI.Models
{
    public class ItemChecklistAuto : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _nombre;
        private decimal _costo;
        private bool _prioridad;
        private bool _completado;

        public string Nombre { get => _nombre; set { _nombre = value; OnPropertyChanged(); } }
        public decimal CostoEstimado { get => _costo; set { _costo = value; OnPropertyChanged(); OnPropertyChanged(nameof(CostoFormateado)); } }
        public bool EsPrioridad { get => _prioridad; set { _prioridad = value; OnPropertyChanged(); } }
        public bool Completado { get => _completado; set { _completado = value; OnPropertyChanged(); OnPropertyChanged(nameof(Enfoque)); } }
        public string CostoFormateado => CostoEstimado.ToString("C2");
        public string Enfoque => Completado ? "✅ Listo" : (EsPrioridad ? "🔴 Urgente" : "🟡 Planear");

    }
}
