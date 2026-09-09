using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CalculadoraFiscalUI.Models
{
    public class ItemWishlist : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            if (name == nameof(Estado) || name == nameof(PrecioEstimado) || name == nameof(PrecioReal))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Completado)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CostoEfectivo)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CostoFormateado)));
            }
        }

        private string _id = Guid.NewGuid().ToString();
        private string _nombre = "";
        private string _categoria = "General";
        private decimal _precioEstimado;
        private decimal _precioReal;
        private string _enlace = "";
        private string _prioridad = "🟡 Media";
        private string _estado = "⏳ Pendiente";
        private string _notas = "";

        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public string Nombre { get => _nombre; set { _nombre = value; OnPropertyChanged(); } }
        public string Categoria { get => _categoria; set { _categoria = value; OnPropertyChanged(); } }
        public decimal PrecioEstimado { get => _precioEstimado; set { _precioEstimado = value; OnPropertyChanged(); } }
        public decimal PrecioReal { get => _precioReal; set { _precioReal = value; OnPropertyChanged(); } }
        public string Enlace { get => _enlace; set { _enlace = value; OnPropertyChanged(); } }
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
            get => _estado == "✅ Comprado";
            set
            {
                _estado = value ? "✅ Comprado" : "⏳ Pendiente";
                OnPropertyChanged(nameof(Estado));
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public decimal CostoEfectivo => Completado ? (PrecioReal > 0 ? PrecioReal : PrecioEstimado) : PrecioEstimado;

        [JsonIgnore]
        public string CostoFormateado => CostoEfectivo.ToString("C2");
    }

    public class ProyectoWishlist : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _id = Guid.NewGuid().ToString();
        private string _nombre = "Nuevo Proyecto";
        private string _icono = "📦";
        private string _descripcion = "";
        private decimal _presupuestoAsignado;
        private DateTime _fechaCreacion = DateTime.Now;
        private ObservableCollection<ItemWishlist> _items = new();

        public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
        public string Nombre { get => _nombre; set { _nombre = value; OnPropertyChanged(); } }
        public string Icono { get => _icono; set { _icono = value; OnPropertyChanged(); } }
        public string Descripcion { get => _descripcion; set { _descripcion = value; OnPropertyChanged(); } }
        public decimal PresupuestoAsignado { get => _presupuestoAsignado; set { _presupuestoAsignado = value; OnPropertyChanged(); NotificarCalculos(); } }
        public DateTime FechaCreacion { get => _fechaCreacion; set { _fechaCreacion = value; OnPropertyChanged(); } }

        public ObservableCollection<ItemWishlist> Items
        {
            get => _items;
            set
            {
                _items = value;
                OnPropertyChanged();
                NotificarCalculos();
            }
        }

        public void NotificarCalculos()
        {
            OnPropertyChanged(nameof(TotalEstimado));
            OnPropertyChanged(nameof(TotalComprado));
            OnPropertyChanged(nameof(TotalPendiente));
            OnPropertyChanged(nameof(CantidadComprados));
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(PorcentajeItems));
            OnPropertyChanged(nameof(PorcentajeDinero));
            OnPropertyChanged(nameof(EstadoResumen));
        }

        [JsonIgnore]
        public decimal TotalEstimado => Items?.Sum(i => i.PrecioEstimado) ?? 0m;

        [JsonIgnore]
        public decimal TotalComprado => Items?.Where(i => i.Completado).Sum(i => i.PrecioReal > 0 ? i.PrecioReal : i.PrecioEstimado) ?? 0m;

        [JsonIgnore]
        public decimal TotalPendiente => Math.Max(0m, TotalEstimado - TotalComprado);

        [JsonIgnore]
        public int CantidadComprados => Items?.Count(i => i.Completado) ?? 0;

        [JsonIgnore]
        public int TotalItems => Items?.Count ?? 0;

        [JsonIgnore]
        public double PorcentajeItems => TotalItems > 0 ? Math.Min(100.0, ((double)CantidadComprados / TotalItems) * 100.0) : 0.0;

        [JsonIgnore]
        public double PorcentajeDinero => TotalEstimado > 0 ? Math.Min(100.0, ((double)TotalComprado / (double)TotalEstimado) * 100.0) : 0.0;

        [JsonIgnore]
        public string EstadoResumen => TotalItems == 0 ? "Sin ítems" : (CantidadComprados == TotalItems ? "🏆 ¡Proyecto 100% Completado!" : $"{CantidadComprados} de {TotalItems} ítems comprados");
    }
}
