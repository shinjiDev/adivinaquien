namespace AdivinaQue.Contracts.Realtime;

public sealed record CardDto(string Id, string Nombre = "", string Imagen = "", string Ficha = "");
