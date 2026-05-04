using CalculadoraFiscalUI.clases;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CalculadoraFiscalUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly CalculadoraFiscal _fiscal = new();
        private ObservableCollection<Gasto> _listaGastos = new();
        public decimal SalarioFinalCalculado { get; private set; } = 0m;
        public MainWindow()
        {
            InitializeComponent();
            DgGastos.ItemsSource = _listaGastos;
            ActualizarResumenGastos();
        }

        // ================= PESTAÑA NÓMINA =================
        private void BtnCalcular_Click(object sender, RoutedEventArgs e)
        {
            string inputSalario = TxtSalario.Text.Replace("$", "").Replace(" ", "");
            if (!decimal.TryParse(inputSalario, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal salario) || salario < 0)
            {
                MessageBox.Show("Ingrese un salario bruto válido (ej: 1200,50)", "Entrada Inválida",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSalario.Focus();
                return;
            }

            string inputOtros = TxtOtrosDescuentos.Text.Replace("$", "").Replace(" ", "");
            decimal otrosDescuentos = 0m;
            if (!string.IsNullOrWhiteSpace(inputOtros))
            {
                if (!decimal.TryParse(inputOtros, NumberStyles.Number, CultureInfo.CurrentCulture, out otrosDescuentos) || otrosDescuentos < 0)
                {
                    MessageBox.Show("Ingrese un monto válido para 'Otros Descuentos' o déjelo vacío.", "Entrada Inválida",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtOtrosDescuentos.Focus();
                    return;
                }
            }

            // Cálculos fiscales
            decimal isr = _fiscal.CalcularISRQuincenal(salario);
            decimal ss = _fiscal.CalcularSeguroSocial(salario);
            decimal se = _fiscal.CalcularSeguroEducativo(salario);

            decimal deduccionesLey = isr + ss + se;
            decimal totalDeducciones = deduccionesLey + otrosDescuentos;

            decimal netoLegal = salario - deduccionesLey;
            SalarioFinalCalculado = netoLegal - otrosDescuentos; // 🔹 Este es el que usamos en Gastos

            // Actualizar UI Nómina
            string fmt = "C2";
            LblISR.Text = $"ISR (Renta): {isr.ToString(fmt)}";
            LblSS.Text = $"Seguro Social (9.75%): {ss.ToString(fmt)}";
            LblSE.Text = $"Seguro Educativo (1.25%): {se.ToString(fmt)}";
            LblOtros.Text = $"Otros Descuentos: {otrosDescuentos.ToString(fmt)}";
            LblTotalDed.Text = $"Total Deducciones: {totalDeducciones.ToString(fmt)}";
            LblNetoLegal.Text = $"📋 Neto después de impuestos: {netoLegal.ToString(fmt)}";
            LblNetoFinal.Text = $"💰 Salario Final a Recibir: {SalarioFinalCalculado.ToString(fmt)}";

            if (otrosDescuentos > 0)
            {
                LblSeparador.Visibility = Visibility.Visible;
                LblAyuda.Text = $"Incluye ${otrosDescuentos.ToString("F2")} en descuentos adicionales";
                LblAyuda.Visibility = Visibility.Visible;
            }
            else
            {
                LblSeparador.Visibility = Visibility.Collapsed;
                LblAyuda.Visibility = Visibility.Collapsed;
            }

            // 🔹 Actualizar también la pestaña de Gastos
            ActualizarSalarioDisponibleEnGastos();
        }

        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            TxtSalario.Clear();
            TxtOtrosDescuentos.Clear();
            TxtSalario.Focus();

            LblISR.Text = "ISR (Renta): $0.00";
            LblSS.Text = "Seguro Social (9.75%): $0.00";
            LblSE.Text = "Seguro Educativo (1.25%): $0.00";
            LblOtros.Text = "Otros Descuentos: $0.00";
            LblTotalDed.Text = "Total Deducciones: $0.00";
            LblNetoLegal.Text = "📋 Neto después de impuestos: $0.00";
            LblNetoFinal.Text = "💰 Salario Final a Recibir: $0.00";
            LblSeparador.Visibility = Visibility.Collapsed;
            LblAyuda.Visibility = Visibility.Collapsed;

            SalarioFinalCalculado = 0m;
            ActualizarSalarioDisponibleEnGastos();
        }

        private void BtnIrAGastos_Click(object sender, RoutedEventArgs e)
        {
            TabPrincipal.SelectedIndex = 1; // Cambia a la pestaña de Gastos
        }
        // ================= PESTAÑA GASTOS =================
        private void ActualizarSalarioDisponibleEnGastos()
        {
            LblSalarioDisponible.Text = SalarioFinalCalculado.ToString("C2");
            ActualizarResumenGastos(); // Recalcular saldo al cambiar el salario
        }
        private void BtnAgregarGasto_Click(object sender, RoutedEventArgs e)
        {
            string nombre = TxtNombreGasto.Text.Trim();
            string inputMonto = TxtMontoGasto.Text.Replace("$", "").Replace(" ", "");

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show("Ingresa un nombre para el gasto", "Campo requerido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNombreGasto.Focus();
                return;
            }

            if (!decimal.TryParse(inputMonto, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal monto) || monto <= 0)
            {
                MessageBox.Show("Ingresa un monto válido mayor a 0", "Entrada Inválida",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtMontoGasto.Focus();
                return;
            }

            _listaGastos.Add(new Gasto { Nombre = nombre, Monto = monto });

            // Limpiar formulario
            TxtNombreGasto.Clear();
            TxtMontoGasto.Clear();
            TxtNombreGasto.Focus();

            ActualizarResumenGastos();
        }

        private void BtnEliminarGasto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Gasto gasto)
            {
                _listaGastos.Remove(gasto);
                ActualizarResumenGastos();
            }
        }
        private void BtnLimpiarGastos_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Eliminar todos los gastos registrados?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _listaGastos.Clear();
                ActualizarResumenGastos();
            }
        }
        private void ActualizarResumenGastos()
        {
            decimal totalGastos = _listaGastos.Sum(g => g.Monto);
            decimal saldoRestante = SalarioFinalCalculado - totalGastos;

            string fmt = "C2";
            LblTotalGastos.Text = $" {totalGastos.ToString(fmt)}";
            LblSaldoRestante.Text = $" {saldoRestante.ToString(fmt)}";

            // Mensaje contextual según el saldo
            if (saldoRestante < 0)
            {
                LblSaldoRestante.Foreground = System.Windows.Media.Brushes.Red;
                LblMensajeSaldo.Text = "⚠️ ¡Atención! Estás gastando más de lo disponible.";
                LblMensajeSaldo.Foreground = System.Windows.Media.Brushes.Red;
            }
            else if (saldoRestante == 0)
            {
                LblSaldoRestante.Foreground = System.Windows.Media.Brushes.DarkOrange;
                LblMensajeSaldo.Text = "⚡ Has usado todo tu salario disponible.";
                LblMensajeSaldo.Foreground = System.Windows.Media.Brushes.Gray;
            }
            else
            {
                LblSaldoRestante.Foreground = System.Windows.Media.Brushes.DarkGreen;
                LblMensajeSaldo.Text = $"✨ ¡Bien! Te quedan {saldoRestante.ToString(fmt)} para ahorrar o imprevistos.";
                LblMensajeSaldo.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }



        public class CalculadoraFiscal
        {
            private const decimal ExentoMax = 11000m;
            private const decimal TopeTramoMedio = 50000m;
            private const decimal TasaTramoMedio = 0.15m;
            private const decimal TasaTramoAlto = 0.25m;
            private const int QuincenasPorAño = 24;

            public decimal CalcularISRQuincenal(decimal salarioBrutoQuincenal)
            {
                decimal salarioAnual = salarioBrutoQuincenal * QuincenasPorAño;
                decimal isrAnual;

                if (salarioAnual <= ExentoMax) isrAnual = 0m;
                else if (salarioAnual <= TopeTramoMedio) isrAnual = (salarioAnual - ExentoMax) * TasaTramoMedio;
                else
                {
                    decimal tramoMedio = (TopeTramoMedio - ExentoMax) * TasaTramoMedio;
                    decimal tramoAlto = (salarioAnual - TopeTramoMedio) * TasaTramoAlto;
                    isrAnual = tramoMedio + tramoAlto;
                }

                return isrAnual / QuincenasPorAño;
            }

            public decimal CalcularSeguroSocial(decimal salario) => salario * 0.0975m;
            public decimal CalcularSeguroEducativo(decimal salario) => salario * 0.0125m;
        }
    }
}