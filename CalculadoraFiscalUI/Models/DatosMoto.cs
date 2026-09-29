using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CalculadoraFiscalUI.Models
{
    public class DatosMoto : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _modelo = "Royal Enfield Continental GT 650";
        private int _kilometrajeActual = 5000;
        private int _anno = 2023;
        private ObservableCollection<MotoModificacion> _modificaciones = new();
        private ObservableCollection<ServicioMoto> _servicios = new();

        public string Modelo { get => _modelo; set { _modelo = value; OnPropertyChanged(); } }
        public int KilometrajeActual { get => _kilometrajeActual; set { _kilometrajeActual = value; OnPropertyChanged(); NotificarCalculos(); } }
        public int Anno { get => _anno; set { _anno = value; OnPropertyChanged(); } }

        public ObservableCollection<MotoModificacion> Modificaciones
        {
            get => _modificaciones;
            set { _modificaciones = value; OnPropertyChanged(); NotificarCalculos(); }
        }

        public ObservableCollection<ServicioMoto> Servicios
        {
            get => _servicios;
            set { _servicios = value; OnPropertyChanged(); NotificarCalculos(); }
        }

        public void NotificarCalculos()
        {
            OnPropertyChanged(nameof(TotalEstimadoModificaciones));
            OnPropertyChanged(nameof(TotalCompradoModificaciones));
            OnPropertyChanged(nameof(TotalPendienteModificaciones));
            OnPropertyChanged(nameof(TotalGastadoServicios));
            OnPropertyChanged(nameof(CantidadServiciosPendientes));
        }

        [JsonIgnore]
        public decimal TotalEstimadoModificaciones => Modificaciones?.Sum(m => m.PrecioEstimado) ?? 0m;

        [JsonIgnore]
        public decimal TotalCompradoModificaciones => Modificaciones?.Where(m => m.Completado).Sum(m => m.PrecioReal > 0 ? m.PrecioReal : m.PrecioEstimado) ?? 0m;

        [JsonIgnore]
        public decimal TotalPendienteModificaciones => Math.Max(0m, TotalEstimadoModificaciones - TotalCompradoModificaciones);

        [JsonIgnore]
        public decimal TotalGastadoServicios => Servicios?.Where(s => s.Realizado).Sum(s => s.CostoTotal) ?? 0m;

        [JsonIgnore]
        public int CantidadServiciosPendientes => Servicios?.Count(s => !s.Realizado) ?? 0;
    }
}
