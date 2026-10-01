namespace Pos.Application.Licensing;

/// <summary>ID estable de la máquina, derivado del sistema operativo (011, research §1).</summary>
public interface IMachineIdProvider
{
    string GetMachineId();
}
