# 08 - Reglas de Negocio (Business Rules)

## 1. Reglas de Puntuación y Carambolas
- **Incremento/Decremento**: Los botones de anotación son `+1` (cuerpo de la tarjeta), `+2`, `+3`, `+5` y `-1`.
- **Límite Inferior**: El puntaje de un jugador nunca puede ser menor a `0`. Si se resta estando en 0, el marcador permanece en 0.
- **Transacciones Idempotentes**: Cada acción del marcador incluye un `TransactionId` (GUID). Si el servidor o la cola offline procesa dos veces el mismo GUID, la segunda llamada es ignorada sin alterar el marcador.

## 2. Cálculo de Tiempo y Tarifa
- **Fórmula de Tiempo**: El costo del tiempo transcurrido se calcula en base a los segundos jugados:
  $$\text{CostoTiempo} = \frac{\text{SegundosJugados}}{3600} \times \text{TarifaPorHora}$$
- **Redondeo**: El costo acumulado se actualiza en tiempo real en la interfaz segundo a segundo y se redondea según la moneda local al finalizar.
- **Independencia del Cronómetro**: Las solicitudes como "Pedir Cuenta" o la adición de productos **no detienen** el cronómetro. El tiempo corre continuamente hasta que la partida es finalizada explícitamente.

## 3. Seguridad UX en Modo Libre
- **Sin Administración de Consumo**: En Modo Libre no se permite la adición de productos ni interacción con administradores.
- **Mecanismos Anti-Cierre Accidental (Bajo Efectos del Alcohol)**:
  1. **Long-Press (3 Segundos)**: El usuario debe presionar de manera ininterrumpida el botón de finalización durante 3000 ms. Un indicador de progreso circular visualiza la retención.
  2. **Modal Slide-to-Confirm**: Al soltar tras los 3s, se despliega un modal interactivo que exige deslizar un control para confirmar el cierre definitivo.

## 4. Cierre y Reinicio de Partida
- **Persistencia Obligatoria**: Antes de limpiar el estado en pantalla, se debe instanciar y guardar la entidad `MatchHistory` con la fecha, duración, carambolas totales, consumos, operador y costos.
- **Reinicio Limpio de UI**: La partida finalizada borra de la interfaz activa los contadores (`0` carambolas), restaura los nombres por defecto (`Jugador 1` y `Jugador 2`) y detiene el cronómetro.

## 5. Auditoría
- Cualquier adición de consumos, inicio de mesa, cambio de tarifa o cierre de sesión genera una entrada inmutable en `AuditLogs` con el usuario actuante (`Administrador` o `Empleado`) y timestamp.
