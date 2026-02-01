// Servidor de WebSocket para notificaciones en tiempo real de Bitacora
// Requisitos:
//   - Node.js instalado
//   - Ejecutar "npm install" dentro de esta carpeta (SocketServer)
//
// Flujo general:
//   1. Cada cliente WPF se conecta mediante WebSocket y envía un mensaje "register" con su userId.
//   2. El servidor guarda la conexión asociada a ese userId.
//   3. Cuando un cliente crea/asigna un registro, envía un mensaje "assignment" con el usuario destino.
//   4. El servidor reenvía el evento solo al usuario destino (si está conectado).

const WebSocket = require('ws');
const sql = require('mssql');

// Configuración de Base de Datos
const dbConfig = {
    user: 'sa',
    password: '123',
    server: 'localhost',
    database: 'BD_TRAZZO',
    options: {
        encrypt: false, // TrustServerCertificate=True en connection string
        trustServerCertificate: true
    }
};

// Conectar a SQL Server
sql.connect(dbConfig).then(pool => {
    console.log('[SocketServer] Conectado a SQL Server correctamente.');
}).catch(err => {
    console.error('[SocketServer] Error fatal conectando a SQL Server:', err);
});

// Puerto configurable vía variable de entorno, con valor por defecto 4000
const PORT = process.env.SOCKET_PORT || 4000;

// Mapa userId -> Set de conexiones WebSocket
// Se usa Set para permitir múltiples sesiones del mismo usuario (por ejemplo, dos PCs o dos ventanas).
const clientsByUserId = new Map();

// Mapa para rastrear la tarea activa de cada usuario: WebSocket -> registroId
// Usamos el objeto ws como clave para distinguir sesiones
const activeTasksBySocket = new Map();

// Crear servidor WebSocket
const wss = new WebSocket.Server({ port: PORT });

console.log(`[SocketServer] Iniciando servidor WebSocket en puerto ${PORT}...`);

// Función auxiliar para registrar una conexión con un userId
function registerClient(userId, ws) {
  // Limpiar cualquier registro previo de ese socket
  if (ws.userId && clientsByUserId.has(ws.userId)) {
    const prevSet = clientsByUserId.get(ws.userId);
    prevSet.delete(ws);
    if (prevSet.size === 0) {
      clientsByUserId.delete(ws.userId);
    }
  }

  ws.userId = userId;

  if (!clientsByUserId.has(userId)) {
    clientsByUserId.set(userId, new Set());
  }

  const set = clientsByUserId.get(userId);
  set.add(ws);

  console.log(`[SocketServer] Usuario registrado en sockets: ${userId}. Conexiones activas: ${set.size}`);
}

// Función auxiliar para enviar un mensaje a un usuario específico
function sendToUser(userId, payload) {
  const set = clientsByUserId.get(userId);
  if (!set || set.size === 0) {
    console.log(`[SocketServer] No hay clientes conectados para el usuario ${userId}.`);
    return;
  }

  const message = JSON.stringify(payload);

  for (const ws of set) {
    if (ws.readyState === WebSocket.OPEN) {
      ws.send(message);
    }
  }
}

