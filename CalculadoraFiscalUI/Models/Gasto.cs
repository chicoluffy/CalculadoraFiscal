using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace CalculadoraFiscalUI.clases
{
    public class Gasto : INotifyPropertyChanged
    {
        private string _nombre = string.Empty;
        private decimal _monto;
        private int _mes;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
