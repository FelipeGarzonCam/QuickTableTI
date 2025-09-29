using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using System.Data.Entity;
using QuickTableProyect.Dominio;
using QuickTableProyect.Persistencia.Datos;

namespace QuickTableProyect.Interface
{
    // Clases para recibir JSON correctamente (sin JsonElement)
    public class AdminRequest
    {
        public int adminId { get; set; }
        public string nuevaClave { get; set; } = "";
    }

    public class AdminIdRequest
    {
        public int adminId { get; set; }
    }

    public class TIController : Controller
    {
        private readonly SistemaQuickTableContext _ctx = new();

        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            if (HttpContext.Session.GetString("Rol") != "TI")
                ctx.Result = RedirectToAction("Index", "Login");
            base.OnActionExecuting(ctx);
        }

        /* ---------- LISTADO ---------- */
        public IActionResult Index()
        {
            var tarjetas = _ctx.TarjetasRC.Include(t => t.Empleado)
                                          .OrderBy(t => t.Id)
                                          .ToList();

            // Pasar información de sesión si hay una tarjeta pendiente
            ViewBag.SessionCode = TempData["SessionCode"];
            ViewBag.UID = TempData["UID"];
            ViewBag.AdminNombre = TempData["AdminNombre"];

            return View(tarjetas);
        }

        /* ---------- NUEVO ADMIN + TARJETA ---------- */
        [HttpGet]
        public IActionResult CrearAdministrador() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CrearAdministrador(string nombre, string contrasena)
        {
            try
            {
                // 1. Crear el usuario rol Admin
                var admin = new Empleado
                {
                    Nombre = nombre,
                    Contrasena = contrasena,
                    Rol = "Admin"
                };
                _ctx.Empleados.Add(admin);
                _ctx.SaveChanges();

                // 2. Generar UID y crear tarjeta NO ACTIVA
                string uid = Guid.NewGuid().ToString("N")[..8].ToUpper();

                _ctx.TarjetasRC.Add(new TarjetaRC
                {
                    Uid = uid,
                    EmpleadoId = admin.Id,
                    FechaAsignacion = DateTime.Now,
                    Activa = false  // IMPORTANTE: Inicia como no activa
                });
                _ctx.SaveChanges();

                // 3. Generar código de sesión de 6 dígitos
                string sessionCode = Math.Abs(HttpContext.Session.Id.GetHashCode()).ToString("000000")[..6];

                TempData["SessionCode"] = sessionCode;
                TempData["UID"] = uid;
                TempData["AdminNombre"] = nombre;
                TempData["Ok"] = $"Administrador {nombre} creado correctamente. Código de sesión: {sessionCode}";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al crear administrador: {ex.Message}";
                return View();
            }
        }

        /* ---------- MÉTODOS JSON PARA MODALES (CORREGIDOS) ---------- */
        [HttpPost]
        public JsonResult CambiarClaveAdmin([FromBody] AdminRequest data)
        {
            try
            {
                if (data == null || string.IsNullOrEmpty(data.nuevaClave))
                    return Json(new { success = false, message = "Datos inválidos" });

                var admin = _ctx.Empleados.FirstOrDefault(e => e.Id == data.adminId && e.Rol == "Admin");
                if (admin == null)
                    return Json(new { success = false, message = "Administrador no encontrado" });

                admin.Contrasena = data.nuevaClave;
                _ctx.SaveChanges();

                return Json(new { success = true, message = "Contraseña actualizada correctamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public JsonResult EliminarAdmin([FromBody] AdminIdRequest data)
        {
            try
            {
                if (data == null || data.adminId <= 0)
                    return Json(new { success = false, message = "ID de administrador inválido" });

                var admin = _ctx.Empleados.FirstOrDefault(e => e.Id == data.adminId && e.Rol == "Admin");
                if (admin == null)
                    return Json(new { success = false, message = "Administrador no encontrado" });

                // Eliminar tarjeta asociada primero
                var tarjeta = _ctx.TarjetasRC.FirstOrDefault(t => t.EmpleadoId == data.adminId);
                if (tarjeta != null)
                    _ctx.TarjetasRC.Remove(tarjeta);

                // Eliminar códigos 2FA pendientes
                var codigos2FA = _ctx.Codigos2FA.Where(c => c.EmpleadoId == data.adminId).ToList();
                foreach (var codigo in codigos2FA)
                {
                    _ctx.Codigos2FA.Remove(codigo);
                }

                // Eliminar administrador
                _ctx.Empleados.Remove(admin);
                _ctx.SaveChanges();

                return Json(new { success = true, message = "Administrador eliminado correctamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public JsonResult RegenerarTarjeta([FromBody] AdminIdRequest data)
        {
            try
            {
                if (data == null || data.adminId <= 0)
                    return Json(new { success = false, message = "ID de administrador inválido" });

                // Buscar administrador
                var admin = _ctx.Empleados.FirstOrDefault(e => e.Id == data.adminId && e.Rol == "Admin");
                if (admin == null)
                    return Json(new { success = false, message = "Administrador no encontrado" });

                // Eliminar tarjeta anterior
                var tarjetaAnterior = _ctx.TarjetasRC.FirstOrDefault(t => t.EmpleadoId == data.adminId);
                if (tarjetaAnterior != null)
                    _ctx.TarjetasRC.Remove(tarjetaAnterior);

                // Crear nueva tarjeta NO ACTIVA
                string nuevoUid = Guid.NewGuid().ToString("N")[..8].ToUpper();
                _ctx.TarjetasRC.Add(new TarjetaRC
                {
                    Uid = nuevoUid,
                    EmpleadoId = data.adminId,
                    FechaAsignacion = DateTime.Now,
                    Activa = false  // Importante: empieza inactiva hasta ser grabada
                });
                _ctx.SaveChanges();

                // Generar nuevo código de sesión para la regeneración
                string sessionCode = Math.Abs(HttpContext.Session.Id.GetHashCode()).ToString("000000")[..6];

                // Guardar en TempData para mostrar en la vista
                TempData["SessionCode"] = sessionCode;
                TempData["UID"] = nuevoUid;
                TempData["AdminNombre"] = admin.Nombre;

                return Json(new
                {
                    success = true,
                    uid = nuevoUid,
                    sessionCode = sessionCode,
                    message = "Tarjeta regenerada correctamente"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /* ---------- MÉTODO PARA OBTENER ESTADÍSTICAS (OPCIONAL) ---------- */
        [HttpGet]
        public JsonResult EstadisticasTarjetas()
        {
            try
            {
                var total = _ctx.TarjetasRC.Count();
                var activas = _ctx.TarjetasRC.Count(t => t.Activa);
                var pendientes = total - activas;

                return Json(new
                {
                    success = true,
                    total = total,
                    activas = activas,
                    pendientes = pendientes
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
