namespace MyBackendApi.Models
{
    public class User
    {

        public int UserId { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        public List<Image> Images { get; set; } = new List<Image>();
    }

    // DTO para registrar un usuario
    public class RegisterDto
    {
        public required string Username { get; set; }  // opcional
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

    // DTO para login con Email
    public class LoginDto
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

    // Respuesta del login
    public class LoginResponse
    {
        public required string Token { get; set; }
        public int UserId { get; set; }
    }
}
