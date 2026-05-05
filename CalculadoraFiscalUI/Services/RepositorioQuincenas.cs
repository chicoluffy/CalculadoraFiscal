using CalculadoraFiscalUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CalculadoraFiscalUI.Services
{
    public class RepositorioQuincenas
    {
        private readonly string _carpetaDatos;
        public RepositorioQuincenas(string rutaBase)
        {
            _carpetaDatos = Path.Combine(rutaBase,"data", "quincenas");
            Directory.CreateDirectory(_carpetaDatos);
        }


        public string ObtenerRuta(PeriodoQuincenal periodo) => 
            Path.Combine(_carpetaDatos, $"quincena_{periodo.Anio}_{periodo.Quincena}.json");

        public void Guardar(DatosQuincena datos)
        {
            var opciones = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            string json = JsonSerializer.Serialize(datos, opciones);
            File.WriteAllText(ObtenerRuta(datos.Periodo), json, Encoding.UTF8);
        }

        public DatosQuincena? Cargar (PeriodoQuincenal periodo)
        {
            string ruta = ObtenerRuta(periodo);
            if (!File.Exists(ruta)) return null;
            string json = File.ReadAllText(ruta, Encoding.UTF8);
            return JsonSerializer.Deserialize<DatosQuincena>(json);
        }

        public bool Existe (PeriodoQuincenal periodo) => File.Exists(ObtenerRuta(periodo));
        public void Eliminar (PeriodoQuincenal periodo)
        {
            if (Existe(periodo)) File.Delete(ObtenerRuta(periodo));
        }


    }
}
