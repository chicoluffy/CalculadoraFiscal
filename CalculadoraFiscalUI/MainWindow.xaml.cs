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
            InicializarPeriodos();
            CargarPeriodoPorDefecto();
            CargarMetaAhorro();
            CargarProyectoAuto();
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

            // 🔢 Cálculos centralizados
            decimal totalGastos = periodoActual.Sum(g => g.Monto);
            decimal disponibleReal = SalarioFinalCalculado - _metaActual.AportePorQuincena - totalGastos;
            decimal pendiente = SalarioFinalCalculado - totalGastos;

            // 🖥️ UI Superior (Disponible + Reserva)
            LblSalarioDisponible.Text = disponibleReal.ToString("C2");
            LblReservaAhorro.Text = _metaActual.AportePorQuincena > 0 ? $"(Reservado: {_metaActual.AportePorQuincena:C2})" : "";
            LblReservaAhorro.Foreground = _metaActual.AportePorQuincena > SalarioFinalCalculado ? Brushes.Red : new SolidColorBrush(Color.FromRgb(99, 102, 241));

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
        private void BtnLimpiarGastos_Click(object sender, RoutedEventArgs e)
        {
            if (CmbMesContexto.SelectedItem == null || CmbQMesContexto.SelectedItem == null) return;

            int mes = int.TryParse(((ComboBoxItem)CmbMesContexto.SelectedItem).Tag?.ToString(), out int m) ? m : 1;
            int q = int.TryParse(((ComboBoxItem)CmbQMesContexto.SelectedItem).Tag?.ToString(), out int qTag) ? qTag : 1;
            string nombreMes = ((ComboBoxItem)CmbMesContexto.SelectedItem).Content?.ToString() ?? "";

            if (MessageBox.Show($"¿Eliminar TODOS los gastos de {nombreMes} ({(q == 1 ? "1ra" : "2da")})?",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // ✅ FIX: ObservableCollection no tiene RemoveAll. UsamosToList() + foreach Remove()
                var paraEliminar = _listaGastos.Where(g => g.Mes == mes && g.QuincenaMes == q).ToList();
                foreach (var gasto in paraEliminar)
                    _listaGastos.Remove(gasto);

                AplicarFiltroGastos();
                MarcarComoModificado();
            }
        }


        private void CmbContexto_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // IsLoaded evita NullReferenceException durante la carga inicial de la ventana
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
                AplicarFiltroGastos(); // ← Cambiado
                LblEstadoGuardado.Text = "Sin datos";
                LblEstadoGuardado.Foreground = Brushes.Gray;
                return;
            }

            SalarioFinalCalculado = datos.SalarioFinal;
            _listaGastos.Clear();
            foreach (var g in datos.Gastos) _listaGastos.Add(g);

            AplicarFiltroGastos(); // ← Cambiado
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
        #region 🚗 Proyecto Auto
        private ProyectoAuto _proyectoAuto = new();
        private readonly string _rutaAuto = System.IO.Path.Combine(AppContext.BaseDirectory, "data", "proyecto_auto.json");

        private void BtnActualizarAuto_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(TxtFondoAuto.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal fondo) || fondo < 0)
            { MessageBox.Show("Fondo actual inválido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(TxtAporteAuto.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal aporte) || aporte < 0)
            { MessageBox.Show("Aporte quincenal inválido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            _proyectoAuto.FondoActual = fondo;
            _proyectoAuto.AporteQuincenal = aporte;
            CalcularEtaAuto();
        }

        private void BtnAgregarItemAuto_Click(object sender, RoutedEventArgs e)
        {
            string item = TxtItemAuto.Text.Trim();
            if (string.IsNullOrWhiteSpace(item)) return;
            if (!decimal.TryParse(TxtCostoItem.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal costo) || costo <= 0)
            { MessageBox.Show("Costo inválido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            _proyectoAuto.Checklist.Add(new ItemChecklistAuto
            {
                Nombre = item,
                CostoEstimado = costo,
                EsPrioridad = ChkPrioridadAuto.IsChecked == true,
                Completado = false
            });

            TxtItemAuto.Clear(); TxtCostoItem.Clear(); ChkPrioridadAuto.IsChecked = false;
            CalcularEtaAuto();
        }

        private void CalcularEtaAuto()
        {
            decimal totalNecesario = _proyectoAuto.Checklist.Where(i => !i.Completado).Sum(i => i.CostoEstimado);
            decimal pendiente = Math.Max(0, totalNecesario - _proyectoAuto.FondoActual);
            LblNecesitaAuto.Text = $" {totalNecesario.ToString("C2")}";

            if (pendiente <= 0) { LblEtaAuto.Text = " 🎉 ¡Fondo cubre todo!"; LblAlertaAuto.Text = ""; return; }
            if (_proyectoAuto.AporteQuincenal <= 0) { LblEtaAuto.Text = " ⏸️ Define aporte"; LblAlertaAuto.Text = ""; return; }

            double quincenas = Math.Ceiling((double)pendiente / (double)_proyectoAuto.AporteQuincenal);
            double meses = quincenas / 2.0;
            LblEtaAuto.Text = $" ~{meses:F1} meses ({quincenas:F0} Q)";

            // 🔗 Validación contra disponible real de la quincena activa
            decimal disponibleActual = decimal.Parse(LblSalarioDisponible.Text.Replace("$", "").Replace(" ", ""));
            if (_proyectoAuto.AporteQuincenal > disponibleActual)
                LblAlertaAuto.Text = "⚠️ Tu aporte supera el disponible de esta quincena";
            else
                LblAlertaAuto.Text = "";
        }

        private void BtnGuardarAuto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_rutaAuto)!);
                string json = System.Text.Json.JsonSerializer.Serialize(_proyectoAuto, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(_rutaAuto, json, System.Text.Encoding.UTF8);
                MessageBox.Show("Proyecto Auto guardado", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void CargarProyectoAuto()
        {
            if (!System.IO.File.Exists(_rutaAuto)) return;
            try
            {
                string json = System.IO.File.ReadAllText(_rutaAuto, System.Text.Encoding.UTF8);
                var data = System.Text.Json.JsonSerializer.Deserialize<ProyectoAuto>(json);
                if (data != null)
                {
                    _proyectoAuto = data;
                    TxtFondoAuto.Text = data.FondoActual.ToString();
                    TxtAporteAuto.Text = data.AporteQuincenal.ToString();
                    DgChecklistAuto.ItemsSource = data.Checklist;
                    CalcularEtaAuto();
                }
            }
            catch { }
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