// Manejo de conexiones entrantes
wss.on('connection', (ws) => {
  console.log('[SocketServer] Nueva conexión WebSocket.');

  // Marcamos la conexión como viva para el mecanismo de heartbeat
  ws.isAlive = true;

  // Evento de respuesta al ping (heartbeat)
  ws.on('pong', () => {
    ws.isAlive = true;
  });

  // Manejo de mensajes entrantes desde el cliente WPF
  ws.on('message', (data) => {
    try {
      const raw = data.toString();
      const msg = JSON.parse(raw);

      // Se espera un objeto con al menos la propiedad "type"
      if (!msg || typeof msg.type !== 'string') {
        console.warn('[SocketServer] Mensaje inválido recibido:', raw);
        return;
      }

      switch (msg.type) {
        case 'register':
          // Mensaje enviado por el cliente al conectarse:
          // { type: 'register', userId: '...' }
          if (typeof msg.userId !== 'string' || !msg.userId.trim()) {
            console.warn('[SocketServer] Mensaje register inválido, falta userId.');
            return;
          }
          registerClient(msg.userId.trim(), ws);
          break;

        case 'assignment':
          if (typeof msg.toUserId !== 'string' || !msg.toUserId.trim()) {
            console.warn('[SocketServer] Mensaje assignment inválido, falta toUserId.');
            return;
          }

          const toUserId = msg.toUserId.trim();

          // Construimos el payload mínimo que necesita el cliente destino
          const payload = {
            type: 'assignment',
            registroId: msg.registroId || '',
            tipo: msg.tipo || '',
            titulo: msg.titulo || '',
            prioridad: msg.prioridad || '',
            estado: msg.estado || '',
            proyecto: msg.proyecto || '',
            fromUserId: ws.userId || null
          };

          console.log(`[SocketServer] Enviando assignment a usuario ${toUserId}:`, payload);
          sendToUser(toUserId, payload);
          break;

        case 'status_update':
          if (typeof msg.toUserId !== 'string' || !msg.toUserId.trim()) {
            console.warn('[SocketServer] Mensaje status_update inválido, falta toUserId.');
            return;
          }

          const targetUserId = msg.toUserId.trim();
          const updatePayload = {
            type: 'status_update',
            registroId: msg.registroId || '',
            estado: msg.estado || '',
            tiempoTranscurrido: msg.tiempoTranscurrido || 0,
            fromUserId: ws.userId || null
          };

          console.log(`[SocketServer] Enviando status_update a usuario ${targetUserId}:`, updatePayload);
          sendToUser(targetUserId, updatePayload);
          break;

        case 'register_task':
          // El usuario notifica que empezó a trabajar en un registro
          // Fallback: si no tenemos userId en ws, intentamos tomarlo del mensaje
          if (!ws.userId && msg.userId && typeof msg.userId === 'string') {
              console.log(`[SocketServer] Recuperando userId desde register_task: ${msg.userId}`);
              registerClient(msg.userId, ws);
          }

          if (ws.userId && msg.registroId) {
            activeTasksBySocket.set(ws, msg.registroId);
            console.log(`[SocketServer] Usuario ${ws.userId} registra tarea activa: ${msg.registroId}. Sockets activos con tareas: ${activeTasksBySocket.size}`);
          } else {
            console.warn(`[SocketServer] Intento de register_task fallido. userId: ${ws.userId}, registroId: ${msg.registroId}`);
          }
          break;

        case 'unregister_task':
          // El usuario notifica que dejó de trabajar (pausa o fin manual)
          if (ws.userId) {
            if (activeTasksBySocket.has(ws)) {
                activeTasksBySocket.delete(ws);
                console.log(`[SocketServer] Usuario ${ws.userId} desregistra tarea activa de socket actual.`);
            }
          }
          break;

        default:
          console.warn('[SocketServer] Tipo de mensaje no reconocido:', msg.type);
          break;
      }
    } catch (err) {
      console.error('[SocketServer] Error procesando mensaje:', err);
    }
  });

  // Manejo de cierre de conexión
  ws.on('close', () => {
    const userId = ws.userId;
    console.log(`[SocketServer] Conexión cerrada. UserId: ${userId || 'Anonimo'}`);

    if (userId) {
        let closedTaskRegistroId = null;

        // 1. Limpieza inmediata de mapas para mantener estado consistente
        if (activeTasksBySocket.has(ws)) {
            closedTaskRegistroId = activeTasksBySocket.get(ws);
            activeTasksBySocket.delete(ws);
        }

        if (clientsByUserId.has(userId)) {
            const set = clientsByUserId.get(userId);
            set.delete(ws);
            if (set.size === 0) {
                clientsByUserId.delete(userId);
            }
        }

        // 2. Si tenía tarea activa, programar verificación diferida (Grace Period)
        if (closedTaskRegistroId) {
            console.log(`[SocketServer] [GracePeriod] Usuario ${userId} desconectado. Tarea: ${closedTaskRegistroId}. Esperando 5s...`);
            
            setTimeout(async () => {
                // Verificar si el usuario ha vuelto
                let isUserBackAndWorking = false;
                const userSockets = clientsByUserId.get(userId);
                
                if (userSockets && userSockets.size > 0) {
                    console.log(`[SocketServer] [GracePeriod] Usuario ${userId} tiene ${userSockets.size} sockets activos.`);
                    for (const s of userSockets) {
                        if (s.readyState === WebSocket.OPEN && activeTasksBySocket.has(s)) {
                            if (activeTasksBySocket.get(s) === closedTaskRegistroId) {
                                isUserBackAndWorking = true;
                                break;
                            }
                        }
                    }
                } else {
                    console.log(`[SocketServer] [GracePeriod] Usuario ${userId} NO tiene sockets activos.`);
                }

                if (isUserBackAndWorking) {
                    console.log(`[SocketServer] [GracePeriod] Usuario ${userId} recuperado en tarea ${closedTaskRegistroId}. NO se pausa.`);
                } else {
                    console.log(`[SocketServer] [GracePeriod] Ejecutando PAUSA para registro ${closedTaskRegistroId}...`);
                    
                    try {
                        const request = new sql.Request();
                        request.input('id', sql.VarChar, closedTaskRegistroId);
                        
                        // Solo pausamos si sigue en proceso (Estado 2)
                        const result = await request.query('UPDATE Registros SET Estado = 3 WHERE Id = @id AND Estado = 2');
                        
                        if (result.rowsAffected[0] > 0) {
                            console.log(`[SocketServer] [GracePeriod] PAUSA EXITOSA en BD para ${closedTaskRegistroId}.`);
                            
                            // Notificar por si acaso (ej. volvió pero a otra pantalla)
                            const updatePayload = {
                                type: 'status_update',
                                registroId: closedTaskRegistroId,
                                estado: 'En Pausa',
                                tiempoTranscurrido: 0,
                                fromUserId: 'SYSTEM'
                            };
                            sendToUser(userId, updatePayload);

                        } else {
                            console.log(`[SocketServer] [GracePeriod] No se actualizó. RowsAffected: ${result.rowsAffected[0]}. (¿Ya estaba pausado?)`);
                        }
                    } catch (dbErr) {
                        console.error('[SocketServer] Error SQL en GracePeriod:', dbErr);
                    }
                }
            }, 5000); // 5 segundos de espera
        }
    }
    console.log('[SocketServer] Conexión cerrada.');
  });

  // Manejo de errores en la conexión
  ws.on('error', (err) => {
    console.error('[SocketServer] Error en conexión WebSocket:', err);
  });
});

// Heartbeat para cerrar conexiones muertas y liberar recursos
const interval = setInterval(() => {
  wss.clients.forEach((ws) => {
    if (ws.isAlive === false) {
      console.log('[SocketServer] Cerrando conexión inactiva (heartbeat failed).');
      return ws.terminate();
    }

    ws.isAlive = false;
    ws.ping();
  });
}, 5000); // Reducido a 5s para detección rápida de cierres inesperados sin sobrecarga

wss.on('close', () => {
  clearInterval(interval);
});

console.log('[SocketServer] Servidor WebSocket listo.');
