using System.Data.SqlClient;
using System.Linq;
using BitacoraApi.Data;
using BitacoraApi.Models;
using BitacoraApi.Security;

namespace BitacoraApi.Repositories
{
    public class BitacoraRepository : IBitacoraRepository
    {
        private readonly BitacoraDbConnectionFactory _connectionFactory;

        public BitacoraRepository(BitacoraDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<UsuarioDto?> LoginAsync(string email, string password)
        {
            var user = await GetUsuarioByEmailAsync(email);
            if (user == null)
            {
                return null;
            }

            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT tPassword FROM MUSUARIO WHERE tEmailEmpresa = @email";
            AddParameter(cmd, "@email", email);

            var dbPassword = (string?)await cmd.ExecuteScalarAsync();
            if (dbPassword == null)
            {
                return null;
            }

            if (!PasswordHasher.Verify(password, dbPassword.Trim()))
            {
                return null;
            }

            return user;
        }

        public async Task<UsuarioDto?> GetUsuarioByEmailAsync(string email)
        {
            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT u.iMUsuario, u.tNombreRed, u.tEmailEmpresa, r.iMRol, r.tRol
FROM MUSUARIO u
INNER JOIN MROL r ON u.iMRol = r.iMRol
WHERE LOWER(u.tEmailEmpresa) = LOWER(@email)";
            AddParameter(cmd, "@email", email.Trim());

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapToUsuarioDto(reader);
            }

            return null;
        }

        public async Task<UsuarioDto?> GetUsuarioByIdAsync(string id)
        {
            if (!long.TryParse(id, out var userId)) return null;

            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT u.iMUsuario, u.tNombreRed, u.tEmailEmpresa, r.iMRol, r.tRol
FROM MUSUARIO u
INNER JOIN MROL r ON u.iMRol = r.iMRol
WHERE u.iMUsuario = @id";
            AddParameter(cmd, "@id", userId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapToUsuarioDto(reader);
            }

            return null;
        }

        private UsuarioDto MapToUsuarioDto(SqlDataReader reader)
        {
            return new UsuarioDto
            {
                Id = reader.GetInt64(0).ToString(),
                Nombre = reader.GetString(1),
                Email = reader.GetString(2),
                RolId = reader.GetInt64(3),
                Rol = reader.GetString(4)
            };
        }

        public async Task<List<UsuarioDto>> GetDesarrolladoresAsync()
        {
            var result = new List<UsuarioDto>();

            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT u.iMUsuario, u.tNombreRed, u.tEmailEmpresa, r.iMRol, r.tRol
FROM MUSUARIO u
INNER JOIN MROL r ON u.iMRol = r.iMRol
WHERE r.iMRol = 2";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new UsuarioDto
                {
                    Id = reader.GetInt64(0).ToString(),
                    Nombre = reader.GetString(1),
                    Email = reader.GetString(2),
                    RolId = reader.GetInt64(3),
                    Rol = reader.GetString(4)
                });
            }

            return result;
        }

        public async Task<List<UsuarioDto>> GetUsuariosActivosAsync()
        {
            var result = new List<UsuarioDto>();

            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT u.iMUsuario, u.tNombreRed, u.tEmailEmpresa, r.iMRol, r.tRol
FROM MUSUARIO u
INNER JOIN MROL r ON u.iMRol = r.iMRol
WHERE u.lActivo = 1";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new UsuarioDto
                {
                    Id = reader.GetInt64(0).ToString(),
                    Nombre = reader.GetString(1),
                    Email = reader.GetString(2),
                    RolId = reader.GetInt64(3),
                    Rol = reader.GetString(4)
                });
            }

            return result;
        }

        public async Task<List<RegistroDto>> GetRegistrosAsync(string? creadoPor, string? asignadoA)
        {
            var registros = new List<RegistroDto>();

            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            var cmd = connection.CreateCommand();

            var baseQuery = @"
SELECT b.iMBitacora, b.iCodigo, b.tTipo, b.tTitulo, b.tDescripcion,
       b.iMSolicitante, b.iMResponsable, b.fRegistro, b.fAsignacionTiempo, b.fCierre,
       b.Estado, 
       CASE 
           WHEN (SELECT COUNT(*) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0)) > 0
           THEN (SELECT ISNULL(SUM(s.nMinutosReal), 0) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0))
           ELSE b.nMinutosReal 
       END as nMinutosReal,
       CASE 
           WHEN (SELECT COUNT(*) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0)) > 0
           THEN (SELECT ISNULL(SUM(s.nMinutosEstimado), 0) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0))
           ELSE b.nMinutosEstimado 
       END as nMinutosEstimado,
       p.tProyecto, e.tRazonSocial,
       uResp.tNombreRed as NombreResponsable, b.tPrioridad, b.iMPadre, b.fInicio, b.fFin,
       (SELECT COUNT(*) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0)) as SubRegistrosCount
