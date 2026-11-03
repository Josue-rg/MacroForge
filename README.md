# MacroForge

Macro de teclado y ratón para Windows, con interfaz y un solo `.exe`.

## Uso

1. Abre `dist\MacroForge.exe`.
2. Pulsa **Configurar**: en las pestañas simples te pide la tecla de **Iniciar** (ventana azul) y la de **Detener** (roja). En **Avanzado** te pide **Grabar**, **Play** y **Parar**.
3. **Autoclic / Auto-tecla**: elige botón o tecla, **Velocidad**, **Veces** y, si quieres, **Tiempo** para parar solo.
4. **Avanzado**: pulsa **Grabar**, haz todo lo que quieras (teclas, clics y movimiento del ratón) y pulsa **Parar**. Elige **Veces** y pulsa **Play** para repetirlo.
5. Usa tus teclas desde cualquier ventana.

`F12` sigue deteniendo siempre, aunque no lo hayas configurado.

## Avanzado (grabar y reproducir)

- Graba **todo** lo que hagas mientras grabas: pulsaciones, clics y el recorrido del ratón, con sus tiempos.
- Al parar, la grabación queda lista. Solo eliges **Veces** (0 = infinito).
- **Play** reproduce la grabación y **Stop** la para.
- Las teclas de **Grabar**, **Play** y **Parar** se asignan con **Configurar**.
- **Guardar** guarda también la grabación en el `.json`.

## Atajos

- **Iniciar** (o **Grabar** en Avanzado), **Detener** (o **Parar**) y **Play** tienen su propia tecla o combinación.
- Las teclas se guardan entre sesiones.
- `F12` detiene siempre, aunque no hayas configurado nada.

## Guardar y compartir

- **Guardar…** / **Abrir…** usan archivos `.json`, así que un amigo puede cargar tu macro.
- Para compartir la app solo hace falta enviar `dist\MacroForge.exe`. No requiere instalar .NET.

## Recompilar

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o dist
```

## Límites

- Si la ventana destino corre **como administrador**, MacroForge también debe ejecutarse como administrador.
- No funciona (ni conviene) en juegos con anti-cheat a nivel kernel.
- El ratón se mueve en saltos; no hay curva de aceleración.
