using CalculadoraFiscalUI.clases;
using CalculadoraFiscalUI.Models;
using CalculadoraFiscalUI.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
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
        private readonly RepositorioQuincenas _repo;
        private readonly RepositorioMoto _repoMoto;
        private readonly ObservableCollection<Gasto> _listaGastos = new();

        public decimal SalarioFinalCalculado { get; private set; }
        private PeriodoQuincenal _periodoActual = new();
        private DatosMoto _datosMoto = new();

        public MainWindow()
        {
            InitializeComponent();
            _repo = new RepositorioQuincenas(AppContext.BaseDirectory);
            _repoMoto = new RepositorioMoto(AppContext.BaseDirectory);
            DgGastos.ItemsSource = _listaGastos;
            InicializarPeriodos();
            CargarPeriodoPorDefecto();
            CargarMetaAhorro();
            CargarProyectos();
            CargarDatosMoto();
        }

        #region INICIALIZACIÓN Y CARGA DE DATOS
        private void InicializarPeriodos()
        {
            int anioActual = DateTime.Now.Year;
            CmbAnio.ItemsSource = System.Linq.Enumerable.Range(anioActual - 2, 3).Reverse();
            CmbAnio.SelectedItem = anioActual;
            CmbQuincena.SelectedIndex = 0;
        }

        private void CargarPeriodoPorDefecto()
        {
            ActualizarPeriodoActual();
            CargarGastosDelPeriodo();
        }

        private void ActualizarPeriodoActual()
        {
            int anio = int.TryParse(CmbAnio.SelectedItem?.ToString(), out int a) ? a : DateTime.Now.Year;
            var comboItem = CmbQuincena.SelectedItem as ComboBoxItem;
            int quincena = int.TryParse(comboItem?.Tag?.ToString(), out int q) ? q : 1;

            _periodoActual = new PeriodoQuincenal { Anio = anio, Quincena = quincena };
        }

        private void CmbPeriodo_SelectionChanged(object sender,SelectionChangedEventArgs e)
        {
            if (CmbAnio.SelectedItem == null || CmbQuincena.SelectedItem == null) return;
            ActualizarPeriodoActual();
            CargarPeriodoPorDefecto();
            LblEstadoGuardado.Text = "";
        }
        #endregion

        #region Pestaña Nomina
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

        private void BtnLimpiar_Click(object sender,RoutedEventArgs e)
        {
            TxtSalario.Clear(); TxtOtrosDescuentos.Clear(); TxtSalario.Focus();
            LblISR.Text = "ISR: $0.00"; LblSS.Text = "Seg. Social: $0.00"; LblSE.Text = "Seg. Educativo: $0.00";
            LblOtros.Text = "Otros: $0.00"; LblTotalDed.Text = "Total: $0.00";
            LblNetoLegal.Text = "Neto impuestos: $0.00"; LblNetoFinal.Text = "Final a recibir: $0.00";
            SalarioFinalCalculado = 0m; ActualizarSalarioDisponibleEnGastos();
        }

        private void BtnIrAGastos_Click(object sender, RoutedEventArgs e) => TabPrincipal.SelectedIndex = 1;

        private void ActualizarSalarioDisponibleEnGastos() => AplicarFiltroGastos();
        #endregion

        #region Pestaña Gastos
        private void BtnAgregarGasto_Click(object sender, RoutedEventArgs e)
        {
            string nombre = TxtNombreGasto.Text.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            { MessageBox.Show("Ingresa un concepto", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            if (!decimal.TryParse(TxtMontoGasto.Text.Replace("$", "").Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal monto) || monto <= 0)
            { MessageBox.Show("Monto inválido (>0)", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            // Toma automáticamente el contexto de arriba
            int mes = int.TryParse(((ComboBoxItem)CmbMesContexto.SelectedItem).Tag?.ToString(), out int m) ? m : 1;
            int q = int.TryParse(((ComboBoxItem)CmbQMesContexto.SelectedItem).Tag?.ToString(), out int qTag) ? qTag : 1;

            _listaGastos.Add(new Gasto { Nombre = nombre, Monto = monto, Mes = mes, QuincenaMes = q });
            TxtNombreGasto.Clear(); TxtMontoGasto.Clear(); TxtNombreGasto.Focus();

            AplicarFiltroGastos(); // Refresca tabla
            MarcarComoModificado();
        }


        private void AplicarFiltroGastos()
        {
            // 🛡️ Guarda contra carga inicial o tab no visible
            if (!IsLoaded || CmbMesContexto.SelectedItem == null || CmbQMesContexto.SelectedItem == null) return;

            int mes = int.TryParse(((ComboBoxItem)CmbMesContexto.SelectedItem).Tag?.ToString(), out int m) ? m : 1;
            int q = int.TryParse(((ComboBoxItem)CmbQMesContexto.SelectedItem).Tag?.ToString(), out int qTag) ? qTag : 1;

            var periodoActual = _listaGastos.Where(g => g.Mes == mes && g.QuincenaMes == q).ToList();
            DgGastos.ItemsSource = periodoActual;

            // 🔢 Cálculos centralizados (Reserva Q1 de $300 o Q2 de $350 según la quincena activa)
            decimal totalGastos = periodoActual.Sum(g => g.Monto);
            decimal aporteReserva = (q == 1) ? _metaActual.AporteQ1 : _metaActual.AporteQ2;
            decimal disponibleReal = SalarioFinalCalculado - aporteReserva - totalGastos;
            decimal pendiente = SalarioFinalCalculado - totalGastos;

            // 🖥️ UI Superior (Disponible + Reserva)
            LblSalarioDisponible.Text = disponibleReal.ToString("C2");
            LblReservaAhorro.Text = aporteReserva > 0 ? $"(Reservado Q{q}: {aporteReserva:C2})" : "";
            LblReservaAhorro.Foreground = aporteReserva > SalarioFinalCalculado ? Brushes.Red : new SolidColorBrush(Color.FromRgb(99, 102, 241));

            // 🖥️ UI Inferior (Resumen)
            LblTotalGastos.Text = $" {totalGastos:C2}";
            LblSaldoRestante.Text = $" {pendiente:C2}";
            LblMensajeSaldo.Text = disponibleReal >= 0 ? "✨ Suficiente para gastos + ahorro" : "⚠️ Ajusta tu presupuesto";
            LblMensajeSaldo.Foreground = disponibleReal >= 0 ? Brushes.Gray : Brushes.Red;
        }

        private void BtnEliminarGasto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Gasto gasto)
            {
                _listaGastos.Remove(gasto);
                AplicarFiltroGastos(); // ← Cambiado
                MarcarComoModificado();
            }
        }
        private List<Gasto> ObtenerPlantillaGastos(int anio, int mes, int quincena)
        {
            var gastos = new List<Gasto>();
            // Moto ($138) expira automáticamente después de febrero de 2027 (a partir de marzo 2027)
            bool incluirMoto = !(anio > 2027 || (anio == 2027 && mes > 2));

            if (quincena == 1)
            {
                gastos.Add(new Gasto { Nombre = "🏠 Alquiler Casa", Monto = 75.00m, Mes = mes, QuincenaMes = 1 });
                gastos.Add(new Gasto { Nombre = "🛒 Comida", Monto = 60.00m, Mes = mes, QuincenaMes = 1 });
                gastos.Add(new Gasto { Nombre = "⛽ Gasolina", Monto = 30.00m, Mes = mes, QuincenaMes = 1 });
                gastos.Add(new Gasto { Nombre = "📱 Data", Monto = 10.09m, Mes = mes, QuincenaMes = 1 });
                if (incluirMoto)
                {
                    gastos.Add(new Gasto { Nombre = "🏍️ Cuota Moto", Monto = 138.00m, Mes = mes, QuincenaMes = 1 });
                }
                gastos.Add(new Gasto { Nombre = "🧺 Lavandería", Monto = 23.30m, Mes = mes, QuincenaMes = 1 });
                gastos.Add(new Gasto { Nombre = "🐱 Gatos", Monto = 20.00m, Mes = mes, QuincenaMes = 1 });
                gastos.Add(new Gasto { Nombre = "❤️ Mamá", Monto = 10.50m, Mes = mes, QuincenaMes = 1 });
                gastos.Add(new Gasto { Nombre = "📶 WiFi", Monto = 9.99m, Mes = mes, QuincenaMes = 1 });
                gastos.Add(new Gasto { Nombre = "💑 Salidas Novia", Monto = 120.00m, Mes = mes, QuincenaMes = 1 });
                gastos.Add(new Gasto { Nombre = "🔧 Mantenimiento Moto", Monto = 10.00m, Mes = mes, QuincenaMes = 1 });
            }
            else
            {
                gastos.Add(new Gasto { Nombre = "🏠 Alquiler Casa", Monto = 75.00m, Mes = mes, QuincenaMes = 2 });
                gastos.Add(new Gasto { Nombre = "🛒 Comida", Monto = 60.00m, Mes = mes, QuincenaMes = 2 });
                gastos.Add(new Gasto { Nombre = "⛽ Gasolina", Monto = 30.00m, Mes = mes, QuincenaMes = 2 });
                gastos.Add(new Gasto { Nombre = "📱 Data", Monto = 10.09m, Mes = mes, QuincenaMes = 2 });
                if (incluirMoto)
                {
                    gastos.Add(new Gasto { Nombre = "🏍️ Cuota Moto", Monto = 138.00m, Mes = mes, QuincenaMes = 2 });
                }
                gastos.Add(new Gasto { Nombre = "🧺 Lavandería", Monto = 23.30m, Mes = mes, QuincenaMes = 2 });
                gastos.Add(new Gasto { Nombre = "🐱 Gatos", Monto = 20.00m, Mes = mes, QuincenaMes = 2 });
                gastos.Add(new Gasto { Nombre = "❤️ Mamá", Monto = 10.50m, Mes = mes, QuincenaMes = 2 });
                gastos.Add(new Gasto { Nombre = "📶 WiFi", Monto = 23.99m, Mes = mes, QuincenaMes = 2 });
                gastos.Add(new Gasto { Nombre = "💑 Salidas Novia", Monto = 120.00m, Mes = mes, QuincenaMes = 2 });
                gastos.Add(new Gasto { Nombre = "🏋️ Gym", Monto = 38.00m, Mes = mes, QuincenaMes = 2 });
                gastos.Add(new Gasto { Nombre = "🎵 YouTube Music", Monto = 5.00m, Mes = mes, QuincenaMes = 2 });
            }

            return gastos;
        }

        private void BtnCargarPlantilla_Click(object sender, RoutedEventArgs e)
        {
            if (CmbMesContexto.SelectedItem == null || CmbQMesContexto.SelectedItem == null) return;

            int anio = int.TryParse(CmbAnio.SelectedItem?.ToString(), out int a) ? a : DateTime.Now.Year;
            int mes = int.TryParse(((ComboBoxItem)CmbMesContexto.SelectedItem).Tag?.ToString(), out int m) ? m : 1;
            int q = int.TryParse(((ComboBoxItem)CmbQMesContexto.SelectedItem).Tag?.ToString(), out int qTag) ? qTag : 1;

            var paraEliminar = _listaGastos.Where(g => g.Mes == mes && g.QuincenaMes == q).ToList();
            foreach (var gasto in paraEliminar)
                _listaGastos.Remove(gasto);

            var plantilla = ObtenerPlantillaGastos(anio, mes, q);
            foreach (var gasto in plantilla)
                _listaGastos.Add(gasto);

            AplicarFiltroGastos();
            MarcarComoModificado();
            LblEstadoGuardado.Text = "Plantilla Fija Cargada";
            LblEstadoGuardado.Foreground = Brushes.Blue;
        }

        private void BtnAutoRellenarAnio_Click(object sender, RoutedEventArgs e)
        {
            int anio = int.TryParse(CmbAnio.SelectedItem?.ToString(), out int a) ? a : DateTime.Now.Year;

            if (MessageBox.Show($"¿Auto-rellenar las 24 quincenas del año {anio} con la plantilla de gastos fijos?",
                "Confirmar Auto-Rellenado Anual", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                for (int m = 1; m <= 12; m++)
                {
                    for (int q = 1; q <= 2; q++)
                    {
                        var per = new PeriodoQuincenal { Anio = anio, Quincena = (m - 1) * 2 + q };
                        var datosQuincena = new DatosQuincena
                        {
                            Periodo = per,
                            SalarioFinal = SalarioFinalCalculado,
                            Gastos = ObtenerPlantillaGastos(anio, m, q),
                            FechaGuardado = DateTime.Now
                        };
                        _repo.Guardar(datosQuincena);
                    }
                }

                CargarGastosDelPeriodo();
                MessageBox.Show($"¡Las 24 quincenas del año {anio} han sido auto-rellenadas y guardadas exitosamente!",
                    "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnLimpiarGastos_Click(object sender, RoutedEventArgs e)
        {
            if (CmbMesContexto.SelectedItem == null || CmbQMesContexto.SelectedItem == null) return;

            int mes = int.TryParse(((ComboBoxItem)CmbMesContexto.SelectedItem).Tag?.ToString(), out int m) ? m : 1;
            int q = int.TryParse(((ComboBoxItem)CmbQMesContexto.SelectedItem).Tag?.ToString(), out int qTag) ? qTag : 1;
            string nombreMes = ((ComboBoxItem)CmbMesContexto.SelectedItem).Content?.ToString() ?? "";

            if (MessageBox.Show($"¿Eliminar TODOS los gastos de {nombreMes} ({(q == 1 ? "1ra" : "2da")})?",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var paraEliminar = _listaGastos.Where(g => g.Mes == mes && g.QuincenaMes == q).ToList();
                foreach (var gasto in paraEliminar)
                    _listaGastos.Remove(gasto);

                AplicarFiltroGastos();
                MarcarComoModificado();
            }
        }

        private void CmbContexto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            AplicarFiltroGastos();
        }

        private void ActualizarResumenGastos(decimal totalGastos, decimal disponibleReal)
        {
            decimal pendiente = SalarioFinalCalculado - totalGastos;
            LblTotalGastos.Text = $" {totalGastos.ToString("C2")}";
            LblSaldoRestante.Text = $" {pendiente.ToString("C2")}";
            LblMensajeSaldo.Text = disponibleReal >= 0 ? "✨ Suficiente para gastos + ahorro" : "⚠️ Ajusta tu presupuesto";
            LblMensajeSaldo.Foreground = disponibleReal >= 0 ? System.Windows.Media.Brushes.Gray : System.Windows.Media.Brushes.Red;
        }

        private void MarcarComoModificado()
        {
            LblEstadoGuardado.Text = "⚠️ Cambios no guardados";
            LblEstadoGuardado.Foreground = System.Windows.Media.Brushes.Orange;
        }
        #endregion

        #region Persistencia
        private void BtnGuardarQuincena_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var datos = new DatosQuincena
                {
                    Periodo = _periodoActual,
                    SalarioFinal = SalarioFinalCalculado,
                    Gastos = _listaGastos.ToList(),
                    FechaGuardado = DateTime.Now
                };
                _repo.Guardar(datos);
                LblEstadoGuardado.Text = "Guardado"; LblEstadoGuardado.Foreground = Brushes.DarkGreen;
                MessageBox.Show("Quincena guardada correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarGastosDelPeriodo()
        {
            var datos = _repo.Cargar(_periodoActual);
            if (datos == null)
            {
                _listaGastos.Clear();
                int anio = _periodoActual.Anio;
                int mes = int.TryParse(((ComboBoxItem)CmbMesContexto.SelectedItem)?.Tag?.ToString(), out int m) ? m : 1;
                int q = int.TryParse(((ComboBoxItem)CmbQMesContexto.SelectedItem)?.Tag?.ToString(), out int qTag) ? qTag : 1;

                var plantilla = ObtenerPlantillaGastos(anio, mes, q);
                foreach (var g in plantilla) _listaGastos.Add(g);

                AplicarFiltroGastos();
                LblEstadoGuardado.Text = "Plantilla Fija (Sin Guardar)";
                LblEstadoGuardado.Foreground = Brushes.Orange;
                return;
            }

            SalarioFinalCalculado = datos.SalarioFinal;
            _listaGastos.Clear();
            foreach (var g in datos.Gastos) _listaGastos.Add(g);

            AplicarFiltroGastos();
            LblEstadoGuardado.Text = "Cargado";
            LblEstadoGuardado.Foreground = Brushes.DarkGreen;
        }

        private void BtnEliminarQuincena_Click(object sender, RoutedEventArgs e)
        {
            if (!_repo.Existe(_periodoActual))
            { 
                MessageBox.Show("No hay datos para este periodo", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show($"¿Eliminar Quincena {_periodoActual.Quincena}/{_periodoActual.Anio}?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _repo.Eliminar(_periodoActual);
                _listaGastos.Clear(); SalarioFinalCalculado = 0m; ActualizarSalarioDisponibleEnGastos();
                LblEstadoGuardado.Text = "Eliminada"; LblEstadoGuardado.Foreground = Brushes.Gray;
            }
        }
        #endregion


        #region Ahorro rpg
        private MetaAhorro _metaActual = new();
        private readonly string _rutaMeta = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "meta_ahorro.json");

        private void AsegurarSubMetasPorDefecto()
        {
            if (_metaActual.SubMetas == null) _metaActual.SubMetas = new List<SubMetaAhorro>();

            var defaultMetas = new List<SubMetaAhorro>
            {
                new SubMetaAhorro { Nombre = "🏠 Casa - Gastos Legales + Colchón", MontoObjetivo = 2354.26m, Icono = "🏠" },
                new SubMetaAhorro { Nombre = "🏠 Casa - Abono Inicial", MontoObjetivo = 3500m, Icono = "🏠" },
                new SubMetaAhorro { Nombre = "🛡️ Seguridad de Casa", MontoObjetivo = 1500m, Icono = "🛡️" },
                new SubMetaAhorro { Nombre = "💡 Conexión Servicios & Trámites", MontoObjetivo = 350m, Icono = "💡" },
                new SubMetaAhorro { Nombre = "🏍️ Moto (con ITBMS)", MontoObjetivo = 3700m, Icono = "🏍️" },
                new SubMetaAhorro { Nombre = "🛋️ Fondo Inicial Muebles", MontoObjetivo = 1200m, Icono = "🛋️" },
                new SubMetaAhorro { Nombre = "🚨 Fondo de Emergencia", MontoObjetivo = 3000m, Icono = "🚨" },
                new SubMetaAhorro { Nombre = "📉 Abono a Capital Hipoteca", MontoObjetivo = 0m, Icono = "📉" }
            };

            foreach (var def in defaultMetas)
            {
                string key = def.Nombre switch
                {
                    var n when n.Contains("Gastos Legales") => "Gastos Legales",
                    var n when n.Contains("Abono Inicial") => "Abono Inicial",
                    var n when n.Contains("Seguridad") => "Seguridad",
                    var n when n.Contains("Servicios") => "Servicios",
                    var n when n.Contains("Moto") => "Moto",
                    var n when n.Contains("Muebles") => "Muebles",
                    var n when n.Contains("Emergencia") => "Emergencia",
                    var n when n.Contains("Capital") => "Capital",
                    _ => def.Nombre.Split(' ')[1]
                };

                var existente = _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains(key));
                if (existente == null)
                {
                    _metaActual.SubMetas.Add(def);
                }
                else
                {
                    existente.Nombre = def.Nombre;
                    existente.MontoObjetivo = def.MontoObjetivo;
                    existente.Icono = def.Icono;
                }
            }

            _metaActual.MontoObjetivo = _metaActual.SubMetas.Sum(s => s.MontoObjetivo);
            _metaActual.MontoActual = _metaActual.SubMetas.Sum(s => s.MontoActual);
        }

        private void BtnFijarMeta_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(TxtAporteQ1.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal q1) || q1 < 0)
            { MessageBox.Show("Define un aporte de 1ra quincena válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(TxtAporteQ2.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal q2) || q2 < 0)
            { MessageBox.Show("Define un aporte de 2da quincena válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(TxtMontoDecimo.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal decimo) || decimo < 0)
            { MessageBox.Show("Define un monto de décimo válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            _metaActual.Nombre = string.IsNullOrWhiteSpace(TxtNombreMeta.Text) ? "Plan Casa, Movilidad y Ahorro Continuo" : TxtNombreMeta.Text;
            _metaActual.AporteQ1 = q1;
            _metaActual.AporteQ2 = q2;
            _metaActual.MontoDecimo = decimo;
            _metaActual.AportePorQuincena = (q1 + q2) / 2m;

            if (decimal.TryParse(TxtAportePostEntrega.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal postQ) && postQ >= 0)
                _metaActual.AporteQuincenalPostEntrega = postQ;

            if (DtpFechaLimite.SelectedDate.HasValue)
                _metaActual.FechaLimite = DtpFechaLimite.SelectedDate.Value;
            else
                _metaActual.FechaLimite = new DateTime(2027, 10, 30);

            _metaActual.ActivarIncrementoFuturo = ChkActivarIncremento.IsChecked ?? false;

            if (decimal.TryParse(TxtIncrementoMonto.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal inc) && inc >= 0)
                _metaActual.IncrementoQuincenalFuturo = inc;

            if (DtpFechaIncremento.SelectedDate.HasValue)
                _metaActual.FechaInicioIncremento = DtpFechaIncremento.SelectedDate.Value;
            else
                _metaActual.FechaInicioIncremento = new DateTime(2027, 4, 15);

            _metaActual.ActivarVacaciones = ChkActivarVacaciones.IsChecked ?? false;

            if (decimal.TryParse(TxtMontoVacaciones.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal vac) && vac >= 0)
                _metaActual.MontoVacaciones = vac;

            if (decimal.TryParse(TxtMontoVacaciones2026.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal vac26) && vac26 >= 0)
                _metaActual.MontoVacaciones2026 = vac26;

            ActualizarUIAhorro();
            LblFeedback.Text = $"Plan configurado. ¡Proyección al {_metaActual.FechaLimite:dd/MM/yyyy} actualizada!";
        }

        private void CmbTipoDeposito_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || CmbTipoDeposito.SelectedItem == null) return;
            var comboTipo = CmbTipoDeposito.SelectedItem as ComboBoxItem;
            string tipo = comboTipo?.Tag?.ToString() ?? "";

            switch (tipo)
            {
                case "Aporte Q1":
                    TxtDepositoAhorro.Text = _metaActual.AporteQ1.ToString();
                    break;
                case "Aporte Q2":
                    TxtDepositoAhorro.Text = _metaActual.AporteQ2.ToString();
                    break;
                case "Décimo":
                    TxtDepositoAhorro.Text = _metaActual.MontoDecimo.ToString();
                    break;
                case "Vacaciones":
                    TxtDepositoAhorro.Text = _metaActual.MontoVacaciones.ToString();
                    break;
            }
        }

        private void BtnAgregarAhorro_Click(object sender, RoutedEventArgs e)
        {
            AsegurarSubMetasPorDefecto();

            if (!decimal.TryParse(TxtDepositoAhorro.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal monto) || monto <= 0)
            { MessageBox.Show("Monto inválido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var comboTipo = CmbTipoDeposito.SelectedItem as ComboBoxItem;
            string tipoStr = comboTipo?.Tag?.ToString() ?? "Quincenal";

            var comboDestino = CmbDestinoDeposito.SelectedItem as ComboBoxItem;
            string destinoTag = comboDestino?.Tag?.ToString() ?? "AUTO";

            string destinoNombre = "⚡ Auto (Cascada por Prioridad)";

            if (destinoTag == "AUTO")
            {
                // Cascadas por prioridad diferenciada:
                // Décimo y Vacaciones a Casa primero; Quincenas normales a Moto ($3,700) primero.
                decimal rem = monto;
                string[] ordenMetas;

                if (tipoStr == "Décimo" || tipoStr == "Vacaciones")
                {
                    ordenMetas = new string[]
                    {
                        "Gastos Legales",
                        "Abono Inicial",
                        "Seguridad de Casa",
                        "Conexión Servicios",
                        "Moto",
                        "Fondo Inicial Muebles",
                        "Fondo de Emergencia",
                        "Abono a Capital"
                    };
                }
                else
                {
                    ordenMetas = new string[]
                    {
                        "Moto",
                        "Gastos Legales",
                        "Abono Inicial",
                        "Seguridad de Casa",
                        "Conexión Servicios",
                        "Fondo Inicial Muebles",
                        "Fondo de Emergencia",
                        "Abono a Capital"
                    };
                }

                foreach (var metaPart in ordenMetas)
                {
                    if (rem <= 0) break;
                    var sub = _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains(metaPart));
                    if (sub != null)
                    {
                        if (sub.MontoObjetivo > 0)
                        {
                            decimal faltante = Math.Max(0m, sub.MontoObjetivo - sub.MontoActual);
                            if (faltante > 0)
                            {
                                decimal aporte = Math.Min(rem, faltante);
                                sub.MontoActual += aporte;
                                rem -= aporte;
                            }
                        }
                        else
                        {
                            // Submeta sin tope (Abono a Capital)
                            sub.MontoActual += rem;
                            rem = 0;
                        }
                    }
                }
                destinoNombre = (tipoStr == "Décimo" || tipoStr == "Vacaciones")
                    ? "⚡ Auto (Prioridad Casa - Décimo/Vac)"
                    : "⚡ Auto (Prioridad Moto - Quincenal)";
            }
            else
            {
                var subSel = destinoTag switch
                {
                    "SUB1" => _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains("Gastos Legales")),
                    "SUB2" => _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains("Abono Inicial")),
                    "SUB3" => _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains("Seguridad de Casa")),
                    "SUB8" => _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains("Conexión Servicios")),
                    "SUB4" => _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains("Moto")),
                    "SUB5" => _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains("Fondo Inicial Muebles")),
                    "SUB6" => _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains("Fondo de Emergencia")),
                    "SUB7" => _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains("Abono a Capital")),
                    _ => null
                };

                if (subSel != null)
                {
                    subSel.MontoActual += monto;
                    destinoNombre = subSel.Nombre;
                }
            }

            _metaActual.MontoActual = _metaActual.SubMetas.Sum(s => s.MontoActual);

            _metaActual.Historial.Insert(0, new HistorialAhorro
            {
                Fecha = DateTime.Now,
                Anio = int.TryParse(CmbAnio.SelectedItem?.ToString(), out int a) ? a : DateTime.Now.Year,
                Quincena = (CmbQuincena.SelectedItem as ComboBoxItem)?.Tag != null && int.TryParse(((ComboBoxItem)CmbQuincena.SelectedItem).Tag.ToString(), out int q) ? q : 1,
                Monto = monto,
                Tipo = tipoStr,
                MetaNombre = _metaActual.Nombre,
                SubMetaNombre = destinoNombre
            });

            TxtDepositoAhorro.Clear();
            ActualizarUIAhorro();

            double pct = (double)_metaActual.MontoActual / (double)_metaActual.MontoObjetivo * 100;
            LblFeedback.Text = pct >= 100 ? "🏆 ¡TODAS LAS METAS COMPLETADAS! Excelente." : $"✨ +{monto:C0} ({tipoStr}) abonado a {destinoNombre}.";
        }

        private void ActualizarUIAhorro()
        {
            AsegurarSubMetasPorDefecto();

            _metaActual.MontoObjetivo = _metaActual.SubMetas.Sum(s => s.MontoObjetivo);
            _metaActual.MontoActual = _metaActual.SubMetas.Sum(s => s.MontoActual);

            TxtNombreMeta.Text = _metaActual.Nombre;
            TxtMetaMonto.Text = $"{_metaActual.MontoObjetivo:C0}";

            if (_metaActual.FechaLimite == default)
                _metaActual.FechaLimite = new DateTime(2027, 10, 30);
            if (_metaActual.FechaInicioIncremento == default)
                _metaActual.FechaInicioIncremento = new DateTime(2027, 4, 15);
            if (_metaActual.AporteQuincenalPostEntrega <= 0)
                _metaActual.AporteQuincenalPostEntrega = 200m;

            DtpFechaLimite.SelectedDate = _metaActual.FechaLimite;
            ChkActivarIncremento.IsChecked = _metaActual.ActivarIncrementoFuturo;
            TxtIncrementoMonto.Text = _metaActual.IncrementoQuincenalFuturo.ToString("0.##");
            DtpFechaIncremento.SelectedDate = _metaActual.FechaInicioIncremento;
            TxtAportePostEntrega.Text = _metaActual.AporteQuincenalPostEntrega.ToString("0.##");
            ChkActivarVacaciones.IsChecked = _metaActual.ActivarVacaciones;
            TxtMontoVacaciones.Text = _metaActual.MontoVacaciones.ToString("0.##");
            TxtMontoVacaciones2026.Text = _metaActual.MontoVacaciones2026.ToString("0.##");

            double pct = _metaActual.MontoObjetivo > 0
                ? Math.Min((double)_metaActual.MontoActual / (double)_metaActual.MontoObjetivo * 100, 100)
                : 0;
            PgbProgreso.Value = pct;
            LblPorcentaje.Text = $"{pct:F1}%";
            LblActual.Text = $" {_metaActual.MontoActual:C2}";
            LblNivel.Text = ObtenerNivelRPG(pct);

            // 🏠 Actualizar Tarjetas por Sub-meta (8 SubMetas)
            void ActualizarCard(string key, TextBlock lblMonto, TextBlock lblPct, ProgressBar pgb)
            {
                var sub = _metaActual.SubMetas.FirstOrDefault(s => s.Nombre.Contains(key));
                if (sub != null && lblMonto != null && lblPct != null && pgb != null)
                {
                    lblMonto.Text = sub.MontoObjetivo > 0
                        ? $"${sub.MontoActual:N0} / ${sub.MontoObjetivo:N0}"
                        : $"${sub.MontoActual:N0}";
                    lblPct.Text = sub.MontoObjetivo > 0 ? $" ({sub.Porcentaje:F0}%)" : "";
                    pgb.Value = sub.MontoObjetivo > 0 ? sub.Porcentaje : 100;
                }
            }

            ActualizarCard("Gastos Legales", LblSub1Monto, LblSub1Pct, PgbSub1);
            ActualizarCard("Abono Inicial", LblSub2Monto, LblSub2Pct, PgbSub2);
            ActualizarCard("Seguridad de Casa", LblSub3Monto, LblSub3Pct, PgbSub3);
            ActualizarCard("Conexión Servicios", LblSub8Monto, LblSub8Pct, PgbSub8);
            ActualizarCard("Moto", LblSub4Monto, LblSub4Pct, PgbSub4);
            ActualizarCard("Fondo Inicial Muebles", LblSub5Monto, LblSub5Pct, PgbSub5);
            ActualizarCard("Fondo de Emergencia", LblSub6Monto, LblSub6Pct, PgbSub6);
            ActualizarCard("Abono a Capital", LblSub7Monto, LblSub7Pct, PgbSub7);

            // 🚀 Proyección detallada a Fecha Límite
            DateTime hoy = DateTime.Now;
            DateTime limite = _metaActual.FechaLimite;

            LblTituloProyeccion.Text = $"| 🚀 Proyectado al {limite:dd/MM/yyyy}:";

            int cantQ1 = 0;
            int cantQ2 = 0;
            int cantDecimos = 0;
            decimal proyectadoQuincenas = 0m;
            decimal proyectadoDecimos = 0m;
            decimal proyectadoVacaciones = 0m;

            int startYear = hoy.Year;
            int startMonth = hoy.Month;
            int startQ = hoy.Day <= 15 ? 1 : 2;

            var listaProyeccion = new List<FilaProyeccionAhorro>();
            decimal acumuladoRunning = _metaActual.MontoActual;

            // Recorrido quincenal ordenado desde la fecha actual hasta la Fecha Límite
            for (int y = startYear; y <= limite.Year; y++)
            {
                int mStart = (y == startYear) ? startMonth : 1;
                int mEnd = (y == limite.Year) ? limite.Month : 12;

                for (int m = mStart; m <= mEnd; m++)
                {
                    for (int q = 1; q <= 2; q++)
                    {
                        if (y == startYear && m == startMonth && q < startQ) continue;

                        int diaQ = (q == 1) ? 15 : DateTime.DaysInMonth(y, m);
                        DateTime dtQ = new DateTime(y, m, diaQ);

                        if (dtQ > limite) break;

                        decimal aporteBase = (q == 1) ? _metaActual.AporteQ1 : _metaActual.AporteQ2;
                        decimal incrementoExtra = (_metaActual.ActivarIncrementoFuturo && dtQ >= _metaActual.FechaInicioIncremento)
                            ? _metaActual.IncrementoQuincenalFuturo
                            : 0m;

                        decimal aporteQuincenaTotal = aporteBase + incrementoExtra;

                        decimal decimoEstePeriodo = 0m;
                        if (q == 1 && (m == 4 || m == 8 || m == 12) && dtQ <= limite)
                        {
                            cantDecimos++;
                            decimoEstePeriodo = _metaActual.MontoDecimo;
                        }

                        decimal vacacionesEstePeriodo = 0m;
                        if (_metaActual.ActivarVacaciones && q == 1 && dtQ <= limite)
                        {
                            if (y == 2026 && m == _metaActual.MesVacaciones2026)
                            {
                                vacacionesEstePeriodo = _metaActual.MontoVacaciones2026;
                            }
                            else if (y > 2026 && m == _metaActual.MesVacacionesAnual)
                            {
                                vacacionesEstePeriodo = _metaActual.MontoVacaciones;
                            }
                        }

                        // 🛡️ Descontar depósitos ya realizados en este periodo para no duplicar el acumulado
                        decimal yaDepositadoQ = _metaActual.Historial
                            .Where(h => (h.Anio == y || h.Fecha.Year == y) && h.Fecha.Month == m && h.Quincena == q && !h.Tipo.Contains("Décimo") && !h.Tipo.Contains("Vacacion"))
                            .Sum(h => h.Monto);

                        decimal yaDepositadoDec = _metaActual.Historial
                            .Where(h => (h.Anio == y || h.Fecha.Year == y) && h.Fecha.Month == m && h.Quincena == q && h.Tipo.Contains("Décimo"))
                            .Sum(h => h.Monto);

                        decimal yaDepositadoVac = _metaActual.Historial
                            .Where(h => (h.Anio == y || h.Fecha.Year == y) && h.Fecha.Month == m && h.Quincena == q && h.Tipo.Contains("Vacacion"))
                            .Sum(h => h.Monto);

                        decimal aporteQuincenalPendiente = Math.Max(0m, aporteQuincenaTotal - yaDepositadoQ);
                        decimal decimoPendiente = Math.Max(0m, decimoEstePeriodo - yaDepositadoDec);
                        decimal vacacionesPendiente = Math.Max(0m, vacacionesEstePeriodo - yaDepositadoVac);

                        proyectadoQuincenas += aporteQuincenalPendiente;
                        proyectadoDecimos += decimoPendiente;
                        proyectadoVacaciones += vacacionesPendiente;

                        if (q == 1) cantQ1++; else cantQ2++;

                        acumuladoRunning += (aporteQuincenalPendiente + decimoPendiente + vacacionesPendiente);

                        string hito = "🏍️ Moto (con ITBMS - Enero 2027)";
                        if (decimoPendiente > 0 || vacacionesPendiente > 0)
                        {
                            hito = "🏠 Casa - Gastos Legales (Décimo/Vac)";
                        }
                        else if (dtQ > new DateTime(2027, 1, 31) || acumuladoRunning >= 3700.00m)
                        {
                            if (acumuladoRunning >= 15604.26m)
                                hito = "📉 Abono a Capital Hipoteca";
                            else if (acumuladoRunning >= 12604.26m)
                                hito = "🚨 Fondo de Emergencia";
                            else if (acumuladoRunning >= 11404.26m)
                                hito = "🛋️ Fondo Inicial Muebles";
                            else if (acumuladoRunning >= 11054.26m)
                                hito = "💡 Conexión Servicios & Trámites";
                            else if (acumuladoRunning >= 9554.26m)
                                hito = "🛡️ Seguridad de Casa";
                            else if (acumuladoRunning >= 6054.26m)
                                hito = "🏠 Abono Inicial Casa";
                            else
                                hito = "🏠 Gastos Legales + Colchón";
                        }

                        listaProyeccion.Add(new FilaProyeccionAhorro
                        {
                            Fecha = dtQ,
                            Periodo = $"{y}-Q{q}",
                            AporteQuincenal = aporteQuincenalPendiente,
                            MontoDecimo = decimoPendiente,
                            MontoVacaciones = vacacionesPendiente,
                            Acumulado = acumuladoRunning,
                            Hito = hito
                        });
                    }
                }
            }

            DgProyeccionQuincenal.ItemsSource = listaProyeccion;

            decimal totalProyectado = _metaActual.MontoActual + proyectadoQuincenas + proyectadoDecimos + proyectadoVacaciones;
            LblProyeccionDic2027.Text = $" {totalProyectado:C2}";

            // === Evaluación de Prioridades: Vivienda ($7,704.26) vs. Moto+Muebles ($4,900) vs. Fondo Emergencia ($3,000) ===
            decimal metaViviendaTotal = 7704.26m; // Legales + Colchón ($2,354.26) + Abono ($3,500) + Seguridad ($1,500) + Servicios ($350)
            decimal metaMotoMueblesTotal = 4900m; // Moto ($3,700) + Muebles ($1,200)
            decimal metaEmergenciaTotal = 3000m; // Fondo de Emergencia

            if (totalProyectado >= metaViviendaTotal)
            {
                LblEstadoCasa.Text = $"✅ ¡100% CUBIERTA! Acumulas {totalProyectado:C0} al {limite:dd/MM/yyyy} (superas los ${metaViviendaTotal:N0} de Legales, Abono, Seguridad y Servicios).";

                decimal sobranteParaMoto = totalProyectado - metaViviendaTotal;

                // Simulación Post-Entrega Dual (Moto+Muebles y Emergencia)
                DateTime dtSimMoto = limite;
                DateTime dtSimEmergencia = limite;
                decimal acumuladoPostMoto = sobranteParaMoto;
                decimal acumuladoPostEmergencia = sobranteParaMoto;
                int qExtraMoto = 0;
                int qExtraEmergencia = 0;
                decimal metaTotalGeneral = metaMotoMueblesTotal + metaEmergenciaTotal; // $7,900 sobrante total requerido

                while (acumuladoPostEmergencia < metaTotalGeneral)
                {
                    qExtraEmergencia++;
                    if (dtSimEmergencia.Day <= 15)
                    {
                        dtSimEmergencia = new DateTime(dtSimEmergencia.Year, dtSimEmergencia.Month, DateTime.DaysInMonth(dtSimEmergencia.Year, dtSimEmergencia.Month));
                    }
                    else
                    {
                        int nMonth = dtSimEmergencia.Month == 12 ? 1 : dtSimEmergencia.Month + 1;
                        int nYear = dtSimEmergencia.Month == 12 ? dtSimEmergencia.Year + 1 : dtSimEmergencia.Year;
                        dtSimEmergencia = new DateTime(nYear, nMonth, 15);
                    }

                    int qSim = dtSimEmergencia.Day <= 15 ? 1 : 2;
                    decimal aAporte = _metaActual.AporteQuincenalPostEntrega > 0 ? _metaActual.AporteQuincenalPostEntrega : 200m;
                    decimal aporteExtra = aAporte;

                    if (qSim == 1 && (dtSimEmergencia.Month == 4 || dtSimEmergencia.Month == 8 || dtSimEmergencia.Month == 12))
                    {
                        aporteExtra += _metaActual.MontoDecimo;
                    }
                    if (_metaActual.ActivarVacaciones && qSim == 1 && dtSimEmergencia.Month == _metaActual.MesVacacionesAnual)
                    {
                        aporteExtra += _metaActual.MontoVacaciones;
                    }

                    if (acumuladoPostMoto < metaMotoMueblesTotal)
                    {
                        acumuladoPostMoto += aporteExtra;
                        dtSimMoto = dtSimEmergencia;
                        qExtraMoto = qExtraEmergencia;
                    }

                    acumuladoPostEmergencia += aporteExtra;
                }

                if (sobranteParaMoto >= metaMotoMueblesTotal)
                {
                    decimal excedenteAhorroContinuo = sobranteParaMoto - metaMotoMueblesTotal;
                    LblEstadoMoto.Text = $"✅ ¡100% CUBIERTAS! Moto ($3.7k) y Muebles ($1.2k) listos al {limite:dd/MM/yyyy}.";
                    LblEstadoAhorroContinuo.Text = $"🚀 Excedente de ${excedenteAhorroContinuo:N0} pasa a Fondo de Emergencia.\n🎯 Emergencia ($3k) se completa el {dtSimEmergencia:dd/MM/yyyy} ({qExtraEmergencia} Qs post-entrega).";
                    LblEstadoMeta2027.Text = $"🚀 Plan Excelente: Superas tus metas principales por ${excedenteAhorroContinuo:N0} al {limite:dd/MM/yyyy}.";
                    LblEstadoMeta2027.Foreground = Brushes.LightGreen;
                }
                else
                {
                    decimal pctMoto = (sobranteParaMoto / metaMotoMueblesTotal) * 100m;
                    decimal faltanteMoto = metaMotoMueblesTotal - sobranteParaMoto;

                    LblEstadoMoto.Text = $"⚠️ Acumularás ${sobranteParaMoto:N0} ({pctMoto:F0}%) al {limite:dd/MM/yyyy}.\n🎯 Moto + Muebles se completan el {dtSimMoto:dd/MM/yyyy} ({qExtraMoto} Qs post-entrega a ${_metaActual.AporteQuincenalPostEntrega:N0}/Q).";
                    LblEstadoAhorroContinuo.Text = $"✨ A partir del {dtSimMoto:dd/MM/yyyy}, el ahorro pasa a Fondo de Emergencia ($3k), completándose el {dtSimEmergencia:dd/MM/yyyy} ({qExtraEmergencia} Qs post-entrega).";
                    LblEstadoMeta2027.Text = $"🏠 Vivienda 100% asegurada. 🏍️ Moto+Muebles listos en {dtSimMoto:MMMM yyyy}.";
                    LblEstadoMeta2027.Foreground = Brushes.Orange;
                }
            }
            else
            {
                decimal faltanteCasa = metaViviendaTotal - totalProyectado;
                LblEstadoCasa.Text = $"⚠️ Te faltarían ${faltanteCasa:N0} para completar los $8,850 de la Vivienda al {limite:dd/MM/yyyy}.";
                LblEstadoMoto.Text = "❌ Aplazada. Toda la prioridad financiera se enfoca en la Vivienda.";
                LblEstadoAhorroContinuo.Text = "⏳ En espera de completar la vivienda y movilidad.";
                LblEstadoMeta2027.Text = $"⚠️ Faltan ${faltanteCasa:C0} para la meta de la vivienda al {limite:dd/MM/yyyy}. Recorta gastos o ajusta el aporte.";
                LblEstadoMeta2027.Foreground = Brushes.Red;
            }

            // === Historial ===
            DgHistorialAhorro.ItemsSource = _metaActual.Historial.OrderByDescending(h => h.Fecha).ToList();
        }

        private void BtnEliminarDeposito_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is HistorialAhorro item)
            {
                if (MessageBox.Show($"¿Eliminar el depósito de {item.Monto:C2} ({item.SubMetaNombre})?",
                    "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _metaActual.Historial.Remove(item);

                    var sub = _metaActual.SubMetas.FirstOrDefault(s => s.Nombre == item.SubMetaNombre);
                    if (sub != null)
                    {
                        sub.MontoActual = Math.Max(0m, sub.MontoActual - item.Monto);
                    }
                    else
                    {
                        decimal aRestar = item.Monto;
                        foreach (var s in _metaActual.SubMetas.AsEnumerable().Reverse())
                        {
                            if (s.MontoActual > 0 && aRestar > 0)
                            {
                                decimal des = Math.Min(s.MontoActual, aRestar);
                                s.MontoActual -= des;
                                aRestar -= des;
                            }
                        }
                    }

                    _metaActual.MontoActual = _metaActual.SubMetas.Sum(s => s.MontoActual);
                    ActualizarUIAhorro();
                }
            }
        }

        private string ObtenerNivelRPG(double pct) => pct switch
        {
            >= 100 => "🏆 Nivel MAX: Leyenda (Metas Logradas)",
            >= 80 => "🔥 Nivel 5: Experto",
            >= 60 => "🛡️ Nivel 4: Veterano",
            >= 40 => "⚔️ Nivel 3: Aventurero",
            >= 20 => "🌱 Nivel 2: Aprendiz",
            _ => "🟢 Nivel 1: Novato"
        };

        private void BtnGuardarAhorro_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AsegurarSubMetasPorDefecto();
                if (DtpFechaLimite.SelectedDate.HasValue) _metaActual.FechaLimite = DtpFechaLimite.SelectedDate.Value;
                _metaActual.ActivarIncrementoFuturo = ChkActivarIncremento.IsChecked ?? false;
                if (decimal.TryParse(TxtIncrementoMonto.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal inc))
                    _metaActual.IncrementoQuincenalFuturo = inc;
                if (DtpFechaIncremento.SelectedDate.HasValue) _metaActual.FechaInicioIncremento = DtpFechaIncremento.SelectedDate.Value;

                _metaActual.ActivarVacaciones = ChkActivarVacaciones.IsChecked ?? false;
                if (decimal.TryParse(TxtMontoVacaciones.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal vac))
                    _metaActual.MontoVacaciones = vac;
                if (decimal.TryParse(TxtMontoVacaciones2026.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal vac26))
                    _metaActual.MontoVacaciones2026 = vac26;

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_rutaMeta)!);
                string json = System.Text.Json.JsonSerializer.Serialize(_metaActual, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(_rutaMeta, json, System.Text.Encoding.UTF8);
                MessageBox.Show("Progreso de ahorro guardado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void CargarMetaAhorro()
        {
            if (!System.IO.File.Exists(_rutaMeta))
            {
                AsegurarSubMetasPorDefecto();
                TxtNombreMeta.Text = _metaActual.Nombre;
                TxtMetaMonto.Text = $"{_metaActual.MontoObjetivo:C0}";
                TxtAporteQ1.Text = _metaActual.AporteQ1.ToString();
                TxtAporteQ2.Text = _metaActual.AporteQ2.ToString();
                TxtMontoDecimo.Text = _metaActual.MontoDecimo.ToString();
                ActualizarUIAhorro();
                return;
            }
            try
            {
                string json = System.IO.File.ReadAllText(_rutaMeta, System.Text.Encoding.UTF8);
                var meta = System.Text.Json.JsonSerializer.Deserialize<MetaAhorro>(json);
                if (meta != null)
                {
                    _metaActual = meta;
                    AsegurarSubMetasPorDefecto();
                    TxtNombreMeta.Text = meta.Nombre;
                    TxtMetaMonto.Text = $"{meta.MontoObjetivo:C0}";
                    TxtAporteQ1.Text = meta.AporteQ1 > 0 ? meta.AporteQ1.ToString() : "300";
                    TxtAporteQ2.Text = meta.AporteQ2 > 0 ? meta.AporteQ2.ToString() : "300";
                    TxtMontoDecimo.Text = meta.MontoDecimo > 0 ? meta.MontoDecimo.ToString() : "588";
                    TxtMontoVacaciones.Text = meta.MontoVacaciones > 0 ? meta.MontoVacaciones.ToString() : "888";
                    TxtMontoVacaciones2026.Text = meta.MontoVacaciones2026 > 0 ? meta.MontoVacaciones2026.ToString() : "444";
                    ChkActivarVacaciones.IsChecked = meta.ActivarVacaciones;
                    ActualizarUIAhorro();
                }
            }
            catch { /* Ignorar si está corrupto */ }
        }
        #endregion

        #region PROYECTOS & WISHLISTS
        private ObservableCollection<ProyectoWishlist> _listaProyectos = new();
        private ProyectoWishlist? _proyectoSeleccionado = null;
        private readonly string _rutaProyectos = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "proyectos_wishlist.json");

        private void CargarProyectos()
        {
            try
            {
                if (System.IO.File.Exists(_rutaProyectos))
                {
                    string json = System.IO.File.ReadAllText(_rutaProyectos, System.Text.Encoding.UTF8);
                    var lista = System.Text.Json.JsonSerializer.Deserialize<List<ProyectoWishlist>>(json);
                    if (lista != null && lista.Count > 0)
                    {
                        _listaProyectos = new ObservableCollection<ProyectoWishlist>(lista);
                    }
                }
            }
            catch { /* Ignorar si está corrupto */ }

            if (_listaProyectos.Count == 0)
            {
                CargarPlantillasIniciales();
            }

            CmbProyectos.ItemsSource = _listaProyectos;
            if (_listaProyectos.Count > 0)
            {
                CmbProyectos.SelectedIndex = 0;
            }
        }

        private void CargarPlantillasIniciales()
        {
            _listaProyectos.Clear();

            // Proyecto 1: Equipamiento Moto
            var proyMoto = new ProyectoWishlist
            {
                Nombre = "🏍️ Equipamiento Moto",
                Icono = "🏍️",
                Descripcion = "Casco certificado, botas, chaqueta y equipo de protección"
            };
            proyMoto.Items.Add(new ItemWishlist { Categoria = "Seguridad", Nombre = "Casco Certificado (ECE 22.06 / DOT)", PrecioEstimado = 250m, Prioridad = "🔴 Urgente", Estado = "⏳ Pendiente" });
            proyMoto.Items.Add(new ItemWishlist { Categoria = "Calzado", Nombre = "Botas de Protección para Moto", PrecioEstimado = 160m, Prioridad = "🔴 Urgente", Estado = "⏳ Pendiente" });
            proyMoto.Items.Add(new ItemWishlist { Categoria = "Protección", Nombre = "Chaqueta / Chamarra con Protecciones", PrecioEstimado = 180m, Prioridad = "🟠 Alta", Estado = "⏳ Pendiente" });
            proyMoto.Items.Add(new ItemWishlist { Categoria = "Accesorios", Nombre = "Guantes Reforzados Kevlar/Cuero", PrecioEstimado = 55m, Prioridad = "🟠 Alta", Estado = "⏳ Pendiente" });
            proyMoto.Items.Add(new ItemWishlist { Categoria = "Tecnología", Nombre = "Intercomunicador Bluetooth para Casco", PrecioEstimado = 90m, Prioridad = "🟡 Media", Estado = "⏳ Pendiente" });
            proyMoto.Items.Add(new ItemWishlist { Categoria = "Mantenimiento", Nombre = "Impermeable / Capote de Lluvia", PrecioEstimado = 45m, Prioridad = "🟡 Media", Estado = "⏳ Pendiente" });

            // Proyecto 2: Armar PC Gaming
            var proyPC = new ProyectoWishlist
            {
                Nombre = "🖥️ Armar PC Gaming",
                Icono = "🖥️",
                Descripcion = "Componentes para armado de PC de alto rendimiento"
            };
            proyPC.Items.Add(new ItemWishlist { Categoria = "Procesador", Nombre = "CPU (ej. Ryzen 7 / Intel i7)", PrecioEstimado = 280m, Prioridad = "🔴 Urgente", Estado = "⏳ Pendiente" });
            proyPC.Items.Add(new ItemWishlist { Categoria = "Tarjeta de Video", Nombre = "GPU (ej. RTX 4070 Super / RX 7800 XT)", PrecioEstimado = 600m, Prioridad = "🔴 Urgente", Estado = "⏳ Pendiente" });
            proyPC.Items.Add(new ItemWishlist { Categoria = "Placa Base", Nombre = "Motherboard B650 / Z790 WiFi", PrecioEstimado = 170m, Prioridad = "🟠 Alta", Estado = "⏳ Pendiente" });
            proyPC.Items.Add(new ItemWishlist { Categoria = "Memoria RAM", Nombre = "RAM 32GB DDR5 (2x16GB 6000MHz)", PrecioEstimado = 110m, Prioridad = "🟠 Alta", Estado = "⏳ Pendiente" });
            proyPC.Items.Add(new ItemWishlist { Categoria = "Almacenamiento", Nombre = "SSD NVMe M.2 2TB PCIe 4.0", PrecioEstimado = 130m, Prioridad = "🟠 Alta", Estado = "⏳ Pendiente" });
            proyPC.Items.Add(new ItemWishlist { Categoria = "Fuente", Nombre = "Fuente de Poder 750W 80+ Gold", PrecioEstimado = 100m, Prioridad = "🟠 Alta", Estado = "⏳ Pendiente" });
            proyPC.Items.Add(new ItemWishlist { Categoria = "Gabinete / Cooler", Nombre = "Gabinete Mesh + Enfriamiento Líquido 240mm", PrecioEstimado = 120m, Prioridad = "🟡 Media", Estado = "⏳ Pendiente" });
            proyPC.Items.Add(new ItemWishlist { Categoria = "Monitor", Nombre = "Monitor 27\" 1440p 165Hz IPS", PrecioEstimado = 230m, Prioridad = "🟡 Media", Estado = "⏳ Pendiente" });

            // Proyecto 3: Remodelación Cuarto PC
            var proyCuarto = new ProyectoWishlist
            {
                Nombre = "🏗️ Construcción Cuarto PC",
                Icono = "🏗️",
                Descripcion = "Materiales y adecuación de pequeño cuarto para espacio personal y PC"
            };
            proyCuarto.Items.Add(new ItemWishlist { Categoria = "Materiales", Nombre = "Paneles de Gypsum / Paredes / Bloques", PrecioEstimado = 350m, Prioridad = "🔴 Urgente", Estado = "⏳ Pendiente" });
            proyCuarto.Items.Add(new ItemWishlist { Categoria = "Electricidad", Nombre = "Cableado Eléctrico & Tomacorrientes dedicados", PrecioEstimado = 120m, Prioridad = "🔴 Urgente", Estado = "⏳ Pendiente" });
            proyCuarto.Items.Add(new ItemWishlist { Categoria = "Mano de Obra", Nombre = "Instalación / Trabajo de Construcción", PrecioEstimado = 300m, Prioridad = "🟠 Alta", Estado = "⏳ Pendiente" });
            proyCuarto.Items.Add(new ItemWishlist { Categoria = "Acabados", Nombre = "Pintura e Iluminación LED", PrecioEstimado = 80m, Prioridad = "🟡 Media", Estado = "⏳ Pendiente" });

            // Proyecto 4: Muebles & Casa
            var proyMuebles = new ProyectoWishlist
            {
                Nombre = "🛋️ Muebles & Electrodomésticos",
                Icono = "🛋️",
                Descripcion = "Equipamiento básico de hogar: Cama Queen, Estufa, Aire, TV, Sillón"
            };
            proyMuebles.Items.Add(new ItemWishlist { Categoria = "Dormitorio", Nombre = "🛏️ Cama Grande Queen Size", PrecioEstimado = 350m, Prioridad = "🔴 Urgente", Estado = "⏳ Pendiente" });
            proyMuebles.Items.Add(new ItemWishlist { Categoria = "Cocina", Nombre = "🍳 Estufa de Cocina", PrecioEstimado = 220m, Prioridad = "🔴 Urgente", Estado = "⏳ Pendiente" });
            proyMuebles.Items.Add(new ItemWishlist { Categoria = "Climatización", Nombre = "❄️ Aire Acondicionado (Inverter)", PrecioEstimado = 350m, Prioridad = "🟠 Alta", Estado = "⏳ Pendiente" });
            proyMuebles.Items.Add(new ItemWishlist { Categoria = "Entretenimiento", Nombre = "📺 Televisor Smart TV", PrecioEstimado = 280m, Prioridad = "🟡 Media", Estado = "⏳ Pendiente" });
            proyMuebles.Items.Add(new ItemWishlist { Categoria = "Sala", Nombre = "🛋️ Sillón Pequeño / Compacto (Económico/Inflable)", PrecioEstimado = 60m, Prioridad = "🟢 Opcional", Estado = "⏳ Pendiente" });

            // Proyecto 5: Seguridad Casa
            var proySeguridad = new ProyectoWishlist
            {
                Nombre = "🛡️ Seguridad Casa",
                Icono = "🛡️",
                Descripcion = "Puerta blindada, verjas ventanas francesas y cámaras exteriores"
            };
            proySeguridad.Items.Add(new ItemWishlist { Categoria = "Puerta Principal", Nombre = "🚪 Puerta Blindada de Seguridad", PrecioEstimado = 225m, Prioridad = "🔴 Urgente", Estado = "⏳ Pendiente" });
            proySeguridad.Items.Add(new ItemWishlist { Categoria = "Ventanas", Nombre = "🪟 Verjas para Ventanas Francesas", PrecioEstimado = 350m, Prioridad = "🔴 Urgente", Estado = "⏳ Pendiente" });
            proySeguridad.Items.Add(new ItemWishlist { Categoria = "Cámaras", Nombre = "📹 2 Cámaras de Seguridad Exteriores (Instalación Propia)", PrecioEstimado = 70m, Prioridad = "🟠 Alta", Estado = "⏳ Pendiente" });
            proySeguridad.Items.Add(new ItemWishlist { Categoria = "Instalación", Nombre = "🛠️ Mano de Obra (Instalación Puerta y Verjas)", PrecioEstimado = 150m, Prioridad = "🟠 Alta", Estado = "⏳ Pendiente" });

            _listaProyectos.Add(proyCuarto);
            _listaProyectos.Add(proyMuebles);
            _listaProyectos.Add(proySeguridad);
            _listaProyectos.Add(proyMoto);
            _listaProyectos.Add(proyPC);
        }

        private void CmbProyectos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProyectos.SelectedItem is ProyectoWishlist proy)
            {
                _proyectoSeleccionado = proy;
                DgItemsWishlist.ItemsSource = _proyectoSeleccionado.Items;
                SuscribirItemsProyecto();
                ActualizarUIWishlist();
            }
            else
            {
                _proyectoSeleccionado = null;
                DgItemsWishlist.ItemsSource = null;
                ActualizarUIWishlist();
            }
        }

        private void SuscribirItemsProyecto()
        {
            if (_proyectoSeleccionado == null) return;
            foreach (var item in _proyectoSeleccionado.Items)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                item.PropertyChanged += Item_PropertyChanged;
            }
        }

        private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_proyectoSeleccionado != null)
            {
                _proyectoSeleccionado.NotificarCalculos();
                ActualizarUIWishlist();
            }
        }

        private void ActualizarUIWishlist()
        {
            if (_proyectoSeleccionado == null)
            {
                LblProyectoEstimado.Text = "$0.00";
                LblProyectoComprado.Text = "$0.00";
                LblProyectoPendiente.Text = "$0.00";
                LblProyectoProgreso.Text = "0 de 0 (0%)";
                return;
            }

            _proyectoSeleccionado.NotificarCalculos();
            LblProyectoEstimado.Text = $"{_proyectoSeleccionado.TotalEstimado:C2}";
            LblProyectoComprado.Text = $"{_proyectoSeleccionado.TotalComprado:C2}";
            LblProyectoPendiente.Text = $"{_proyectoSeleccionado.TotalPendiente:C2}";
            LblProyectoProgreso.Text = $"{_proyectoSeleccionado.CantidadComprados} de {_proyectoSeleccionado.TotalItems} ({_proyectoSeleccionado.PorcentajeItems:F0}%)";
        }

        private void BtnNuevoProyecto_Click(object sender, RoutedEventArgs e)
        {
            string nombre = Microsoft.VisualBasic.Interaction.InputBox("Nombre del nuevo proyecto / wishlist:", "Nuevo Proyecto", "Mi Proyecto Personal");
            if (string.IsNullOrWhiteSpace(nombre)) return;

            var nuevo = new ProyectoWishlist
            {
                Nombre = nombre.Trim(),
                Icono = "📦"
            };

            _listaProyectos.Add(nuevo);
            CmbProyectos.SelectedItem = nuevo;
            LblEstadoWishlist.Text = $"Proyecto '{nuevo.Nombre}' creado";
            LblEstadoWishlist.Foreground = System.Windows.Media.Brushes.Blue;
        }

        private void BtnEliminarProyecto_Click(object sender, RoutedEventArgs e)
        {
            if (_proyectoSeleccionado == null) return;

            if (MessageBox.Show($"¿Eliminar el proyecto '{_proyectoSeleccionado.Nombre}' y todos sus componentes?",
                "Confirmar Eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                string nombre = _proyectoSeleccionado.Nombre;
                _listaProyectos.Remove(_proyectoSeleccionado);
                if (_listaProyectos.Count > 0)
                    CmbProyectos.SelectedIndex = 0;
                else
                    CmbProyectos.SelectedItem = null;

                LblEstadoWishlist.Text = $"Proyecto '{nombre}' eliminado";
                LblEstadoWishlist.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void BtnPlantillaPC_Click(object sender, RoutedEventArgs e)
        {
            var existente = _listaProyectos.FirstOrDefault(p => p.Nombre.Contains("PC Gaming"));
            if (existente != null)
            {
                CmbProyectos.SelectedItem = existente;
            }
            else
            {
                var proyPC = new ProyectoWishlist
                {
                    Nombre = "🖥️ Armar PC Gaming",
                    Icono = "🖥️",
                    Descripcion = "Componentes para armado de PC"
                };
                proyPC.Items.Add(new ItemWishlist { Categoria = "Procesador", Nombre = "CPU (Ryzen 7 / i7)", PrecioEstimado = 280m, Prioridad = "🔴 Urgente" });
                proyPC.Items.Add(new ItemWishlist { Categoria = "Tarjeta de Video", Nombre = "GPU (RTX 4070 Super)", PrecioEstimado = 600m, Prioridad = "🔴 Urgente" });
                proyPC.Items.Add(new ItemWishlist { Categoria = "Placa Base", Nombre = "Motherboard B650 / Z790", PrecioEstimado = 170m, Prioridad = "🟠 Alta" });
                proyPC.Items.Add(new ItemWishlist { Categoria = "Memoria RAM", Nombre = "RAM 32GB DDR5", PrecioEstimado = 110m, Prioridad = "🟠 Alta" });
                proyPC.Items.Add(new ItemWishlist { Categoria = "Almacenamiento", Nombre = "SSD NVMe M.2 2TB", PrecioEstimado = 130m, Prioridad = "🟠 Alta" });
                proyPC.Items.Add(new ItemWishlist { Categoria = "Fuente", Nombre = "Fuente 750W 80+ Gold", PrecioEstimado = 100m, Prioridad = "🟠 Alta" });
                proyPC.Items.Add(new ItemWishlist { Categoria = "Gabinete / Cooler", Nombre = "Gabinete + Liquida 240mm", PrecioEstimado = 120m, Prioridad = "🟡 Media" });
                proyPC.Items.Add(new ItemWishlist { Categoria = "Monitor", Nombre = "Monitor 27\" 1440p 165Hz", PrecioEstimado = 230m, Prioridad = "🟡 Media" });

                _listaProyectos.Add(proyPC);
                CmbProyectos.SelectedItem = proyPC;
            }

            LblEstadoWishlist.Text = "Plantilla PC Gaming Cargada";
            LblEstadoWishlist.Foreground = System.Windows.Media.Brushes.DarkViolet;
        }

        private void BtnPlantillaMoto_Click(object sender, RoutedEventArgs e)
        {
            var existente = _listaProyectos.FirstOrDefault(p => p.Nombre.Contains("Equipamiento Moto"));
            if (existente != null)
            {
                CmbProyectos.SelectedItem = existente;
            }
            else
            {
                var proyMoto = new ProyectoWishlist
                {
                    Nombre = "🏍️ Equipamiento Moto",
                    Icono = "🏍️",
                    Descripcion = "Casco, botas, chaqueta y equipo"
                };
                proyMoto.Items.Add(new ItemWishlist { Categoria = "Seguridad", Nombre = "Casco Certificado (ECE 22.06 / DOT)", PrecioEstimado = 250m, Prioridad = "🔴 Urgente" });
                proyMoto.Items.Add(new ItemWishlist { Categoria = "Calzado", Nombre = "Botas de Protección para Moto", PrecioEstimado = 160m, Prioridad = "🔴 Urgente" });
                proyMoto.Items.Add(new ItemWishlist { Categoria = "Protección", Nombre = "Chaqueta con Protecciones", PrecioEstimado = 180m, Prioridad = "🟠 Alta" });
                proyMoto.Items.Add(new ItemWishlist { Categoria = "Accesorios", Nombre = "Guantes Reforzados Kevlar/Cuero", PrecioEstimado = 55m, Prioridad = "🟠 Alta" });
                proyMoto.Items.Add(new ItemWishlist { Categoria = "Tecnología", Nombre = "Intercomunicador Bluetooth", PrecioEstimado = 90m, Prioridad = "🟡 Media" });
                proyMoto.Items.Add(new ItemWishlist { Categoria = "Mantenimiento", Nombre = "Impermeable de Lluvia", PrecioEstimado = 45m, Prioridad = "🟡 Media" });

                _listaProyectos.Add(proyMoto);
                CmbProyectos.SelectedItem = proyMoto;
            }

            LblEstadoWishlist.Text = "Plantilla Equipamiento Moto Cargada";
            LblEstadoWishlist.Foreground = System.Windows.Media.Brushes.DarkGreen;
        }

        private void BtnPlantillaCuarto_Click(object sender, RoutedEventArgs e)
        {
            var existente = _listaProyectos.FirstOrDefault(p => p.Nombre.Contains("Cuarto PC"));
            if (existente != null)
            {
                CmbProyectos.SelectedItem = existente;
            }
            else
            {
                var proy = new ProyectoWishlist
                {
                    Nombre = "🏗️ Construcción Cuarto PC",
                    Icono = "🏗️",
                    Descripcion = "Materiales y adecuación de pequeño cuarto para espacio personal y PC"
                };
                proy.Items.Add(new ItemWishlist { Categoria = "Materiales", Nombre = "Paneles de Gypsum / Paredes / Bloques", PrecioEstimado = 350m, Prioridad = "🔴 Urgente" });
                proy.Items.Add(new ItemWishlist { Categoria = "Electricidad", Nombre = "Cableado Eléctrico & Tomacorrientes dedicados", PrecioEstimado = 120m, Prioridad = "🔴 Urgente" });
                proy.Items.Add(new ItemWishlist { Categoria = "Mano de Obra", Nombre = "Instalación / Trabajo de Construcción", PrecioEstimado = 300m, Prioridad = "🟠 Alta" });
                proy.Items.Add(new ItemWishlist { Categoria = "Acabados", Nombre = "Pintura e Iluminación LED", PrecioEstimado = 80m, Prioridad = "🟡 Media" });

                _listaProyectos.Add(proy);
                CmbProyectos.SelectedItem = proy;
            }

            LblEstadoWishlist.Text = "Plantilla Cuarto PC Cargada";
            LblEstadoWishlist.Foreground = System.Windows.Media.Brushes.DodgerBlue;
        }

        private void BtnPlantillaMuebles_Click(object sender, RoutedEventArgs e)
        {
            var existente = _listaProyectos.FirstOrDefault(p => p.Nombre.Contains("Muebles & Electrodomésticos"));
            if (existente != null)
            {
                CmbProyectos.SelectedItem = existente;
            }
            else
            {
                var proy = new ProyectoWishlist
                {
                    Nombre = "🛋️ Muebles & Electrodomésticos",
                    Icono = "🛋️",
                    Descripcion = "Cama Queen, Estufa, Aire, TV, Sillón"
                };
                proy.Items.Add(new ItemWishlist { Categoria = "Dormitorio", Nombre = "🛏️ Cama Grande Queen Size", PrecioEstimado = 350m, Prioridad = "🔴 Urgente" });
                proy.Items.Add(new ItemWishlist { Categoria = "Cocina", Nombre = "🍳 Estufa de Cocina", PrecioEstimado = 220m, Prioridad = "🔴 Urgente" });
                proy.Items.Add(new ItemWishlist { Categoria = "Climatización", Nombre = "❄️ Aire Acondicionado (Inverter)", PrecioEstimado = 350m, Prioridad = "🟠 Alta" });
                proy.Items.Add(new ItemWishlist { Categoria = "Entretenimiento", Nombre = "📺 Televisor Smart TV", PrecioEstimado = 280m, Prioridad = "🟡 Media" });
                proy.Items.Add(new ItemWishlist { Categoria = "Sala", Nombre = "🛋️ Sillón Pequeño / Compacto (Económico/Inflable)", PrecioEstimado = 60m, Prioridad = "🟢 Opcional" });

                _listaProyectos.Add(proy);
                CmbProyectos.SelectedItem = proy;
            }

            LblEstadoWishlist.Text = "Plantilla Muebles Cargada";
            LblEstadoWishlist.Foreground = System.Windows.Media.Brushes.DeepPink;
        }

        private void BtnPlantillaSeguridad_Click(object sender, RoutedEventArgs e)
        {
            var existente = _listaProyectos.FirstOrDefault(p => p.Nombre.Contains("Seguridad Casa"));
            if (existente != null)
            {
                CmbProyectos.SelectedItem = existente;
            }
            else
            {
                var proy = new ProyectoWishlist
                {
                    Nombre = "🛡️ Seguridad Casa",
                    Icono = "🛡️",
                    Descripcion = "Puerta blindada, verjas ventanas francesas y cámaras exteriores"
                };
                proy.Items.Add(new ItemWishlist { Categoria = "Puerta Principal", Nombre = "🚪 Puerta Blindada de Seguridad", PrecioEstimado = 225m, Prioridad = "🔴 Urgente" });
                proy.Items.Add(new ItemWishlist { Categoria = "Ventanas", Nombre = "🪟 Verjas para Ventanas Francesas", PrecioEstimado = 350m, Prioridad = "🔴 Urgente" });
                proy.Items.Add(new ItemWishlist { Categoria = "Cámaras", Nombre = "📹 2 Cámaras de Seguridad Exteriores (Instalación Propia)", PrecioEstimado = 70m, Prioridad = "🟠 Alta" });
                proy.Items.Add(new ItemWishlist { Categoria = "Instalación", Nombre = "🛠️ Mano de Obra (Instalación Puerta y Verjas)", PrecioEstimado = 150m, Prioridad = "🟠 Alta" });

                _listaProyectos.Add(proy);
                CmbProyectos.SelectedItem = proy;
            }

            LblEstadoWishlist.Text = "Plantilla Seguridad Casa Cargada";
            LblEstadoWishlist.Foreground = System.Windows.Media.Brushes.DarkOrange;
        }

        private void BtnAgregarItemWishlist_Click(object sender, RoutedEventArgs e)
        {
            if (_proyectoSeleccionado == null)
            {
                MessageBox.Show("Primero selecciona o crea un proyecto activo.", "Atención", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtItemNombre.Text))
            {
                MessageBox.Show("Ingresa el nombre o modelo del artículo.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal.TryParse(TxtItemPrecioEst.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal est);
            decimal.TryParse(TxtItemPrecioReal.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal real);

            var prioItem = CmbItemPrioridad.SelectedItem as ComboBoxItem;
            string prioStr = prioItem?.Content?.ToString() ?? "🟡 Media";

            var nuevoItem = new ItemWishlist
            {
                Categoria = string.IsNullOrWhiteSpace(TxtItemCategoria.Text) ? "General" : TxtItemCategoria.Text.Trim(),
                Nombre = TxtItemNombre.Text.Trim(),
                PrecioEstimado = est,
                PrecioReal = real,
                Prioridad = prioStr,
                Estado = real > 0 ? "✅ Comprado" : "⏳ Pendiente"
            };

            nuevoItem.PropertyChanged += Item_PropertyChanged;
            _proyectoSeleccionado.Items.Add(nuevoItem);

            TxtItemCategoria.Clear();
            TxtItemNombre.Clear();
            TxtItemPrecioEst.Clear();
            TxtItemPrecioReal.Clear();

            ActualizarUIWishlist();
            LblEstadoWishlist.Text = $"Item '{nuevoItem.Nombre}' agregado";
            LblEstadoWishlist.Foreground = System.Windows.Media.Brushes.DarkGreen;
        }

        private void BtnEliminarItemWishlist_Click(object sender, RoutedEventArgs e)
        {
            if (_proyectoSeleccionado == null) return;
            if (sender is Button btn && btn.Tag is ItemWishlist item)
            {
                item.PropertyChanged -= Item_PropertyChanged;
                _proyectoSeleccionado.Items.Remove(item);
                ActualizarUIWishlist();
            }
        }

        private void BtnGuardarProyectos_Click(object sender, RoutedEventArgs e)
        {
            GuardarProyectos();
        }

        private void GuardarProyectos()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_rutaProyectos)!);
                string json = System.Text.Json.JsonSerializer.Serialize(_listaProyectos, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(_rutaProyectos, json, System.Text.Encoding.UTF8);
                LblEstadoWishlist.Text = "Proyectos guardados correctamente";
                LblEstadoWishlist.Foreground = System.Windows.Media.Brushes.DarkGreen;
                MessageBox.Show("Proyectos y Wishlists guardados exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar proyectos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region MÓDULO MOTO (ROYAL ENFIELD CONTINENTAL GT)
        private void CargarDatosMoto()
        {
            _datosMoto = _repoMoto.Cargar();
            TxtMotoKmActual.Text = _datosMoto.KilometrajeActual.ToString();

            DgMotoModificaciones.ItemsSource = _datosMoto.Modificaciones;
            DgMotoServicios.ItemsSource = _datosMoto.Servicios;

            SuscribirItemsMoto();
            ActualizarUIMoto();
        }

        private void SuscribirItemsMoto()
        {
            if (_datosMoto == null) return;

            foreach (var mod in _datosMoto.Modificaciones)
            {
                mod.PropertyChanged -= ItemMoto_PropertyChanged;
                mod.PropertyChanged += ItemMoto_PropertyChanged;
            }

            foreach (var srv in _datosMoto.Servicios)
            {
                srv.PropertyChanged -= ItemMoto_PropertyChanged;
                srv.PropertyChanged += ItemMoto_PropertyChanged;
            }
        }

        private void ItemMoto_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ActualizarUIMoto();
        }

        private void ActualizarUIMoto()
        {
            if (_datosMoto == null) return;
            _datosMoto.NotificarCalculos();

            LblMotoTotalModCompradas.Text = $"{_datosMoto.TotalCompradoModificaciones:C2}";
            LblMotoTotalModPendientes.Text = $"{_datosMoto.TotalPendienteModificaciones:C2}";
            LblMotoServiciosProximos.Text = $"{_datosMoto.CantidadServiciosPendientes} pendientes";
        }

        private void BtnActualizarKm_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtMotoKmActual.Text.Replace(".", "").Replace(",", "").Trim(), out int km) && km >= 0)
            {
                _datosMoto.KilometrajeActual = km;
                ActualizarUIMoto();
                LblEstadoMotoModulo.Text = $"Odómetro actualizado a {km:N0} km";
                LblEstadoMotoModulo.Foreground = System.Windows.Media.Brushes.Blue;
            }
            else
            {
                MessageBox.Show("Ingresa un kilometraje válido (número entero positivo).", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnAgregarModificacion_Click(object sender, RoutedEventArgs e)
        {
            string nombre = TxtModNombre.Text.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show("Ingresa el nombre de la pieza o modificación.", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal.TryParse(TxtModPrecioEst.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal est);
            decimal.TryParse(TxtModPrecioReal.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal real);

            string cat = (CmbModCategoria.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Estética";
            string prio = (CmbModPrioridad.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "🟠 Media";

            var nuevaMod = new MotoModificacion
            {
                Nombre = nombre,
                Categoria = cat,
                PrecioEstimado = est,
                PrecioReal = real,
                Prioridad = prio,
                Estado = real > 0 ? "✅ Instalado" : "⏳ Deseado"
            };

            nuevaMod.PropertyChanged += ItemMoto_PropertyChanged;
            _datosMoto.Modificaciones.Add(nuevaMod);

            TxtModNombre.Clear();
            TxtModPrecioEst.Clear();
            TxtModPrecioReal.Clear();

            ActualizarUIMoto();
            LblEstadoMotoModulo.Text = $"Modificación '{nuevaMod.Nombre}' agregada";
            LblEstadoMotoModulo.Foreground = System.Windows.Media.Brushes.DarkGreen;
        }

        private void BtnEliminarModificacion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MotoModificacion mod)
            {
                mod.PropertyChanged -= ItemMoto_PropertyChanged;
                _datosMoto.Modificaciones.Remove(mod);
                ActualizarUIMoto();
            }
        }

        private void BtnAgregarServicio_Click(object sender, RoutedEventArgs e)
        {
            string concepto = TxtServicioConcepto.Text.Trim();
            if (string.IsNullOrWhiteSpace(concepto))
            {
                MessageBox.Show("Ingresa el concepto del servicio o pieza de desgaste.", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tipo = (CmbServicioTipo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "⚙️ Pieza de Desgaste";
            int.TryParse(TxtServicioKm.Text, out int kmSrv);
            if (kmSrv <= 0) kmSrv = _datosMoto.KilometrajeActual;
            int.TryParse(TxtServicioProxKm.Text, out int proxKm);
            if (proxKm <= 0) proxKm = kmSrv + 5000;

            var nuevoSrv = new ServicioMoto
            {
                Concepto = concepto,
                Tipo = tipo,
                KilometrajeServicio = kmSrv,
                ProximoKilometraje = proxKm,
                Realizado = false,
                FechaServicio = DateTime.Now
            };

            nuevoSrv.PropertyChanged += ItemMoto_PropertyChanged;
            _datosMoto.Servicios.Add(nuevoSrv);

            TxtServicioConcepto.Clear();
            TxtServicioKm.Clear();
            TxtServicioProxKm.Clear();

            ActualizarUIMoto();
            LblEstadoMotoModulo.Text = $"Servicio '{nuevoSrv.Concepto}' registrado";
            LblEstadoMotoModulo.Foreground = System.Windows.Media.Brushes.DarkGreen;
        }

        private void BtnEliminarServicio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ServicioMoto srv)
            {
                srv.PropertyChanged -= ItemMoto_PropertyChanged;
                _datosMoto.Servicios.Remove(srv);
                ActualizarUIMoto();
            }
        }

        private void BtnCargarPlantillaMotoCompleta_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Deseas reiniciar la lista con la plantilla completa recomendada para la Royal Enfield Continental GT?",
                "Cargar Plantilla RE Continental GT", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _datosMoto = _repoMoto.CrearPlantillaPorDefecto();
                TxtMotoKmActual.Text = _datosMoto.KilometrajeActual.ToString();

                DgMotoModificaciones.ItemsSource = _datosMoto.Modificaciones;
                DgMotoServicios.ItemsSource = _datosMoto.Servicios;

                SuscribirItemsMoto();
                ActualizarUIMoto();

                _repoMoto.Guardar(_datosMoto);
                LblEstadoMotoModulo.Text = "Plantilla Continental GT cargada exitosamente";
                LblEstadoMotoModulo.Foreground = System.Windows.Media.Brushes.DarkViolet;
            }
        }

        private void BtnGuardarMoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(TxtMotoKmActual.Text.Replace(".", "").Replace(",", "").Trim(), out int km))
                {
                    _datosMoto.KilometrajeActual = km;
                }

                _repoMoto.Guardar(_datosMoto);
                LblEstadoMotoModulo.Text = "Datos de la moto guardados correctamente";
                LblEstadoMotoModulo.Foreground = System.Windows.Media.Brushes.DarkGreen;
                MessageBox.Show("Datos y mantenimientos de la moto guardados exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar datos de moto: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
    public static class StringExtensions
    {
        public static int ParseIntSafe(this string s)
        {
            return int.TryParse(s, out int r) ? r : 0;
        }
    }
}