FROM DBITACORA b
LEFT JOIN MPROYECTO p ON p.iMProyecto = b.iMProyecto
LEFT JOIN MEMPRESA e ON e.iMEmpresa = b.iMEmpresa
LEFT JOIN MUSUARIO uResp ON uResp.iMUsuario = b.iMResponsable";

            var whereParts = new List<string>();
            
            // Filter only root items (parents)
            whereParts.Add("(b.iMPadre IS NULL OR b.iMPadre = 0)");

            if (!string.IsNullOrEmpty(creadoPor))
            {
                whereParts.Add("b.iMSolicitante = @creadoPor");
                AddParameter(cmd, "@creadoPor", long.Parse(creadoPor));
            }

            if (!string.IsNullOrEmpty(asignadoA))
            {
                whereParts.Add("b.iMResponsable = @asignadoA");
                AddParameter(cmd, "@asignadoA", long.Parse(asignadoA));
            }

            if (whereParts.Count > 0)
            {
                baseQuery += " WHERE " + string.Join(" AND ", whereParts);
            }

            baseQuery += " ORDER BY b.fRegistro DESC";

            cmd.CommandText = baseQuery;

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt64(0);
                var numero = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                var tipoDb = reader.GetString(2);
                var tipo = tipoDb == "I" ? "Incidente" : "Requerimiento";

                var estadoDb = reader.IsDBNull(10) ? "Pendiente" : reader.GetString(10);
                var minutosReal = reader.IsDBNull(11) ? 0 : reader.GetInt32(11);
                var minutosEstimado = reader.IsDBNull(12) ? 0 : reader.GetInt32(12);

                var reg = new RegistroDto
                {
                    Id = id.ToString(),
                    Numero = numero,
                    Tipo = tipo,
                    Titulo = reader.GetString(3),
                    Descripcion = reader.GetString(4),
                    CreadoPor = reader.GetInt64(5).ToString(),
                    AsignadoA = reader.IsDBNull(6) ? null : reader.GetInt64(6).ToString(),
                    FechaCreacion = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7),
                    FechaAsignacion = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    FechaCierre = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    Estado = estadoDb,
                    TiempoTranscurrido = minutosReal * 60,
                    TiempoEstimado = minutosEstimado * 60,
                    Proyecto = reader.IsDBNull(13) ? null : reader.GetString(13),
                    Empresa = reader.IsDBNull(14) ? null : reader.GetString(14),
                    NombreAsignado = reader.IsDBNull(15) ? null : reader.GetString(15),
                    Prioridad = reader.IsDBNull(16) ? null : reader.GetString(16),
                    ParentId = reader.IsDBNull(17) ? null : reader.GetInt64(17).ToString(),
                    FechaInicio = reader.IsDBNull(18) ? null : reader.GetDateTime(18),
                    FechaFin = reader.IsDBNull(19) ? null : reader.GetDateTime(19),
                    TaskCountFromDb = reader.IsDBNull(20) ? 0 : reader.GetInt32(20)
                };

                registros.Add(reg);
            }

            return registros;
        }

        public async Task<RegistroDto?> GetRegistroByIdAsync(string id)
        {
            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            if (!long.TryParse(id, out var bitacoraId))
            {
                return null;
            }

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT b.iMBitacora, b.iCodigo, b.tTipo, b.tTitulo, b.tDescripcion,
       b.iMSolicitante, b.iMResponsable, b.fRegistro, b.fAsignacionTiempo, b.fCierre,
       b.Estado, 
       CASE 
           WHEN (SELECT COUNT(*) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0)) > 0
           THEN (SELECT ISNULL(SUM(s.nMinutosReal), 0) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0))
           ELSE b.nMinutosReal 
       END as nMinutosReal,
       CASE 
           WHEN (SELECT COUNT(*) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0)) > 0
           THEN (SELECT ISNULL(SUM(s.nMinutosEstimado), 0) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0))
           ELSE b.nMinutosEstimado 
       END as nMinutosEstimado,
       p.tProyecto, e.tRazonSocial,
       uResp.tNombreRed as NombreResponsable, b.tPrioridad, b.iMPadre, b.fInicio, b.fFin
FROM DBITACORA b
LEFT JOIN MPROYECTO p ON p.iMProyecto = b.iMProyecto
LEFT JOIN MEMPRESA e ON e.iMEmpresa = b.iMEmpresa
LEFT JOIN MUSUARIO uResp ON uResp.iMUsuario = b.iMResponsable
WHERE b.iMBitacora = @id";
            AddParameter(cmd, "@id", bitacoraId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var numero = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            var tipoDb = reader.GetString(2);
            var tipo = tipoDb == "I" ? "Incidente" : "Requerimiento";

            var estadoDb = reader.IsDBNull(10) ? "Pendiente" : reader.GetString(10);
            var minutosReal = reader.IsDBNull(11) ? 0 : reader.GetInt32(11);
            var minutosEstimado = reader.IsDBNull(12) ? 0 : reader.GetInt32(12);

            var registro = new RegistroDto
            {
                Id = reader.GetInt64(0).ToString(),
                Numero = numero,
                Tipo = tipo,
                Titulo = reader.GetString(3),
                Descripcion = reader.GetString(4),
                CreadoPor = reader.GetInt64(5).ToString(),
                AsignadoA = reader.IsDBNull(6) ? null : reader.GetInt64(6).ToString(),
                FechaCreacion = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7),
                FechaAsignacion = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                FechaCierre = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                Estado = estadoDb,
                TiempoTranscurrido = minutosReal * 60,
                TiempoEstimado = minutosEstimado * 60,
                Proyecto = reader.IsDBNull(13) ? null : reader.GetString(13),
                Empresa = reader.IsDBNull(14) ? null : reader.GetString(14),
                NombreAsignado = reader.IsDBNull(15) ? null : reader.GetString(15),
                Prioridad = reader.IsDBNull(16) ? null : reader.GetString(16),
                ParentId = reader.IsDBNull(17) ? null : reader.GetInt64(17).ToString(),
                FechaInicio = reader.IsDBNull(18) ? null : reader.GetDateTime(18),
                FechaFin = reader.IsDBNull(19) ? null : reader.GetDateTime(19)
            };

            await reader.CloseAsync();

            var adjuntos = new List<string>();

            var cmdAdj = connection.CreateCommand();
            cmdAdj.CommandText = @"
