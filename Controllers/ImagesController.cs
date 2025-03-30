using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBackendApi.Data;
using MyBackendApi.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net.Http;

namespace MyBackendApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ImagesController(AppDbContext context)
        {
            _context = context;
        }


        // Método al que se le pasa un userId y filtra las imágenes de ese usuario, si no existe,las devuelve todas.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Image>>> GetImages(
            [FromHeader(Name = "X-Token")] string token,
            [FromQuery] int? userId
        )
        {
            var jwt = ValidateToken(token);
            if (jwt == null)
            {
                return Unauthorized("Token inválido o no provisto.");
            }

            // Sin userId retornar todas las imagenes
            if (userId == null)
            {
                return await _context.Images.ToListAsync();
            }
            else
            {
                // Filtra por userId
                return await _context.Images
                    .Where(img => img.UserId == userId)
                    .ToListAsync();
            }
        }


        // Método que devuelve las imágenes cuyo UserId coincida con {userId}.
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<Image>>> GetImagesByUserId(
            [FromHeader(Name = "X-Token")] string token,
            int userId
        )
        {
            var jwt = ValidateToken(token);
            if (jwt == null)
            {
                return Unauthorized("Token inválido o no provisto.");
            }

            // Validar que el token incluya el claim UserId y que coincida
            var tokenUserIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            if (tokenUserIdClaim == null || int.Parse(tokenUserIdClaim) != userId)
            {
                return Unauthorized("El token no corresponde al usuario solicitado.");
            }

            var images = await _context.Images
                .Where(img => img.UserId == userId)
                .ToListAsync();

            return images;
        }


        // Método que devuelve una imagen por su ID
        [HttpGet("{id}")]
        public async Task<ActionResult<Image>> GetImage(int id, [FromHeader(Name = "X-Token")] string token)
        {
            var jwt = ValidateToken(token);
            if (jwt == null)
            {
                return Unauthorized("Token inválido o no provisto.");
            }

            var image = await _context.Images.FindAsync(id);

            if (image == null)
                return NotFound();

            return image;
        }


        // Método que crea una nueva imagen
        [HttpPost]
        public async Task<ActionResult<Image>> CreateImage([FromHeader(Name = "X-Token")] string token, Image image)
        {
            var jwt = ValidateToken(token);
            if (jwt == null)
            {
                return Unauthorized("Token inválido o no provisto.");
            }

            _context.Images.Add(image);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetImage), new { id = image.ImageId }, image);
        }


        // Método que actualiza una imagen existente
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateImage(int id, [FromHeader(Name = "X-Token")] string token, Image updatedImage)
        {
            var jwt = ValidateToken(token);
            if (jwt == null)
            {
                return Unauthorized("Token inválido o no provisto.");
            }

            if (id != updatedImage.ImageId)
                return BadRequest("El ID de la ruta y el de la imagen no coinciden.");

            _context.Entry(updatedImage).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ImageExists(id))
                    return NotFound();
                else
                    throw;
            }
            return NoContent();
        }

        
        // Método que añade a la base de datos la imagen original en estado pendiente.Tambien la guarda en el directorio previsto.
        [HttpPost("pending")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<Image>> CreatePendingImage(
            [FromHeader(Name = "X-Token")] string token,
            [FromForm] UploadPendingImageDto dto)
            {
            var jwt = ValidateToken(token);
            if (jwt == null)
            {
                return Unauthorized("Token inválido o no provisto.");
            }

            // Verificar que venga la imagen original
            if (dto.OriginalFile == null || dto.OriginalFile.Length == 0)
            {
                return BadRequest("No se ha subido ningún archivo de imagen original.");
            }

            try
            {
                // Guardar físicamente la imagen en wwwroot/images
                string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Genera nombre único para la imagen
                string originalUniqueName = $"{Guid.NewGuid()}_{dto.OriginalFile.FileName}";
                string originalAbsolutePath = Path.Combine(folderPath, originalUniqueName);

                // Copia al disco
                using (var stream = new FileStream(originalAbsolutePath, FileMode.Create))
                {
                    await dto.OriginalFile.CopyToAsync(stream);
                }

                string originalRelativePath = "/" + Path.Combine("images", originalUniqueName).Replace("\\", "/");

                // Crear objeto Image con estado "pendiente"
                var newImage = new Image
                {
                    UserId = dto.UserId,
                    OriginalImagePath = originalRelativePath,
                    ProcessedImagePath = "",
                    ScaleOption = dto.ScaleOption,
                    Metadata = dto.Metadata,
                    Status = "pendiente",
                    ProcessedAt = DateTime.UtcNow
                };

                // Guarda en la base de datos
                _context.Images.Add(newImage);
                await _context.SaveChangesAsync();

                // Devuelve una creacion exitosa que incluye la url y la imagen original
                return CreatedAtAction(nameof(GetImage), new { id = newImage.ImageId }, newImage);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al guardar la imagen original: {ex.Message}");
            }
        }


        // Método para actualizar la imagen que estaba en pendiente
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadProcessedImage(
            [FromHeader(Name = "X-Token")] string token,
            [FromForm] UploadProcessedImageDto dto)
            {
            var jwt = ValidateToken(token);
            if (jwt == null)
            {
                return Unauthorized("Token inválido o no provisto.");
            }

            // Busca la fila existente
            var existingImage = await _context.Images.FindAsync(dto.ImageId);
            if (existingImage == null)
            {
                return NotFound($"No existe una imagen con ID={dto.ImageId}");
            }

            // Validar que exista ProcessedFile
            if (dto.ProcessedFile == null || dto.ProcessedFile.Length == 0)
            {
                return BadRequest("No se ha subido ningún archivo de imagen procesada (ProcessedFile).");
            }
            try
            {
                string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Guarda la imagen procesada
                string processedUniqueName = $"{Guid.NewGuid()}_{dto.ProcessedFile.FileName}";
                string processedAbsolutePath = Path.Combine(folderPath, processedUniqueName);

                using (var stream = new FileStream(processedAbsolutePath, FileMode.Create))
                {
                    await dto.ProcessedFile.CopyToAsync(stream);
                }

                string processedRelativePath = "/" + Path.Combine("images", processedUniqueName)
                    .Replace("\\", "/");

                // Actualiza la fila existente en la base de datos
                existingImage.ProcessedImagePath = processedRelativePath;
                existingImage.ScaleOption = dto.ScaleOption ?? existingImage.ScaleOption;
                existingImage.Metadata = dto.Metadata ?? existingImage.Metadata;
                existingImage.Status = "procesada";
                existingImage.ProcessedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(existingImage);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al guardar la imagen procesada: {ex.Message}");
            }
        }


        // Método para borrar una foto tanto de una base de datos como del directorio donde se encuentra
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImage(
            int id,
            [FromHeader(Name = "X-Token")] string token)
            {
            //  Validar token
            var jwt = ValidateToken(token);
            if (jwt == null)
            {
                return Unauthorized("Token inválido o no provisto.");
            }

            // Buscar la imagen en la base de datos
            var image = await _context.Images.FindAsync(id);
            if (image == null)
            {
                return NotFound($"No se encontró la imagen con ID={id}");
            }

            try
            {
                // Ruta base donde se guardan los archivos
                string folderPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images"
                );

                // Borrar la imagen original si existe
                if (!string.IsNullOrEmpty(image.OriginalImagePath))
                {
                    // Quita la barra inicial del directorio guardado en la base de datos
                    string relativeOriginal = image.OriginalImagePath.TrimStart('/');
                    // Extrae el nombre del archivo
                    string fileOriginalName = Path.GetFileName(relativeOriginal);

                    // Construye la ruta absoluta
                    string absoluteOriginalPath = Path.Combine(folderPath, fileOriginalName);

                    if (System.IO.File.Exists(absoluteOriginalPath))
                    {
                        System.IO.File.Delete(absoluteOriginalPath);
                    }
                }

                // Borra la imagen procesada si existe
                if (!string.IsNullOrEmpty(image.ProcessedImagePath))
                {
                    string relativeProcessed = image.ProcessedImagePath.TrimStart('/');
                    string fileProcessedName = Path.GetFileName(relativeProcessed);
                    string absoluteProcessedPath = Path.Combine(folderPath, fileProcessedName);

                    if (System.IO.File.Exists(absoluteProcessedPath))
                    {
                        System.IO.File.Delete(absoluteProcessedPath);
                    }
                }
                // Eliminar el registro de la base de datos
                _context.Images.Remove(image);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al eliminar la imagen: {ex.Message}");
            }
        }


        //Método para actualizar la foto pendiente a error
        [HttpPost("set-error")]
        public async Task<IActionResult> SetError(
            [FromHeader(Name = "X-Token")] string token,
            [FromBody] int imageId)
            {
            var jwt = ValidateToken(token);
            if (jwt == null)
            {
                return Unauthorized("Token inválido o no provisto.");
            }

            // Buscar la fila existente
            var existingImage = await _context.Images.FindAsync(imageId);
            if (existingImage == null)
            {
                return NotFound($"No existe una imagen con ID={imageId}");
            }

            // Marcar como error
            existingImage.Status = "error";
            existingImage.ProcessedAt = DateTime.UtcNow;

            // Guardar
            await _context.SaveChangesAsync();
            return Ok(existingImage);
        }


        /// Método auxiliar para validar y decodificar el token.
        private JwtSecurityToken? ValidateToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                return jwtToken;
            }
            catch
            {
                return null;
            }
        }

        private bool ImageExists(int id)
        {
            return _context.Images.Any(i => i.ImageId == id);
        }
    }
}
