using System.Linq;
using Microsoft.AspNetCore.Mvc;
using QuickTableProyect.Persistencia.Datos;

namespace QuickTableProyect.Interface.Api
{
    [ApiController]
    [Route("api/tarjeta")]
    public class TarjetaApiController : ControllerBase
    {
        private readonly SistemaQuickTableContext _ctx = new();

        // 1. devuelve UIDs pendientes de grabar
        [HttpGet("pendientes")]
        public IActionResult Pendientes()
        {
            var lista = _ctx.TarjetasRC
                            .Where(t => !t.Activa)
                            .Select(t => t.Uid)
                            .ToList();
            return Ok(lista);
        }

        // 2. Raspberry confirma grabación
        [HttpPost("confirmar")]
        public IActionResult Confirmar([FromForm] string uidLeido)
        {
            var t = _ctx.TarjetasRC.FirstOrDefault(x => x.Uid == uidLeido && !x.Activa);
            if (t == null) return NotFound();                       // 404
            t.Activa = true;
            _ctx.SaveChanges();
            return Ok();                                            // 200
        }

        // 3. Endpoint para polling desde el navegador
        [HttpGet("estado")]
        public IActionResult Estado(string uid) =>
            Ok(_ctx.TarjetasRC.Any(t => t.Uid == uid && t.Activa));
    }
}
