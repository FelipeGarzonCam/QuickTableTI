using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Data.Entity; // ← IMPORTANTE: EF6, no EF Core
using QuickTableProyect.Dominio;
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
            try
            {
                var lista = _ctx.TarjetasRC
                                .Where(t => !t.Activa)
                                .Select(t => t.Uid)
                                .ToList();
                return Ok(lista);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // 2. Raspberry confirma grabación
        [HttpPost("confirmar")]
        public IActionResult Confirmar([FromForm] string uidLeido)
        {
            try
            {
                var t = _ctx.TarjetasRC.FirstOrDefault(x => x.Uid == uidLeido && !x.Activa);
                if (t == null)
                    return NotFound(new { message = "Tarjeta no encontrada o ya está activa" });

                t.Activa = true;
                _ctx.SaveChanges();
                return Ok(new { message = "Tarjeta activada correctamente" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // 3. Endpoint para polling desde el navegador
        [HttpGet("estado")]
        public IActionResult Estado(string uid)
        {
            try
            {
                if (string.IsNullOrEmpty(uid))
                    return BadRequest(new { error = "UID requerido" });

                bool activa = _ctx.TarjetasRC.Any(t => t.Uid == uid && t.Activa);
                return Ok(activa);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // 4. Validar código de sesión desde Raspberry Pi
        [HttpPost("validar-sesion")]
        public IActionResult ValidarSesion([FromForm] string sessionCode)
        {
            try
            {
                // Validar formato del código
                if (string.IsNullOrEmpty(sessionCode) || sessionCode.Length != 6 || !sessionCode.All(char.IsDigit))
                {
                    return Ok(new { valid = false, message = "Código de sesión inválido" });
                }

                // Buscar tarjeta pendiente más reciente CON Include para EF6
                var tarjetaPendiente = _ctx.TarjetasRC
                    .Include(t => t.Empleado) // EF6 syntax
                    .Where(t => !t.Activa)
                    .OrderByDescending(t => t.FechaAsignacion)
                    .FirstOrDefault();

                if (tarjetaPendiente != null)
                {
                    return Ok(new
                    {
                        valid = true,
                        role = "TI",
                        uid = tarjetaPendiente.Uid,
                        empleadoId = tarjetaPendiente.EmpleadoId,
                        adminNombre = tarjetaPendiente.Empleado?.Nombre ?? "Sin nombre",
                        fechaAsignacion = tarjetaPendiente.FechaAsignacion
                    });
                }

                return Ok(new { valid = false, message = "No hay tarjetas pendientes de grabar" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { valid = false, error = ex.Message });
            }
        }

        // 5. Obtener información específica de una sesión
        [HttpGet("sesion-info")]
        public IActionResult SesionInfo(string sessionCode)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionCode) || sessionCode.Length != 6)
                    return Ok(new { valid = false, message = "Código inválido" });

                // Buscar tarjeta pendiente más reciente
                var tarjetaPendiente = _ctx.TarjetasRC
                    .Include(t => t.Empleado) // EF6 syntax
                    .Where(t => !t.Activa)
                    .OrderByDescending(t => t.FechaAsignacion)
                    .FirstOrDefault();

                if (tarjetaPendiente != null)
                {
                    return Ok(new
                    {
                        valid = true,
                        role = "TI",
                        uid = tarjetaPendiente.Uid,
                        empleadoId = tarjetaPendiente.EmpleadoId,
                        adminNombre = tarjetaPendiente.Empleado?.Nombre ?? "Sin nombre",
                        fechaAsignacion = tarjetaPendiente.FechaAsignacion?.ToString("dd/MM/yyyy HH:mm")
                    });
                }

                return Ok(new { valid = false, message = "No hay tarjetas pendientes" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { valid = false, error = ex.Message });
            }
        }

        // 6. Obtener información de sesión activa (simplificado)
        [HttpGet("sesion-activa")]
        public IActionResult SesionActiva(string sessionCode)
        {
            try
            {
                // Validar código
                if (string.IsNullOrEmpty(sessionCode) || sessionCode.Length != 6 || !sessionCode.All(char.IsDigit))
                {
                    return Ok(new { valid = false });
                }

                var tarjetaPendiente = _ctx.TarjetasRC
                    .Where(t => !t.Activa)
                    .OrderByDescending(t => t.FechaAsignacion)
                    .FirstOrDefault();

                if (tarjetaPendiente != null)
                {
                    return Ok(new
                    {
                        valid = true,
                        role = "TI",
                        uid = tarjetaPendiente.Uid,
                        empleadoId = tarjetaPendiente.EmpleadoId
                    });
                }

                return Ok(new { valid = false });
            }
            catch (Exception ex)
            {
                return BadRequest(new { valid = false, error = ex.Message });
            }
        }

        // 7. NUEVO: Endpoint para Admin 2FA
        [HttpPost("validar-sesion-admin")]
        public IActionResult ValidarSesionAdmin([FromForm] string sessionCode)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionCode) || sessionCode.Length != 6 || !sessionCode.All(char.IsDigit))
                {
                    return Ok(new { valid = false, message = "Código inválido" });
                }

                // Para Admin, buscar código 2FA activo
                var codigo2FA = _ctx.Codigos2FA
                    .Include(c => c.Empleado) // EF6 syntax
                    .Where(c => !c.Confirmado && c.Expiracion > DateTime.Now)
                    .FirstOrDefault();

                if (codigo2FA != null && codigo2FA.Empleado?.Rol == "Admin")
                {
                    return Ok(new
                    {
                        valid = true,
                        role = "Admin",
                        navId = codigo2FA.NavegadorId,
                        empleadoId = codigo2FA.EmpleadoId,
                        adminNombre = codigo2FA.Empleado?.Nombre ?? "Admin"
                    });
                }

                return Ok(new { valid = false, message = "No hay sesiones de Admin pendientes" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { valid = false, error = ex.Message });
            }
        }

        // 8. NUEVO: Test de conectividad
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new
            {
                status = "OK",
                timestamp = DateTime.Now,
                message = "TarjetaApiController funcionando correctamente"
            });
        }
    }
}
