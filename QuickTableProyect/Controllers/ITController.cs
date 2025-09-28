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
    public class ITController : Controller
    {
        private readonly SistemaQuickTableContext _ctx = new();

        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            if (HttpContext.Session.GetString("Rol") != "IT")
                ctx.Result = RedirectToAction("Index", "Login");
            base.OnActionExecuting(ctx);
        }

        /* ---------- LISTADO ---------- */
        public IActionResult Index()
        {
            var tarjetas = _ctx.TarjetasRC.Include(t => t.Empleado)
                                          .OrderBy(t => t.Id)
                                          .ToList();
            return View(tarjetas);
        }

        /* ---------- NUEVO ADMIN + TARJETA ---------- */
        [HttpGet]
        public IActionResult CrearAdministrador() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CrearAdministrador(string nombre, string contrasena)
        {
            // 1. crea el usuario (rol Admin)
            var admin = new Empleado { Nombre = nombre, Contrasena = contrasena, Rol = "Admin" };
            _ctx.Empleados.Add(admin);
            _ctx.SaveChanges();

            // 2. genera un UID aleatorio (8 dígitos hex)
            string uid = Guid.NewGuid().ToString("N")[..8].ToUpper();           // ABCD1234 :contentReference[oaicite:1]{index=1}

            _ctx.TarjetasRC.Add(new TarjetaRC
            {
                Uid = uid,
                EmpleadoId = admin.Id,
                FechaAsignacion = DateTime.Now
            });
            _ctx.SaveChanges();

            TempData["Ok"] = $"Administrador {nombre} creado con tarjeta {uid}";
            return RedirectToAction(nameof(Index));
        }
    }
}
