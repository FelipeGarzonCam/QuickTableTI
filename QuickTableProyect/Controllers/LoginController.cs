using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data.Entity;                                // EF 6 → Include()
using QuickTableProyect.Aplicacion;
using QuickTableProyect.Dominio;
using QuickTableProyect.Persistencia.Datos;

namespace QuickTableProyect.Interface
{
    public class LoginController : Controller
    {
        private readonly EmpleadoService _empleadoService;
        private readonly RegistroSesionService _sesionService;
        private readonly SistemaQuickTableContext _ctx = new();      // EF6 DbContext

        public LoginController(
            EmpleadoService empleadoService,
            RegistroSesionService sesionService)
        {
            _empleadoService = empleadoService;
            _sesionService = sesionService;
        }

        // ----------- GET /Login --------------
        public IActionResult Index()
        {
            string rol = HttpContext.Session.GetString("Rol");
            if (!string.IsNullOrEmpty(rol))
            {
                return rol switch
                {
                    "Admin" => RedirectToAction("Index", "Administrador"),
                    "Mesero" => RedirectToAction("Index", "Mesero"),
                    "Cocina" => RedirectToAction("Index", "Cocina"),
                    "Cajero" => RedirectToAction("Index", "Caja"),
                    "IT" => RedirectToAction("Index", "IT"),
                    _ => RedirectToAction("Index", "Login")
                };
            }
            return View();
        }

        // ----------- POST /Login/Autenticar --------------
        [HttpPost]
        public JsonResult Autenticar(string nombre, string contrasena)
        {
            var emp = _empleadoService.ObtenerEmpleadoPorNombre(nombre);
            if (emp == null || emp.Contrasena != contrasena)
                return Json(new { success = false, message = "Nombre o contraseña incorrectos." });

            // 1) marca sesión anterior como error
            _sesionService.MarcarErroresPendientes(emp.Id);

            // 2) registra nueva conexión
            int regId = _sesionService.RegistrarConexion(emp.Id);

            // 3) guarda datos en la sesión de ASP.NET Core
            HttpContext.Session.SetString("Rol", emp.Rol);
            HttpContext.Session.SetString("Id", emp.Id.ToString());
            HttpContext.Session.SetString("Nombre", emp.Nombre);
            HttpContext.Session.SetInt32("RegistroSesionId", regId);

            // ----- segundo factor SOLO para administradores -----
            if (emp.Rol == "Admin")
            {
                string code = new Random().Next(100_000, 999_999).ToString();
                Guid navId = Guid.NewGuid();

                _ctx.Codigos2FA.Add(new Codigo2FA
                {
                    Codigo = code,
                    NavegadorId = navId,
                    EmpleadoId = emp.Id,
                    Expiracion = DateTime.Now.AddMinutes(10),
                    Confirmado = false
                });
                _ctx.SaveChanges();

                return Json(new
                {
                    success = true,
                    requiere2FA = true,
                    code,
                    nav = navId     // usado por el modal en JS
                });
            }

            // redirección normal si no requiere 2FA
            string url = emp.Rol switch
            {
                "Admin" => Url.Action("Index", "Administrador"),
                "Mesero" => Url.Action("Index", "Mesero"),
                "Cocina" => Url.Action("Index", "Cocina"),
                "Cajaero" => Url.Action("Index", "Caja"),
                "IT" => Url.Action("Index", "IT"),
                _ => Url.Action("Index", "Login")
            };
            return Json(new { success = true, redirectUrl = url });
        }

        // ----------- POST /Login/Confirmar2FA --------------
        [HttpPost]
        public IActionResult Confirmar2FA(Guid navId, string uid)
        {
            var registro = _ctx.Codigos2FA
                               .Include(c => c.Empleado)            // Include de EF6 :contentReference[oaicite:2]{index=2}
                               .FirstOrDefault(c => c.NavegadorId == navId &&
                                                    c.Expiracion > DateTime.Now);

            if (registro == null || registro.Confirmado)
                return BadRequest();                               // 400 :contentReference[oaicite:3]{index=3}

            var tarjeta = _ctx.TarjetasRC.FirstOrDefault(t => t.Uid == uid && t.Activa);
            if (tarjeta == null || tarjeta.EmpleadoId != registro.EmpleadoId)
                return StatusCode(401);                           // 401 :contentReference[oaicite:4]{index=4}

            registro.Confirmado = true;
            _ctx.SaveChanges();
            return Ok();                                          // 200
        }

        // ----------- GET /Login/Check2FA --------------
        [HttpGet]
        public JsonResult Check2FA(Guid navId)
        {
            bool ok = _ctx.Codigos2FA.Any(c => c.NavegadorId == navId && c.Confirmado);
            return Json(ok);                                      // JsonRequestBehavior ya no se usa en Core :contentReference[oaicite:5]{index=5}
        }

        // ----------- utilitario: ¿tiene 2FA pendiente? --------------
        private bool Requiere2FA(int empId) =>
            _ctx.Codigos2FA.Any(c => c.EmpleadoId == empId &&
                                     !c.Confirmado &&
                                     c.Expiracion > DateTime.Now);

        // ----------- GET /Login/Logout --------------
        public IActionResult Logout()
        {
            int? regId = HttpContext.Session.GetInt32("RegistroSesionId");
            if (regId.HasValue) _sesionService.RegistrarDesconexion(regId.Value);

            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
    }
}
