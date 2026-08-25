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
            if (!decimal.TryParse(TxtAporteQ1.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal q1) || q1 < 0)
            { MessageBox.Show("Define un aporte de 1ra quincena válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(TxtAporteQ2.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal q2) || q2 < 0)
            { MessageBox.Show("Define un aporte de 2da quincena válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(TxtMontoDecimo.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal decimo) || decimo < 0)
            { MessageBox.Show("Define un monto de décimo válido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            _metaActual.Nombre = string.IsNullOrWhiteSpace(TxtNombreMeta.Text) ? "Meta Principal (Moto + Casa)" : TxtNombreMeta.Text;
            _metaActual.MontoObjetivo = obj;
            _metaActual.AporteQ1 = q1;
            _metaActual.AporteQ2 = q2;
            _metaActual.MontoDecimo = decimo;
            _metaActual.AportePorQuincena = (q1 + q2) / 2m;

            ActualizarUIAhorro();
            LblFeedback.Text = "Plan configurado. ¡Proyección a Dic 2027 actualizada!";
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
            }
        }

        private void BtnAgregarAhorro_Click(object sender, RoutedEventArgs e)
        {
            if (_metaActual.MontoObjetivo == 0) { MessageBox.Show("Primero fija una meta u objetivo total", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(TxtDepositoAhorro.Text.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal monto) || monto <= 0)
            { MessageBox.Show("Monto inválido", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var comboTipo = CmbTipoDeposito.SelectedItem as ComboBoxItem;
            string tipoStr = comboTipo?.Tag?.ToString() ?? "Quincenal";

            _metaActual.MontoActual += monto;
            _metaActual.Historial.Insert(0, new HistorialAhorro
            {
                Fecha = DateTime.Now,
                Anio = int.TryParse(CmbAnio.SelectedItem?.ToString(), out int a) ? a : DateTime.Now.Year,
                Quincena = (CmbQuincena.SelectedItem as ComboBoxItem)?.Tag != null && int.TryParse(((ComboBoxItem)CmbQuincena.SelectedItem).Tag.ToString(), out int q) ? q : 1,
                Monto = monto,
                Tipo = tipoStr,
                MetaNombre = _metaActual.Nombre
            });

            TxtDepositoAhorro.Clear();
            ActualizarUIAhorro();

            double pct = (double)_metaActual.MontoActual / (double)_metaActual.MontoObjetivo * 100;
            LblFeedback.Text = pct >= 100 ? "🏆 ¡META COMPLETADA! Eres legendario." : $"✨ +{monto:C0} ({tipoStr}) registrado.";
        }

        private void ActualizarUIAhorro()
        {
            double pct = _metaActual.MontoObjetivo > 0
                ? Math.Min((double)_metaActual.MontoActual / (double)_metaActual.MontoObjetivo * 100, 100)
                : 0;
            PgbProgreso.Value = pct;
            LblPorcentaje.Text = $"{pct:F1}%";
            LblActual.Text = $" {_metaActual.MontoActual:C2}";
            LblNivel.Text = ObtenerNivelRPG(pct);

            // 🚀 Proyección detallada a Diciembre 2027
            DateTime hoy = DateTime.Now;
            int cantQ1 = 0;
            int cantQ2 = 0;
            int cantDecimos = 0;

            int startYear = hoy.Year;
            int startMonth = hoy.Month;
            int startQ = hoy.Day <= 15 ? 1 : 2;

            for (int y = startYear; y <= 2027; y++)
            {
                int mStart = (y == startYear) ? startMonth : 1;
                int mEnd = (y == 2027) ? 12 : 12;
                for (int m = mStart; m <= mEnd; m++)
                {
                    // 1ra Quincena
                    if (!(y == startYear && m == startMonth && startQ > 1))
                    {
                        cantQ1++;
                    }
                    // 2da Quincena
                    cantQ2++;

                    // Décimos (Abril, Agosto, Diciembre)
                    if ((m == 4 || m == 8 || m == 12) && !(y == startYear && m == startMonth && hoy.Day > 15))
                    {
                        cantDecimos++;
                    }
                }
            }

            decimal proyectadoQuincenas = (cantQ1 * _metaActual.AporteQ1) + (cantQ2 * _metaActual.AporteQ2);
            decimal proyectadoDecimos = cantDecimos * _metaActual.MontoDecimo;
            decimal totalProyectado = _metaActual.MontoActual + proyectadoQuincenas + proyectadoDecimos;

            LblProyeccionDic2027.Text = $" {totalProyectado:C2}";

            if (_metaActual.MontoObjetivo > 0)
            {
                decimal dif = totalProyectado - _metaActual.MontoObjetivo;
                if (dif >= 0)
                {
                    LblEstadoMeta2027.Text = $"🚀 Plan en curso: ¡Superas tu meta por {dif:C2} para Dic 2027! ({cantQ1} Q1 de ${_metaActual.AporteQ1:F0}, {cantQ2} Q2 de ${_metaActual.AporteQ2:F0}, {cantDecimos} Décimos de ${_metaActual.MontoDecimo:F0})";
                    LblEstadoMeta2027.Foreground = Brushes.LightGreen;
                }
                else
                {
                    LblEstadoMeta2027.Text = $"⚠️ Te faltarán {Math.Abs(dif):C2} para alcanzar la meta en Dic 2027 ({cantQ1} Q1, {cantQ2} Q2, {cantDecimos} Décimos restantes)";
                    LblEstadoMeta2027.Foreground = Brushes.Orange;
                }
            }
            else
            {
                LblEstadoMeta2027.Text = $"✨ Proyección estimada al 31/12/2027: {totalProyectado:C2}";
                LblEstadoMeta2027.Foreground = Brushes.LightYellow;
            }

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
                MessageBox.Show("Progreso de ahorro guardado correctamente", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Error al guardar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void CargarMetaAhorro()
        {
            if (!System.IO.File.Exists(_rutaMeta))
            {
                // Cargar valores por defecto solicitados por el usuario
                TxtNombreMeta.Text = _metaActual.Nombre;
                TxtAporteQ1.Text = _metaActual.AporteQ1.ToString();
                TxtAporteQ2.Text = _metaActual.AporteQ2.ToString();
                TxtMontoDecimo.Text = _metaActual.MontoDecimo.ToString();
                return;
            }
            try
            {
                string json = System.IO.File.ReadAllText(_rutaMeta, System.Text.Encoding.UTF8);
                var meta = System.Text.Json.JsonSerializer.Deserialize<MetaAhorro>(json);
                if (meta != null)
                {
                    _metaActual = meta;
                    TxtNombreMeta.Text = meta.Nombre;
                    TxtMetaMonto.Text = meta.MontoObjetivo > 0 ? meta.MontoObjetivo.ToString() : "";
                    TxtAporteQ1.Text = meta.AporteQ1 > 0 ? meta.AporteQ1.ToString() : "300";
                    TxtAporteQ2.Text = meta.AporteQ2 > 0 ? meta.AporteQ2.ToString() : "350";
                    TxtMontoDecimo.Text = meta.MontoDecimo > 0 ? meta.MontoDecimo.ToString() : "588";
                    ActualizarUIAhorro();
                }
            }
            catch { /* Ignorar si está corrupto */ }
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