SELECT tRutaMiniatura, tRutaArchivo, ISNULL(iPesoKb, 0), tNombreArchivo
FROM MEVIDENCIAS
WHERE iMBitacora = @id
  AND (lEliminado IS NULL OR lEliminado = 0)";
            AddParameter(cmdAdj, "@id", bitacoraId);

            using (var readerAdj = await cmdAdj.ExecuteReaderAsync())
            {
                while (await readerAdj.ReadAsync())
                {
                    var rutaMini = readerAdj.IsDBNull(0) ? null : readerAdj.GetString(0);
                    var rutaFull = readerAdj.IsDBNull(1) ? null : readerAdj.GetString(1);
                    var pesoKb = readerAdj.IsDBNull(2) ? 0 : readerAdj.GetInt32(2);
                    var nombreArchivo = readerAdj.IsDBNull(3) ? null : readerAdj.GetString(3);

                    string? value = null;
                    if (!string.IsNullOrWhiteSpace(rutaMini) && !string.IsNullOrWhiteSpace(rutaFull))
                    {
                        value = $"{rutaMini}|{rutaFull}";
                    }
                    else if (!string.IsNullOrWhiteSpace(rutaFull))
                    {
                        value = rutaFull;
                    }
                    else if (!string.IsNullOrWhiteSpace(rutaMini))
                    {
                        value = rutaMini;
                    }

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        if (pesoKb > 0)
                        {
                            value += "|" + pesoKb.ToString();
                        }

                        if (!string.IsNullOrWhiteSpace(nombreArchivo))
                        {
                            if (pesoKb <= 0)
                            {
                                value += "|0";
                            }

                            value += "|" + nombreArchivo;
                        }

                        adjuntos.Add(value);
                    }
                }
            }

            if (adjuntos.Count > 0)
            {
                registro.Adjuntos = string.Join(";", adjuntos);
            }

            var cmdPeso = connection.CreateCommand();
            cmdPeso.CommandText = @"
