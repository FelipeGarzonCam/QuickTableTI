﻿using QuickTableProyect.Dominio;
using QuickTableProyect.Persistencia.Datos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity; 


namespace QuickTableProyect.Aplicacion
{
    public class HistorialPedidoService
    {
        private readonly SistemaQuickTableContext _context;

        public HistorialPedidoService(SistemaQuickTableContext context)
        {
            _context = context;
        }

        public void CrearHistorialPedido(HistorialPedido historialPedido, PedidosActivos pedido = null)
        {
            if (pedido != null)
            {
                // Copy FechaCreacion from PedidosActivos to FechaHora in HistorialPedido
                historialPedido.FechaHora = pedido.FechaCreacion;

                // Copy timestamps for preparation and acceptance
                historialPedido.CocinaListoAt = pedido.CocinaListoAt;
                historialPedido.MeseroAceptadoAt = pedido.MeseroAceptadoAt;
            }
            else
            {
                // If we don't have the source pedido, try to get it from the database
                var pedidoActivo = _context.PedidosActivos
                    .FirstOrDefault(p => p.MeseroId == historialPedido.MeseroId &&
                                        p.NumeroMesa == historialPedido.NumeroMesa &&
                                        p.Estado == EstadosPedido.Listo);

                if (pedidoActivo != null)
                {
                    // Copy the timestamps
                    historialPedido.FechaHora = pedidoActivo.FechaCreacion;
                    historialPedido.CocinaListoAt = pedidoActivo.CocinaListoAt;
                    historialPedido.MeseroAceptadoAt = pedidoActivo.MeseroAceptadoAt;
                }
            }

            _context.HistorialPedidos.Add(historialPedido);
            _context.SaveChanges();
        }

        public List<HistorialPedido> ObtenerHistorialPedidos()
        {
            var historial = _context.HistorialPedidos
                .Include(h => h.Detalles)
                .OrderByDescending(h => h.FechaHora)
                .ToList();

            return historial;
        }

        // Método para obtener pedidos paginados
        public List<HistorialPedido> ObtenerHistorialPedidosPaginados(int pageNumber, int pageSize)
        {
            return _context.HistorialPedidos
                .Include(h => h.Detalles)
                .OrderByDescending(h => h.FechaHora)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        public int ObtenerTotalHistorialPedidos()
        {
            return _context.HistorialPedidos.Count();
        }
        public HistorialPedido ObtenerHistorialPedidoPorId(int id)
        {
            return _context.HistorialPedidos
                .Include(h => h.Detalles)
                .FirstOrDefault(h => h.Id == id);
        }
        public int ObtenerTotalHistorialPedidos(string fecha = null, int? pedidoId = null, int? meseroId = null)
        {
            var query = _context.HistorialPedidos.AsQueryable();

            if (!string.IsNullOrEmpty(fecha))
            {
                if (DateTime.TryParse(fecha, out DateTime fechaParsed))
                {
                    query = query.Where(h => DbFunctions.TruncateTime(h.FechaHora) == DbFunctions.TruncateTime(fechaParsed));
                }
            }

            if (pedidoId.HasValue)
            {
                query = query.Where(h => h.Id == pedidoId.Value);
            }

            if (meseroId.HasValue)
            {
                query = query.Where(h => h.MeseroId == meseroId.Value);
            }

            return query.Count();
        }

        public List<HistorialPedido> ObtenerHistorialPedidosPaginados(int pageNumber, int pageSize, string fecha = null, int? pedidoId = null, int? meseroId = null)
        {
            var query = _context.HistorialPedidos
                .Include(h => h.Detalles)
                .OrderByDescending(h => h.FechaHora)
                .AsQueryable();

            if (!string.IsNullOrEmpty(fecha))
            {
                if (DateTime.TryParse(fecha, out DateTime fechaParsed))
                {
                    query = query.Where(h => DbFunctions.TruncateTime(h.FechaHora) == DbFunctions.TruncateTime(fechaParsed));
                }
            }

            if (pedidoId.HasValue)
            {
                query = query.Where(h => h.Id == pedidoId.Value);
            }

            if (meseroId.HasValue)
            {
                query = query.Where(h => h.MeseroId == meseroId.Value);
            }

            return query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }


    }

}
