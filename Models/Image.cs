namespace MyBackendApi.Models
{
    public class Image
    {
        public int ImageId { get; set; }
        public int UserId { get; set; }
        public required string OriginalImagePath { get; set; }
        public required string ProcessedImagePath { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public required string Status { get; set; }
        public required string Metadata { get; set; }
        public required string ScaleOption { get; set; }

    }

    // DTO para registrar imagen original
    public class UploadPendingImageDto
    {
        public int UserId { get; set; }
        public required string ScaleOption { get; set; }
        public required string Metadata { get; set; }
        public required IFormFile OriginalFile { get; set; }
    }


    // DTO para actualizar con imagen procesada
    public class UploadProcessedImageDto
    {
        public int ImageId { get; set; }
        public int UserId { get; set; }
        public string? ScaleOption { get; set; }
        public string? Metadata { get; set; }
        public IFormFile? OriginalFile { get; set; }
        public required IFormFile ProcessedFile { get; set; }
    }



}
