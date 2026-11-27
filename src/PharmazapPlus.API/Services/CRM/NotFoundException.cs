namespace PharmazapPlus.API.Services.CRM;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
