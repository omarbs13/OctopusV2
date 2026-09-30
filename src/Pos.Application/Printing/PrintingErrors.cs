using Pos.Application.Abstractions;

namespace Pos.Application.Printing;

/// <summary>No hay impresora (ni virtual) configurada en esta máquina.</summary>
public sealed record NotConfigured : Error;

/// <summary>La impresora no está disponible: apagada, desconectada o inexistente.</summary>
public sealed record PrinterUnavailable : Error;

/// <summary>El ticket no se pudo imprimir o guardar.</summary>
public sealed record PrintFailed : Error;

/// <summary>El cajón no se pudo abrir.</summary>
public sealed record DrawerFailed : Error;