SELECT ISNULL(SUM(ISNULL(iPesoKb, 0)), 0)
FROM MEVIDENCIAS
WHERE iMBitacora = @id
  AND (lEliminado IS NULL OR lEliminado = 0)";
            AddParameter(cmdPeso, "@id", bitacoraId);
            var pesoObj = await cmdPeso.ExecuteScalarAsync();
            registro.TotalPesoAdjuntosKb = Convert.ToInt32(pesoObj);

            // Cargar SubRegistros Recursivamente
            registro.SubRegistros = await GetSubRegistrosRecursiveAsync(connection, bitacoraId);
            registro.TaskCountFromDb = registro.SubRegistros.Count;

            return registro;
        }

        public async Task<string> InsertRegistroAsync(RegistroDto registro)
        {
            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            using var transaction = connection.BeginTransaction();

            try
            {
                var solicitanteId = long.Parse(registro.CreadoPor);
                var responsableId = !string.IsNullOrEmpty(registro.AsignadoA) ? long.Parse(registro.AsignadoA) : solicitanteId;

                var tipoDb = registro.Tipo == "Incidente" ? "I" : "R";

                var cmdNextCodigo = connection.CreateCommand();
                cmdNextCodigo.Transaction = transaction;
                cmdNextCodigo.CommandText = "SELECT ISNULL(MAX(iCodigo), 0) + 1 FROM DBITACORA WHERE tTipo = @tipo";
                AddParameter(cmdNextCodigo, "@tipo", tipoDb);
                var nextCodigoObj = await cmdNextCodigo.ExecuteScalarAsync();
                var nextCodigo = Convert.ToInt32(nextCodigoObj);

                var proyectoId = await EnsureProyectoAsync(connection, transaction, registro.Proyecto);
                var empresaId = await EnsureEmpresaAsync(connection, transaction, registro.Empresa);

                var cmdInsert = connection.CreateCommand();
                cmdInsert.Transaction = transaction;
                cmdInsert.CommandText = @"
INSERT INTO DBITACORA
    (iCodigo, tTipo, tTitulo, tDescripcion, tPrioridad, Estado,
     iMSolicitante, iMResponsable, iMProyecto, iMEmpresa,
     fRegistro, fAsignacionTiempo, fCierre, nMinutosEstimado, nMinutosReal, fEliminacion, lEliminado,
     iMPadre, fInicio, fFin)
VALUES
    (@codigo, @tipo, @titulo, @descripcion, @prioridad, @estado,
     @solicitante, @responsable, @proyecto, @empresa,
     @fRegistro, @fAsignacion, @fCierre, @minEst, @minReal, NULL, 0,
     @padre, @fInicio, @fFin);
SELECT CAST(SCOPE_IDENTITY() AS bigint);";

                AddParameter(cmdInsert, "@codigo", nextCodigo);
                AddParameter(cmdInsert, "@tipo", tipoDb);
                AddParameter(cmdInsert, "@titulo", registro.Titulo);
                AddParameter(cmdInsert, "@descripcion", registro.Descripcion);
                AddParameter(cmdInsert, "@prioridad", registro.Prioridad);
                AddParameter(cmdInsert, "@estado", registro.Estado);
                AddParameter(cmdInsert, "@solicitante", solicitanteId);
                AddParameter(cmdInsert, "@responsable", responsableId);
                AddParameter(cmdInsert, "@proyecto", proyectoId);
                AddParameter(cmdInsert, "@empresa", empresaId);
                AddParameter(cmdInsert, "@fRegistro", registro.FechaCreacion == default ? DateTime.Now : registro.FechaCreacion);
                AddParameter(cmdInsert, "@fAsignacion", registro.FechaAsignacion);
                AddParameter(cmdInsert, "@fCierre", registro.FechaCierre);
                AddParameter(cmdInsert, "@minEst", registro.TiempoEstimado / 60);
                AddParameter(cmdInsert, "@minReal", registro.TiempoTranscurrido / 60);
                AddParameter(cmdInsert, "@padre", string.IsNullOrEmpty(registro.ParentId) ? (object)DBNull.Value : long.Parse(registro.ParentId));
                AddParameter(cmdInsert, "@fInicio", registro.FechaInicio);
                AddParameter(cmdInsert, "@fFin", registro.FechaFin);

                var newIdObj = await cmdInsert.ExecuteScalarAsync();
                var newId = Convert.ToInt64(newIdObj);

                await SaveDetallesAsync(connection, transaction, newId, registro);

                transaction.Commit();

                return newId.ToString();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task UpdateRegistroAsync(RegistroDto registro)
        {
            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            using var transaction = connection.BeginTransaction();

            try
            {
                if (!long.TryParse(registro.Id, out var bitacoraId))
                {
                    throw new InvalidOperationException("Id de registro no valido");
                }

                var solicitanteId = long.Parse(registro.CreadoPor);
                var responsableId = !string.IsNullOrEmpty(registro.AsignadoA) ? long.Parse(registro.AsignadoA) : solicitanteId;

                var proyectoId = await EnsureProyectoAsync(connection, transaction, registro.Proyecto);
                var empresaId = await EnsureEmpresaAsync(connection, transaction, registro.Empresa);

                var cmdUpdate = connection.CreateCommand();
                cmdUpdate.Transaction = transaction;
                    cmdUpdate.CommandText = @"
UPDATE DBITACORA
SET tTipo = @tipo,
    tTitulo = @titulo,
    tDescripcion = @descripcion,
    tPrioridad = @prioridad,
    Estado = @estado,
    iMSolicitante = @solicitante,
    iMResponsable = @responsable,
    iMProyecto = @proyecto,
    iMEmpresa = @empresa,
    fRegistro = @fRegistro,
    fAsignacionTiempo = @fAsignacion,
    fCierre = @fCierre,
    nMinutosEstimado = @minEst,
    nMinutosReal = @minReal,
    iMPadre = @padre,
    fInicio = @fInicio,
    fFin = @fFin
WHERE iMBitacora = @id";

                AddParameter(cmdUpdate, "@tipo", registro.Tipo == "Incidente" ? "I" : "R");
                AddParameter(cmdUpdate, "@titulo", registro.Titulo);
                AddParameter(cmdUpdate, "@descripcion", registro.Descripcion);
                AddParameter(cmdUpdate, "@prioridad", registro.Prioridad);
                AddParameter(cmdUpdate, "@estado", registro.Estado);
                AddParameter(cmdUpdate, "@solicitante", solicitanteId);
                AddParameter(cmdUpdate, "@responsable", responsableId);
                AddParameter(cmdUpdate, "@proyecto", proyectoId);
                AddParameter(cmdUpdate, "@empresa", empresaId);
                AddParameter(cmdUpdate, "@fRegistro", registro.FechaCreacion == default ? DateTime.Now : registro.FechaCreacion);
                AddParameter(cmdUpdate, "@fAsignacion", registro.FechaAsignacion);
                AddParameter(cmdUpdate, "@fCierre", registro.FechaCierre);
                AddParameter(cmdUpdate, "@minEst", registro.TiempoEstimado / 60);
                AddParameter(cmdUpdate, "@minReal", registro.TiempoTranscurrido / 60);
                AddParameter(cmdUpdate, "@padre", string.IsNullOrEmpty(registro.ParentId) ? (object)DBNull.Value : long.Parse(registro.ParentId));
                AddParameter(cmdUpdate, "@fInicio", registro.FechaInicio);
                AddParameter(cmdUpdate, "@fFin", registro.FechaFin);
                AddParameter(cmdUpdate, "@id", bitacoraId);

                await cmdUpdate.ExecuteNonQueryAsync();

                var cmdDelHistorial = connection.CreateCommand();
                cmdDelHistorial.Transaction = transaction;
                cmdDelHistorial.CommandText = "DELETE FROM DHISTORIAL WHERE iMBitacora = @id";
                AddParameter(cmdDelHistorial, "@id", bitacoraId);
                await cmdDelHistorial.ExecuteNonQueryAsync();

                await SaveDetallesAsync(connection, transaction, bitacoraId, registro);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static async Task EnsureOpenAsync(SqlConnection connection)
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
        }

        private static void AddParameter(SqlCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private async Task<long> EnsureEmpresaDefaultAsync(SqlConnection connection, SqlTransaction transaction)
        {
            var cmdSelect = connection.CreateCommand();
            cmdSelect.Transaction = transaction;
            cmdSelect.CommandText = "SELECT TOP 1 iMEmpresa FROM MEMPRESA WHERE lActivo = 1 ORDER BY iMEmpresa";

            var existing = await cmdSelect.ExecuteScalarAsync();
            if (existing != null && existing != DBNull.Value)
            {
                return Convert.ToInt64(existing);
            }

            var cmdInsert = connection.CreateCommand();
            cmdInsert.Transaction = transaction;
            cmdInsert.CommandText = @"
INSERT INTO MEMPRESA (tRuc, tRazonSocial, lActivo)
VALUES ('00000000000', 'Empresa Generica', 1);
SELECT CAST(SCOPE_IDENTITY() AS bigint);";

            var idObj = await cmdInsert.ExecuteScalarAsync();
            return Convert.ToInt64(idObj);
        }

        private async Task<long> EnsureProyectoAsync(SqlConnection connection, SqlTransaction transaction, string? nombreProyecto)
        {
            if (string.IsNullOrWhiteSpace(nombreProyecto))
            {
                nombreProyecto = "Proyecto General";
            }

            var cmdSelect = connection.CreateCommand();
            cmdSelect.Transaction = transaction;
            cmdSelect.CommandText = "SELECT TOP 1 iMProyecto FROM MPROYECTO WHERE tProyecto = @nombre";
            AddParameter(cmdSelect, "@nombre", nombreProyecto);

            var existing = await cmdSelect.ExecuteScalarAsync();
            if (existing != null && existing != DBNull.Value)
            {
                return Convert.ToInt64(existing);
            }

            var cmdInsert = connection.CreateCommand();
            cmdInsert.Transaction = transaction;
            cmdInsert.CommandText = @"
INSERT INTO MPROYECTO (tProyecto, tDescripcion, fRegistro, iUsuarioRegistro, lActivo)
VALUES (@nombre, @descripcion, @fecha, NULL, 1);
SELECT CAST(SCOPE_IDENTITY() AS bigint);";

            AddParameter(cmdInsert, "@nombre", nombreProyecto);
            AddParameter(cmdInsert, "@descripcion", nombreProyecto);
            AddParameter(cmdInsert, "@fecha", DateTime.Now);

            var idObj = await cmdInsert.ExecuteScalarAsync();
            return Convert.ToInt64(idObj);
        }

        private async Task<long> EnsureEmpresaAsync(SqlConnection connection, SqlTransaction transaction, string? nombreEmpresa)
        {
            if (string.IsNullOrWhiteSpace(nombreEmpresa))
            {
                // Si no se especifica, usar la lógica existente de empresa por defecto
                return await EnsureEmpresaDefaultAsync(connection, transaction);
            }

            var cmdSelect = connection.CreateCommand();
            cmdSelect.Transaction = transaction;
            cmdSelect.CommandText = "SELECT TOP 1 iMEmpresa FROM MEMPRESA WHERE tRazonSocial = @nombre";
            AddParameter(cmdSelect, "@nombre", nombreEmpresa);

            var existing = await cmdSelect.ExecuteScalarAsync();
            if (existing != null && existing != DBNull.Value)
            {
                return Convert.ToInt64(existing);
            }

            var cmdInsert = connection.CreateCommand();
            cmdInsert.Transaction = transaction;
            cmdInsert.CommandText = @"
INSERT INTO MEMPRESA (tRuc, tRazonSocial, lActivo)
VALUES ('00000000000', @nombre, 1);
SELECT CAST(SCOPE_IDENTITY() AS bigint);";

            AddParameter(cmdInsert, "@nombre", nombreEmpresa);

            var idObj = await cmdInsert.ExecuteScalarAsync();
            return Convert.ToInt64(idObj);
        }

        public async Task<List<string>> GetProyectosActivosAsync()
        {
            var result = new List<string>();

            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT tProyecto
FROM MPROYECTO
WHERE lActivo = 1
ORDER BY tProyecto";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    result.Add(reader.GetString(0));
                }
            }

            return result;
        }

        public async Task<List<string>> GetEmpresasActivasAsync()
        {
            var result = new List<string>();

            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT tRazonSocial
FROM MEMPRESA
WHERE lActivo = 1
ORDER BY tRazonSocial";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                {
                    result.Add(reader.GetString(0));
                }
            }

            return result;
        }

        private async Task SaveDetallesAsync(SqlConnection connection, SqlTransaction transaction, long bitacoraId, RegistroDto registro)
        {
            var existingSubRegistros = new Dictionary<long, string>();

            var cmdSelectExisting = connection.CreateCommand();
            cmdSelectExisting.Transaction = transaction;
            cmdSelectExisting.CommandText = @"
SELECT iMBitacora, tTitulo
FROM DBITACORA
WHERE iMPadre = @bitacoraId AND (lEliminado IS NULL OR lEliminado = 0)";
            AddParameter(cmdSelectExisting, "@bitacoraId", bitacoraId);

            using (var reader = await cmdSelectExisting.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingSubRegistros.Add(reader.GetInt64(0), reader.GetString(1));
                }
            }

            var processedIds = new HashSet<long>();
            var parentTipo = registro.Tipo == "Incidente" ? "I" : "R";

            if (registro.SubRegistros != null && registro.SubRegistros.Count > 0)
            {
                foreach (var sub in registro.SubRegistros)
                {
                    long subId;
                    bool exists = !string.IsNullOrWhiteSpace(sub.Id) &&
                                  long.TryParse(sub.Id, out subId) &&
                                  existingSubRegistros.ContainsKey(subId);

                    if (exists)
                    {
                        long.TryParse(sub.Id, out subId);
                        var cmdUpdate = connection.CreateCommand();
                        cmdUpdate.Transaction = transaction;
                        cmdUpdate.CommandText = @"
UPDATE DBITACORA
SET tTitulo = @titulo,
    Estado = @estado,
    fAsignacionTiempo = @fecha,
    nMinutosEstimado = @minEst,
    nMinutosReal = @minReal,
    fInicio = @fInicio,
    fFin = @fFin
WHERE iMBitacora = @id";

                        AddParameter(cmdUpdate, "@titulo", sub.Titulo);
                        AddParameter(cmdUpdate, "@estado", sub.Estado);
                        AddParameter(cmdUpdate, "@fecha", DateTime.Now);
                        AddParameter(cmdUpdate, "@minEst", sub.TiempoEstimado / 60);
                        AddParameter(cmdUpdate, "@minReal", sub.TiempoTranscurrido / 60);
                        AddParameter(cmdUpdate, "@fInicio", sub.FechaInicio);
                        AddParameter(cmdUpdate, "@fFin", sub.FechaFin);
                        AddParameter(cmdUpdate, "@id", subId);

                        await cmdUpdate.ExecuteNonQueryAsync();
                        processedIds.Add(subId);
                    }
                    else
                    {
                        var cmdNext = connection.CreateCommand();
                        cmdNext.Transaction = transaction;
                        cmdNext.CommandText = "SELECT ISNULL(MAX(iCodigo), 0) + 1 FROM DBITACORA WHERE tTipo = @tipo";
                        AddParameter(cmdNext, "@tipo", parentTipo);
                        var nextCode = Convert.ToInt32(await cmdNext.ExecuteScalarAsync());

                        var solicitanteId = !string.IsNullOrEmpty(sub.CreadoPor) && long.TryParse(sub.CreadoPor, out var sId) ? sId : 
                                           (!string.IsNullOrEmpty(registro.CreadoPor) && long.TryParse(registro.CreadoPor, out var pSId) ? pSId : 0);
                        
                        var responsableId = !string.IsNullOrEmpty(sub.AsignadoA) && long.TryParse(sub.AsignadoA, out var rId) ? rId : solicitanteId;

                        var proyectoId = await EnsureProyectoAsync(connection, transaction, sub.Proyecto ?? registro.Proyecto);
                        var empresaId = await EnsureEmpresaAsync(connection, transaction, sub.Empresa ?? registro.Empresa);

                        var cmdInsert = connection.CreateCommand();
                        cmdInsert.Transaction = transaction;
                        cmdInsert.CommandText = @"
INSERT INTO DBITACORA 
(iCodigo, tTipo, tTitulo, tDescripcion, tPrioridad, Estado, 
 iMSolicitante, iMResponsable, iMProyecto, iMEmpresa, 
 fRegistro, nMinutosEstimado, nMinutosReal, lEliminado, iMPadre, fInicio, fFin)
VALUES 
(@codigo, @tipo, @titulo, @descripcion, 'MEDIA', @estado, 
 @solicitante, @responsable, @proyecto, @empresa, 
 @fecha, @minEst, @minReal, 0, @padre, @fInicio, @fFin);";

                        AddParameter(cmdInsert, "@codigo", nextCode);
                        AddParameter(cmdInsert, "@tipo", parentTipo);
                        AddParameter(cmdInsert, "@titulo", sub.Titulo);
                        AddParameter(cmdInsert, "@descripcion", sub.Descripcion ?? "");
                        AddParameter(cmdInsert, "@estado", sub.Estado ?? "Pendiente");
                        AddParameter(cmdInsert, "@solicitante", solicitanteId);
                        AddParameter(cmdInsert, "@responsable", responsableId);
                        AddParameter(cmdInsert, "@proyecto", proyectoId);
                        AddParameter(cmdInsert, "@empresa", empresaId);
                        AddParameter(cmdInsert, "@fecha", DateTime.Now);
                        AddParameter(cmdInsert, "@minEst", sub.TiempoEstimado / 60);
                        AddParameter(cmdInsert, "@minReal", sub.TiempoTranscurrido / 60);
                        AddParameter(cmdInsert, "@padre", bitacoraId);
                        AddParameter(cmdInsert, "@fInicio", sub.FechaInicio);
                        AddParameter(cmdInsert, "@fFin", sub.FechaFin);

                        await cmdInsert.ExecuteNonQueryAsync();
                    }
                }
            }

            if (existingSubRegistros.Count > 0)
            {
                var toSoftDelete = existingSubRegistros.Keys
                    .Where(id => !processedIds.Contains(id))
                    .ToList();

                if (toSoftDelete.Count > 0)
                {
                    var cmdSoftDelete = connection.CreateCommand();
                    cmdSoftDelete.Transaction = transaction;
                    cmdSoftDelete.CommandText = @"
UPDATE DBITACORA
SET lEliminado = 1, fEliminacion = GETDATE()
WHERE iMBitacora IN (" + string.Join(",", toSoftDelete) + @")";

                    await cmdSoftDelete.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task AddEvidenciaAsync(long bitacoraId, long? tareaId, string rutaLocal, string rutaMiniaturaLocal, string nombreArchivo, int pesoKb)
        {
            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            long targetId = tareaId.HasValue ? tareaId.Value : bitacoraId;

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO MEVIDENCIAS (iMBitacora, tRutaArchivo, tRutaMiniatura, tNombreArchivo, iPesoKb, fCarga, lEliminado)
VALUES (@bitacora, @ruta, @rutaMini, @nombre, @peso, @fecha, 0)";

            AddParameter(cmd, "@bitacora", targetId);
            AddParameter(cmd, "@ruta", rutaLocal);
            AddParameter(cmd, "@rutaMini", string.IsNullOrWhiteSpace(rutaMiniaturaLocal) ? (object?)DBNull.Value ?? DBNull.Value : rutaMiniaturaLocal);
            AddParameter(cmd, "@nombre", nombreArchivo);
            AddParameter(cmd, "@peso", pesoKb);
            AddParameter(cmd, "@fecha", DateTime.Now);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<(string RutaArchivo, string RutaMiniatura)>> SoftDeleteEvidenciasGeneralesAsync(long bitacoraId, IEnumerable<string> nombresArchivos)
        {
            var result = new List<(string RutaArchivo, string RutaMiniatura)>();

            if (nombresArchivos == null)
            {
                return result;
            }

            var nombres = nombresArchivos
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (nombres.Count == 0)
            {
                return result;
            }

            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            var selectCommand = connection.CreateCommand();
            AddParameter(selectCommand, "@bitacoraId", bitacoraId);

            var paramNames = new List<string>();
            for (int i = 0; i < nombres.Count; i++)
            {
                var paramName = "@n" + i;
                paramNames.Add(paramName);
                AddParameter(selectCommand, paramName, nombres[i]);
            }

            selectCommand.CommandText = @"
SELECT tRutaArchivo, tRutaMiniatura
FROM MEVIDENCIAS
WHERE iMBitacora = @bitacoraId
  AND tNombreArchivo IN (" + string.Join(",", paramNames) + @")
  AND (lEliminado IS NULL OR lEliminado = 0)";

            using (var reader = await selectCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var rutaArchivo = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    var rutaMiniatura = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    result.Add((rutaArchivo, rutaMiniatura));
                }
            }

            var updateCommand = connection.CreateCommand();
            AddParameter(updateCommand, "@bitacoraId", bitacoraId);

            for (int i = 0; i < nombres.Count; i++)
            {
                var paramName = "@n" + i;
                AddParameter(updateCommand, paramName, nombres[i]);
            }

            updateCommand.CommandText = @"
UPDATE MEVIDENCIAS
SET lEliminado = 1
WHERE iMBitacora = @bitacoraId
  AND tNombreArchivo IN (" + string.Join(",", paramNames) + @")
  AND (lEliminado IS NULL OR lEliminado = 0)";

            await updateCommand.ExecuteNonQueryAsync();

            return result;
        }

        public async Task<List<(string RutaArchivo, string RutaMiniatura)>> SoftDeleteEvidenciasTareasAsync(long bitacoraId, IEnumerable<string> nombresArchivos)
        {
            var result = new List<(string RutaArchivo, string RutaMiniatura)>();

            if (nombresArchivos == null)
            {
                return result;
            }

            var nombres = nombresArchivos
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (nombres.Count == 0)
            {
                return result;
            }

            using var connection = _connectionFactory.CreateConnection();
            await EnsureOpenAsync(connection);

            var selectCommand = connection.CreateCommand();
            AddParameter(selectCommand, "@bitacoraId", bitacoraId);

            var paramNames = new List<string>();
            for (int i = 0; i < nombres.Count; i++)
            {
                var paramName = "@tn" + i;
                paramNames.Add(paramName);
                AddParameter(selectCommand, paramName, nombres[i]);
            }

            selectCommand.CommandText = @"
SELECT e.tRutaArchivo, e.tRutaMiniatura
FROM MEVIDENCIAS e
INNER JOIN DBITACORA b ON e.iMBitacora = b.iMBitacora
WHERE b.iMPadre = @bitacoraId
  AND e.tNombreArchivo IN (" + string.Join(",", paramNames) + @")
  AND (e.lEliminado IS NULL OR e.lEliminado = 0)";

            using (var reader = await selectCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var rutaArchivo = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    var rutaMiniatura = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    result.Add((rutaArchivo, rutaMiniatura));
                }
            }

            var updateCommand = connection.CreateCommand();
            AddParameter(updateCommand, "@bitacoraId", bitacoraId);

            for (int i = 0; i < nombres.Count; i++)
            {
                var paramName = "@un" + i;
                AddParameter(updateCommand, paramName, nombres[i]);
            }

            updateCommand.CommandText = @"
UPDATE MEVIDENCIAS
SET lEliminado = 1
FROM MEVIDENCIAS e
INNER JOIN DBITACORA b ON e.iMBitacora = b.iMBitacora
WHERE b.iMPadre = @bitacoraId
  AND e.tNombreArchivo IN (" + string.Join(",", nombres.Select((_, i) => "@un" + i)) + @")
  AND (e.lEliminado IS NULL OR e.lEliminado = 0)";

            await updateCommand.ExecuteNonQueryAsync();

            return result;
        }

        private async Task<List<RegistroDto>> GetSubRegistrosRecursiveAsync(SqlConnection connection, long parentId)
        {
            var subRegistros = new List<RegistroDto>();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT b.iMBitacora, b.iCodigo, b.tTipo, b.tTitulo, b.tDescripcion,
       b.iMSolicitante, b.iMResponsable, b.fRegistro, b.fAsignacionTiempo, b.fCierre,
       b.Estado, 
       CASE 
           WHEN (SELECT COUNT(*) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0)) > 0
           THEN (SELECT ISNULL(SUM(s.nMinutosReal), 0) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0))
           ELSE b.nMinutosReal 
       END as nMinutosReal,
       CASE 
           WHEN (SELECT COUNT(*) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0)) > 0
           THEN (SELECT ISNULL(SUM(s.nMinutosEstimado), 0) FROM DBITACORA s WHERE s.iMPadre = b.iMBitacora AND (s.lEliminado IS NULL OR s.lEliminado = 0))
           ELSE b.nMinutosEstimado 
       END as nMinutosEstimado,
       p.tProyecto, e.tRazonSocial,
       uResp.tNombreRed as NombreResponsable, b.tPrioridad, b.iMPadre, b.fInicio, b.fFin
