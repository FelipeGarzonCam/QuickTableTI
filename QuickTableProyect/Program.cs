using Microsoft.Extensions.DependencyInjection;
using QuickTableProyect.Persistencia.Datos;
using QuickTableProyect.Aplicacion;
using QuickTableProyect.Dominio;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
services.AddScoped<IPedidoService, PedidoService>();
// Registro del contexto de base de datos
builder.Services.AddScoped<SistemaQuickTableContext>();



// Otros servicios
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<EmpleadoService>();
builder.Services.AddScoped<PedidoService>();
builder.Services.AddScoped<HistorialPedidoService>();
builder.Services.AddScoped<RegistroSesionService>(); // Nuevo servicio
builder.Services.AddControllersWithViews();
builder.Services.AddSession();  // Para usar sesiones

// Configurar Kestrel para obtener la IP local de la m�quina
builder.WebHost.ConfigureKestrel(options =>
{
    string localIp = GetLocalIPAddress();
    int port = 5000; // Puerto 
    options.Listen(IPAddress.Parse(localIp), port);
});

var app = builder.Build();

// Configuraci�n de la aplicaci�n
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();  // Habilitar sesiones
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(1); // Tiempo de sesi�n
    options.Cookie.HttpOnly = true; // Mejora la seguridad
    options.Cookie.IsEssential = true; // Necesario para GDPR
});

// M�todo para obtener la IP local de la m�quina
string GetLocalIPAddress()
{
    var host = Dns.GetHostEntry(Dns.GetHostName());
    foreach (var ip in host.AddressList)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return ip.ToString();
        }
    }
    throw new Exception("No se pudo determinar la direcci�n IP local.");
}