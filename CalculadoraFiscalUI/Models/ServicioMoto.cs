using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CalculadoraFiscalUI.Models
{
    public class ServicioMoto : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            if (name == nameof(CostoRefacciones) || name == nameof(CostoManoObra))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CostoTotal)));
            }
        }

        private string _id = Guid.NewGuid().ToString();
        private string _concepto = "";
        private string _tipo = "🛢️ Mantenimiento Rutinario";
        private int _kilometrajeServicio;
        private int _proximoKilometraje;
        private DateTime _fechaServicio = DateTime.Now;
        private decimal _costoRefacciones;
        private decimal _costoManoObra;
        private bool _realizado = true;
        private string _taller = "Taller / DIY";
        private string _notas = "";

        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public string Concepto { get => _concepto; set { _concepto = value; OnPropertyChanged(); } }
        public string Tipo { get => _tipo; set { _tipo = value; OnPropertyChanged(); } }
        public int KilometrajeServicio { get => _kilometrajeServicio; set { _kilometrajeServicio = value; OnPropertyChanged(); } }
        public int ProximoKilometraje { get => _proximoKilometraje; set { _proximoKilometraje = value; OnPropertyChanged(); } }
        public DateTime FechaServicio { get => _fechaServicio; set { _fechaServicio = value; OnPropertyChanged(); } }
        public decimal CostoRefacciones { get => _costoRefacciones; set { _costoRefacciones = value; OnPropertyChanged(); } }
        public decimal CostoManoObra { get => _costoManoObra; set { _costoManoObra = value; OnPropertyChanged(); } }
        public bool Realizado { get => _realizado; set { _realizado = value; OnPropertyChanged(); } }
        public string Taller { get => _taller; set { _taller = value; OnPropertyChanged(); } }
        public string Notas { get => _notas; set { _notas = value; OnPropertyChanged(); } }

        [JsonIgnore]
        public decimal CostoTotal => CostoRefacciones + CostoManoObra;

        [JsonIgnore]
        public string EstadoFormateado => Realizado ? "✅ Realizado" : "⏳ Pendiente";

        [JsonIgnore]
        public string CuandoSeDebeHacer => ProximoKilometraje > 0 
            ? $"A los {ProximoKilometraje:N0} km" 
            : (KilometrajeServicio > 0 ? $"A los {KilometrajeServicio:N0} km" : "Pendiente de programar");
    }
}
