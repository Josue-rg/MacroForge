# Modo desarrollo con recarga automática.
# Deja esto abierto: al guardar un .cs o .xaml se recompila y reinicia solo.
# Uso:  .\watch.ps1
dotnet watch run -c Debug --project "$PSScriptRoot\MacroForge.csproj"
