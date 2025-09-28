﻿using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using QuickTableProyect.Dominio;

namespace QuickTableProyect.Persistencia.Datos
{
    public class SistemaQuickTableContext : DbContext
    {
        static string connectionString =
            "Server=localhost;" +
            "Database=QuickTableProyectDB;" +
            "Trusted_Connection=True;"; 

        public SistemaQuickTableContext() : base(connectionString)
        {
        }

        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Empleado> Empleados { get; set; }
        public DbSet<PedidosActivos> PedidosActivos { get; set; }
        public DbSet<ItemDetalle> ItemDetalles { get; set; }

        public DbSet<HistorialPedido> HistorialPedidos { get; set; }
        public DbSet<HistorialDetalle> HistorialDetalles { get; set; }

        public DbSet<RegistroSesion> RegistroSesiones { get; set; } 

        public DbSet<TarjetaRC> TarjetasRC { get; set; }
        public DbSet<Codigo2FA> Codigos2FA { get; set; }


        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Eliminar pluralización automática de nombres de tablas
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            base.OnModelCreating(modelBuilder);
        }
    }
} //masterrrr



