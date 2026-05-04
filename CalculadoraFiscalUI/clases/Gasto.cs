using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace CalculadoraFiscalUI.clases
{
    public class Gasto : INotifyPropertyChanged
    {
        private string _nombre;
        private decimal _monto;

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
