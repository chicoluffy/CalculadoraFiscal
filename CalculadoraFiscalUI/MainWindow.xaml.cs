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
        private readonly ObservableCollection<Gasto> _listaGastos = new();

        public decimal SalarioFinalCalculado { get; private set; }
        private PeriodoQuincenal _periodoActual = new();

        public MainWindow()
        {
            InitializeComponent();
            _repo = new RepositorioQuincenas(AppContext.BaseDirectory);
            DgGastos.ItemsSource = _listaGastos;
            InicializarFiltroMes();
            InicializarPeriodos();
            CargarPeriodoPorDefecto();
            CargarMetaAhorro();
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
            CargarGastosDelPerido();
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

        private void ActualizarSalarioDisponibleEnGastos()
        {
            LblSalarioDisponible.Text = SalarioFinalCalculado.ToString("C2");
            ActualizarResumenGastos();
        }
        #endregion

        #region Pestaña Gastos
        private void BtnAgregarGasto_Click(object sender, RoutedEventArgs e)
        {
            string nombre = TxtNombreGasto.Text.Trim();
            if (string.IsNullOrWhiteSpace(nombre)) { MessageBox.Show("Ingresa un nombre", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(TxtMontoGasto.Text.Replace("$", "").Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal monto) || monto <= 0)
            { MessageBox.Show("Monto inválido > 0", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            int mes = int.TryParse(((ComboBoxItem)CmbFiltroMes.SelectedItem)?.Tag?.ToString(), out int m) ? m : 1;

            _listaGastos.Add(new Gasto { Nombre = nombre, Monto = monto, Mes = mes });
            TxtNombreGasto.Clear(); TxtMontoGasto.Clear(); TxtNombreGasto.Focus();
            CmbFiltroMes_SelectionChanged(this, new SelectionChangedEventArgs(null, null, null)); // Refresca filtro
            MarcarComoModificado();
        }

        private void BtnEliminarGasto_Click(object sender,RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Gasto gasto)
            {
                _listaGastos.Remove(gasto);
                ActualizarResumenGastos();
                MarcarComoModificado();
            }
        }
        private void BtnLimpiarGastos_Click(object sender,RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Eliminar todos los gastos de esta quincena?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _listaGastos.Clear();
                ActualizarResumenGastos();
                MarcarComoModificado();
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

        private void CargarGastosDelPerido()
        {
            var datos = _repo.Cargar(_periodoActual);
            if (datos == null)
            {
                _listaGastos.Clear();
                ActualizarResumenGastos();
                LblEstadoGuardado.Text = "Sin datos";
                LblEstadoGuardado.Foreground = Brushes.Gray; 
                return;

            }

            SalarioFinalCalculado = datos.SalarioFinal;
            _listaGastos.Clear();
            foreach (var g in datos.Gastos)
                _listaGastos.Add(g);

            ActualizarSalarioDisponibleEnGastos();
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

        #region filtro por mes
        private void InicializarFiltroMes()
        {
            string[] nombresMeses = { "Todos", "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
            for (int i = 1; i <= 12; i++)
                CmbFiltroMes.Items.Add(new ComboBoxItem { Content = nombresMeses[i], Tag = i });
        }

        private void CmbFiltroMes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = CmbFiltroMes.SelectedItem as ComboBoxItem;
            if (selected == null) return;

            if (selected.Tag is int mes && mes > 0)
                DgGastos.ItemsSource = _listaGastos.Where(g => g.Mes == mes).ToList();
            else
                DgGastos.ItemsSource = _listaGastos;

            ActualizarResumenGastos();
        }
        #endregion

        #region Ahorro rpg
        private MetaAhorro _metaActual = new();
        private readonly string _rutaMeta = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "meta_ahorro.json");


        private void BtnFijarMeta_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(TxtMetaMonto.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal obj) || obj <= 0)
            { MessageBox.Show("Define un objetivo válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(TxtAporteMeta.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal aporte) || aporte <= 0)
            { MessageBox.Show("Define un aporte quincenal válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            _metaActual.Nombre = string.IsNullOrWhiteSpace(TxtNombreMeta.Text) ? "Mi Meta" : TxtNombreMeta.Text;
            _metaActual.MontoObjetivo = obj;
            _metaActual.AportePorQuincena = aporte;
            ActualizarUIAhorro();
            LblFeedback.Text = "Meta configurada. ¡A subir de nivel!";
        }

        private void BtnAgregarAhorro_Click(object sender, RoutedEventArgs e)
        {
            if (_metaActual.MontoObjetivo == 0) { MessageBox.Show("Primero fija una meta", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(TxtDepositoAhorro.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal monto) || monto <= 0)
            { MessageBox.Show("Monto inválido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            _metaActual.MontoActual += monto;
            _metaActual.Historial.Insert(0, new HistorialAhorro
            {
                Fecha = DateTime.Now,
                Anio = (int)CmbAnio.SelectedItem,
                Quincena = (int)((ComboBoxItem)CmbQuincena.SelectedItem).Tag,
                Monto = monto
            });

            TxtDepositoAhorro.Clear();
            ActualizarUIAhorro();

            // Mensajes tipo RPG
            double pct = (double)_metaActual.MontoActual / (double)_metaActual.MontoObjetivo * 100;
            LblFeedback.Text = pct >= 100 ? "🏆 ¡META COMPLETADA! Eres legendario." : $"✨ +{monto.ToString("C0")} XP acumulados.";
        }

        private void ActualizarUIAhorro()
        {
            double pct = _metaActual.MontoObjetivo > 0
                ? Math.Min((double)_metaActual.MontoActual / (double)_metaActual.MontoObjetivo * 100, 100)
                : 0;
            PgbProgreso.Value = pct;
            LblPorcentaje.Text = $"{pct:F1}%";
            LblActual.Text = $" {_metaActual.MontoActual.ToString("C2")}";
            LblNivel.Text = ObtenerNivelRPG(pct);

            // Cálculo ETA
            decimal restante = _metaActual.MontoObjetivo - _metaActual.MontoActual;
            if (restante <= 0)
            {
                LblEta.Text = " 🎉 ¡Meta alcanzada!";
                LblEta.Foreground = Brushes.Gold;
                return;
            }

            decimal promedioReal = 0m;
            if (_metaActual.Historial.Count > 0)
            {
                promedioReal = _metaActual.Historial.Average(h => h.Monto);
            }
            decimal ritmoProyeccion = promedioReal > 0 ? promedioReal : _metaActual.AportePorQuincena;

            if (ritmoProyeccion <= 0)
            {
                LblEta.Text = " ⏸️ Define tu aporte para proyectar";
                LblEta.Foreground = Brushes.Gray;
                return;
            }

            double quincenasRestantes = Math.Ceiling((double)restante / (double)ritmoProyeccion);
            double mesesRestantes = quincenasRestantes / 2.0;
            double aniosRestantes = mesesRestantes / 12.0;

            // 🔹 Formato inteligente según la escala
            if (quincenasRestantes <= 2)
                LblEta.Text = $" ✨ ¡Casi! ~{quincenasRestantes:F0} Q";
            else if (mesesRestantes < 12)
                LblEta.Text = $" ~{mesesRestantes:F1} meses";
            else
                LblEta.Text = $" ~{aniosRestantes:F1} años ({mesesRestantes:F0} meses)";

            // 🔹 Tooltip informativo (opcional)
            LblEta.ToolTip = promedioReal > 0
                ? $"Basado en tu promedio real: {promedioReal.ToString("C2")}/quincena"
                : $"Basado en tu plan: {_metaActual.AportePorQuincena.ToString("C2")}/quincena";

            LblEta.Foreground = Brushes.LightYellow;

            // === Historial ===
            DgHistorialAhorro.ItemsSource = _metaActual.Historial.OrderByDescending(h => h.Fecha).ToList();
        }

        private string ObtenerNivelRPG(double pct) => pct switch
        {
            >= 100 => "🏆 Nivel MAX: Leyenda",
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
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_rutaMeta)!);
                string json = System.Text.Json.JsonSerializer.Serialize(_metaActual, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(_rutaMeta, json, System.Text.Encoding.UTF8);
                MessageBox.Show("Progreso de ahorro guardado", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }


        private void CargarMetaAhorro()
        {
            if (!System.IO.File.Exists(_rutaMeta)) return;
            try
            {
                string json = System.IO.File.ReadAllText(_rutaMeta, System.Text.Encoding.UTF8);
                var meta = System.Text.Json.JsonSerializer.Deserialize<MetaAhorro>(json);
                if (meta != null)
                {
                    _metaActual = meta;
                    TxtNombreMeta.Text = meta.Nombre;
                    TxtMetaMonto.Text = meta.MontoObjetivo.ToString();
                    TxtAporteMeta.Text = meta.AportePorQuincena.ToString();
                    ActualizarUIAhorro();
                }
            }
            catch { /* Ignorar si está corrupto, se crea uno nuevo */ }
        }
        #endregion

    }
}