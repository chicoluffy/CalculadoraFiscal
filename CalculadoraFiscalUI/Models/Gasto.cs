using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;

namespace CalculadoraFiscalUI.clases
{
    public class Gasto : INotifyPropertyChanged
    {
        private string _nombre = string.Empty;
        private decimal _monto;
        private int _mes;
        private int _quincenaMes = 1;

        public string Nombre
        {
            get => _nombre;
            set { _nombre = value; OnPropertyChanged(nameof(Nombre)); }
        }

        public decimal Monto
        {
            get => _monto;
            set { _monto = value; OnPropertyChanged(nameof(Monto)); }
        }

        public int Mes 
        {
            get => _mes; set { _mes = value; OnPropertyChanged(nameof(Mes)); }
        }
        public int QuincenaMes 
        { 
            get => _quincenaMes; set { _quincenaMes = value; OnPropertyChanged(nameof(QuincenaMes)); OnPropertyChanged(nameof(PeriodoTexto)); } 
        }

        [JsonIgnore]
        public string PeriodoTexto => $"{ObtenerNombreMes(Mes)} ({(QuincenaMes == 1 ? "1ra" : "2da")})";

        private string ObtenerNombreMes(int mes) => mes switch
        {
            1 => "Ene",
            2 => "Feb",
            3 => "Mar",
            4 => "Abr",
            5 => "May",
            6 => "Jun",
            7 => "Jul",
            8 => "Ago",
            9 => "Sep",
            10 => "Oct",
            11 => "Nov",
            12 => "Dic",
            _ => "?"

        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
