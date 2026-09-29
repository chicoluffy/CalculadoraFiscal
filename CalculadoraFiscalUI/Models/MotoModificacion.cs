using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CalculadoraFiscalUI.Models
{
    public class MotoModificacion : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            if (name == nameof(Estado) || name == nameof(PrecioEstimado) || name == nameof(PrecioReal))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Completado)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CostoEfectivo)));
            }
        }

        private string _id = Guid.NewGuid().ToString();
        private string _nombre = "";
        private string _categoria = "Estética";
        private decimal _precioEstimado;
        private decimal _precioReal;
        private string _prioridad = "🟡 Media";
        private string _estado = "⏳ Deseado";
        private string _notas = "";

        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public string Nombre { get => _nombre; set { _nombre = value; OnPropertyChanged(); } }
        public string Categoria { get => _categoria; set { _categoria = value; OnPropertyChanged(); } }
        public decimal PrecioEstimado { get => _precioEstimado; set { _precioEstimado = value; OnPropertyChanged(); } }
        public decimal PrecioReal { get => _precioReal; set { _precioReal = value; OnPropertyChanged(); } }
        public string Prioridad { get => _prioridad; set { _prioridad = value; OnPropertyChanged(); } }
        public string Estado
        {
            get => _estado;
            set
            {
                _estado = value;
                OnPropertyChanged();
            }
        }
        public string Notas { get => _notas; set { _notas = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public bool Completado
        {
            get => _estado == "✅ Instalado" || _estado == "✅ Comprado";
            set
            {
                _estado = value ? "✅ Instalado" : "⏳ Deseado";
                OnPropertyChanged(nameof(Estado));
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public decimal CostoEfectivo => Completado ? (PrecioReal > 0 ? PrecioReal : PrecioEstimado) : PrecioEstimado;
    }
}