FROM DBITACORA b
LEFT JOIN MPROYECTO p ON p.iMProyecto = b.iMProyecto
LEFT JOIN MEMPRESA e ON e.iMEmpresa = b.iMEmpresa
LEFT JOIN MUSUARIO uResp ON uResp.iMUsuario = b.iMResponsable
WHERE b.iMPadre = @parentId AND (b.lEliminado IS NULL OR b.lEliminado = 0)
ORDER BY b.iCodigo";
            AddParameter(cmd, "@parentId", parentId);

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt64(0);
                    var reg = new RegistroDto
                    {
                        Id = id.ToString(),
                        Numero = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        Tipo = reader.GetString(2) == "I" ? "Incidente" : "Requerimiento",
                        Titulo = reader.GetString(3),
                        Descripcion = reader.GetString(4),
                        CreadoPor = reader.GetInt64(5).ToString(),
                        AsignadoA = reader.IsDBNull(6) ? null : reader.GetInt64(6).ToString(),
                        FechaCreacion = reader.IsDBNull(7) ? DateTime.UtcNow : reader.GetDateTime(7),
                        FechaAsignacion = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        FechaCierre = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                        Estado = reader.IsDBNull(10) ? "Pendiente" : reader.GetString(10),
                        TiempoTranscurrido = (reader.IsDBNull(11) ? 0 : reader.GetInt32(11)) * 60,
                        TiempoEstimado = (reader.IsDBNull(12) ? 0 : reader.GetInt32(12)) * 60,
                        Proyecto = reader.IsDBNull(13) ? null : reader.GetString(13),
                        Empresa = reader.IsDBNull(14) ? null : reader.GetString(14),
                        NombreAsignado = reader.IsDBNull(15) ? null : reader.GetString(15),
                        Prioridad = reader.IsDBNull(16) ? null : reader.GetString(16),
                        ParentId = reader.IsDBNull(17) ? null : reader.GetInt64(17).ToString(),
                        FechaInicio = reader.IsDBNull(18) ? null : reader.GetDateTime(18),
                        FechaFin = reader.IsDBNull(19) ? null : reader.GetDateTime(19)
                    };
                    subRegistros.Add(reg);
                }
            }

            foreach (var sub in subRegistros)
            {
                long subId = long.Parse(sub.Id);
                
                // Recursividad
                sub.SubRegistros = await GetSubRegistrosRecursiveAsync(connection, subId);
                
                // Cargar Adjuntos
                sub.Adjuntos = await GetAdjuntosStringAsync(connection, subId);
            }

            return subRegistros;
        }

        private async Task<string?> GetAdjuntosStringAsync(SqlConnection connection, long bitacoraId)
        {
            var adjuntos = new List<string>();
            var cmdAdj = connection.CreateCommand();
            cmdAdj.CommandText = @"
SELECT tRutaMiniatura, tRutaArchivo, ISNULL(iPesoKb, 0), tNombreArchivo
FROM MEVIDENCIAS
WHERE iMBitacora = @id
  AND (lEliminado IS NULL OR lEliminado = 0)";
            AddParameter(cmdAdj, "@id", bitacoraId);

            using (var readerAdj = await cmdAdj.ExecuteReaderAsync())
            {
                while (await readerAdj.ReadAsync())
                {
                    var rutaMini = readerAdj.IsDBNull(0) ? null : readerAdj.GetString(0);
                    var rutaFull = readerAdj.IsDBNull(1) ? null : readerAdj.GetString(1);
                    var pesoKb = readerAdj.IsDBNull(2) ? 0 : readerAdj.GetInt32(2);
                    var nombreArchivo = readerAdj.IsDBNull(3) ? null : readerAdj.GetString(3);

                    string? value = null;
                    if (!string.IsNullOrWhiteSpace(rutaMini) && !string.IsNullOrWhiteSpace(rutaFull))
                    {
                        value = $"{rutaMini}|{rutaFull}";
                    }
                    else if (!string.IsNullOrWhiteSpace(rutaFull))
                    {
                        value = rutaFull;
                    }
                    else if (!string.IsNullOrWhiteSpace(rutaMini))
                    {
                        value = rutaMini;
                    }

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        if (pesoKb > 0)
                        {
                            value += "|" + pesoKb.ToString();
                        }

                        if (!string.IsNullOrWhiteSpace(nombreArchivo))
                        {
                            if (pesoKb <= 0)
                            {
                                value += "|0";
                            }
                            value += "|" + nombreArchivo;
                        }

                        adjuntos.Add(value);
                    }
                }
            }

            return adjuntos.Count > 0 ? string.Join(";", adjuntos) : null;
        }
    }
}
