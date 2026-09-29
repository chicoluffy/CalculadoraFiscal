using CalculadoraFiscalUI.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CalculadoraFiscalUI.Services
{
    public class RepositorioMoto
    {
        private readonly string _rutaArchivo;

        public RepositorioMoto(string rutaBase)
        {
            string carpeta = Path.Combine(rutaBase, "data");
            Directory.CreateDirectory(carpeta);
            _rutaArchivo = Path.Combine(carpeta, "moto_continental_gt.json");
        }

        public void Guardar(DatosMoto datos)
        {
            var opciones = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string json = JsonSerializer.Serialize(datos, opciones);
            File.WriteAllText(_rutaArchivo, json, Encoding.UTF8);
        }

        public DatosMoto Cargar()
        {
            if (!File.Exists(_rutaArchivo))
            {
                var porDefecto = CrearPlantillaPorDefecto();
                Guardar(porDefecto);
                return porDefecto;
            }

            try
            {
                string json = File.ReadAllText(_rutaArchivo, Encoding.UTF8);
                var datos = JsonSerializer.Deserialize<DatosMoto>(json);
                if (datos == null) return CrearPlantillaPorDefecto();

                // Asegurar que 'Cambio de Cadena' esté en la lista si no existía antes
                if (!datos.Servicios.Any(s => s.Concepto.Contains("Cadena") && s.Tipo.Contains("Pieza")))
                {
                    datos.Servicios.Add(new ServicioMoto
                    {
                        Concepto = "Cambio de Cadena / Kit de Arrastre Completo (Cadena, Corona y Piñón)",
                        Tipo = "⚙️ Pieza de Desgaste",
                        KilometrajeServicio = 15000,
                        ProximoKilometraje = 20000,
                        FechaServicio = DateTime.Now.AddMonths(6),
                        Realizado = false,
                        Taller = "Taller Especializado / DIY",
                        Notas = "Pieza de desgaste. Reemplazo de cadena paso 520 y piñón/corona a los 15,000 - 20,000 km"
                    });
                    Guardar(datos);
                }

                return datos;
            }
            catch
            {
                return CrearPlantillaPorDefecto();
            }
        }

        public DatosMoto CrearPlantillaPorDefecto()
        {
            var datos = new DatosMoto
            {
                Modelo = "Royal Enfield Continental GT 650",
                KilometrajeActual = 5000,
                Anno = 2023
            };

            // 1. Modificaciones / Wishlist ("Cosas que quiero cambiar")
            datos.Modificaciones.Add(new MotoModificacion
            {
                Nombre = "Espejos Bar-End de Aluminio CNC",
                Categoria = "Estética",
                PrecioEstimado = 65.00m,
                PrecioReal = 65.00m,
                Prioridad = "🔴 Alta",
                Estado = "✅ Instalado",
                Notas = "Mejora la vista café racer y reduce vibración respecto a los originales"
            });

            datos.Modificaciones.Add(new MotoModificacion
            {
                Nombre = "Escape Deportivo AEW 201 / Red Rooster",
                Categoria = "Escape/Rendimiento",
                PrecioEstimado = 380.00m,
                PrecioReal = 0m,
                Prioridad = "🔴 Alta",
                Estado = "⏳ Deseado",
                Notas = "Sonido más profundo twin y reducción significativa de peso"
            });

            datos.Modificaciones.Add(new MotoModificacion
            {
                Nombre = "Asiento Single Seat Café Racer (Monoplaza)",
                Categoria = "Ergonomía/Asiento",
                PrecioEstimado = 120.00m,
                PrecioReal = 0m,
                Prioridad = "🟠 Alta",
                Estado = "⏳ Deseado",
                Notas = "Estilo clásico monoplaza para Continental GT"
            });

            datos.Modificaciones.Add(new MotoModificacion
            {
                Nombre = "Tail Tidy + Guiños LED Compactos",
                Categoria = "Iluminación",
                PrecioEstimado = 85.00m,
                PrecioReal = 0m,
                Prioridad = "🟡 Media",
                Estado = "⏳ Deseado",
                Notas = "Elimina el guardabarros trasero abultado original"
            });

            datos.Modificaciones.Add(new MotoModificacion
            {
                Nombre = "Sliders / Defensa de Motor Royal Enfield",
                Categoria = "Protección",
                PrecioEstimado = 95.00m,
                PrecioReal = 95.00m,
                Prioridad = "🔴 Alta",
                Estado = "✅ Instalado",
                Notas = "Protección esencial para caídas tontas o raspones"
            });

            datos.Modificaciones.Add(new MotoModificacion
            {
                Nombre = "Filtro de Aire de Alto Flujo DNA / K&N",
                Categoria = "Rendimiento",
                PrecioEstimado = 70.00m,
                PrecioReal = 0m,
                Prioridad = "🟡 Media",
                Estado = "⏳ Deseado",
                Notas = "Aumenta entrada de aire limpia y lavable"
            });

            datos.Modificaciones.Add(new MotoModificacion
            {
                Nombre = "Manetas Ajustables de Freno y Cloche CNC",
                Categoria = "Ergonomía",
                PrecioEstimado = 45.00m,
                PrecioReal = 0m,
                Prioridad = "🟢 Opcional",
                Estado = "⏳ Deseado",
                Notas = "Permite regular la distancia al manillar"
            });

            // 2. Servicios & Piezas de Desgaste
            datos.Servicios.Add(new ServicioMoto
            {
                Concepto = "Cambio de Aceite 15W-50 Semi-Sintético + Filtro de Aceite",
                Tipo = "🛢️ Mantenimiento Rutinario",
                KilometrajeServicio = 5000,
                ProximoKilometraje = 10000,
                FechaServicio = DateTime.Now.AddMonths(-1),
                CostoRefacciones = 45.00m,
                CostoManoObra = 15.00m,
                Realizado = true,
                Taller = "DIY / Taller Especializado",
                Notas = "Aceite Motul 5100 15W50 y filtro original Royal Enfield"
            });

            datos.Servicios.Add(new ServicioMoto
            {
                Concepto = "Reemplazo de Pastillas de Freno ByBre Delanteras",
                Tipo = "⚙️ Pieza de Desgaste",
                KilometrajeServicio = 6000,
                ProximoKilometraje = 12000,
                FechaServicio = DateTime.Now.AddDays(15),
                CostoRefacciones = 35.00m,
                CostoManoObra = 10.00m,
                Realizado = false,
                Taller = "Taller Local",
                Notas = "Pieza de desgaste. Verificar grosor del compuesto antes de viaje"
            });

            datos.Servicios.Add(new ServicioMoto
            {
                Concepto = "Reemplazo de Pastillas de Freno ByBre Traseras",
                Tipo = "⚙️ Pieza de Desgaste",
                KilometrajeServicio = 6000,
                ProximoKilometraje = 12000,
                FechaServicio = DateTime.Now.AddDays(15),
                CostoRefacciones = 28.00m,
                CostoManoObra = 10.00m,
                Realizado = false,
                Taller = "Taller Local",
                Notas = "Pieza de desgaste"
            });

            datos.Servicios.Add(new ServicioMoto
            {
                Concepto = "Retenes y Sellos de Horquilla Delantera (Barras)",
                Tipo = "⚙️ Pieza de Desgaste",
                KilometrajeServicio = 8000,
                ProximoKilometraje = 16000,
                FechaServicio = DateTime.Now.AddMonths(2),
                CostoRefacciones = 32.00m,
                CostoManoObra = 40.00m,
                Realizado = false,
                Taller = "Taller Especializado",
                Notas = "Cambio de sellos de aceite y antipolvos por fuga o desgaste"
            });

            datos.Servicios.Add(new ServicioMoto
            {
                Concepto = "Inspección y Calibración de Válvulas (Punterías)",
                Tipo = "🛢️ Mantenimiento Rutinario",
                KilometrajeServicio = 5000,
                ProximoKilometraje = 10000,
                FechaServicio = DateTime.Now.AddMonths(-1),
                CostoRefacciones = 10.00m,
                CostoManoObra = 35.00m,
                Realizado = true,
                Taller = "Taller Oficial / Especializado",
                Notas = "Ajuste de holgura en frío según manual (Admisión 0.08mm, Escape 0.18mm)"
            });

            datos.Servicios.Add(new ServicioMoto
            {
                Concepto = "Limpieza, Tensión y Lubricación de Cadena (Kit de Arrastre)",
                Tipo = "🛢️ Mantenimiento Rutinario",
                KilometrajeServicio = 4500,
                ProximoKilometraje = 5500,
                FechaServicio = DateTime.Now.AddDays(-7),
                CostoRefacciones = 8.00m,
                CostoManoObra = 0m,
                Realizado = true,
                Taller = "DIY",
                Notas = "Holgura correcta entre 25-30 mm"
            });

            datos.Servicios.Add(new ServicioMoto
            {
                Concepto = "Reemplazo de Bujías NGK CR8E",
                Tipo = "⚙️ Pieza de Desgaste",
                KilometrajeServicio = 10000,
                ProximoKilometraje = 20000,
                FechaServicio = DateTime.Now.AddMonths(4),
                CostoRefacciones = 18.00m,
                CostoManoObra = 5.00m,
                Realizado = false,
                Taller = "DIY",
                Notas = "Pieza de desgaste. Calibrar electrodo a 0.7-0.8 mm"
            });

            datos.Servicios.Add(new ServicioMoto
            {
                Concepto = "Cambio de Cadena / Kit de Arrastre Completo (Cadena, Corona y Piñón)",
                Tipo = "⚙️ Pieza de Desgaste",
                KilometrajeServicio = 15000,
                ProximoKilometraje = 20000,
                FechaServicio = DateTime.Now.AddMonths(6),
                CostoRefacciones = 0m,
                CostoManoObra = 0m,
                Realizado = false,
                Taller = "Taller Especializado / DIY",
                Notas = "Pieza de desgaste. Reemplazo de cadena paso 520 y piñón/corona a los 15,000 - 20,000 km"
            });

            return datos;
        }
    }
}
