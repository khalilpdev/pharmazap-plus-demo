namespace PharmazapPlus.API.Controllers;

public record ResetPasswordRequest
{
    public string NewPassword { get; set; } = "";
}
