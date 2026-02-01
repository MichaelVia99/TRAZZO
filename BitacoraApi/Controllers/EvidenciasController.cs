using BitacoraApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace BitacoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [SupportedOSPlatform("windows")]
    public class EvidenciasController : ControllerBase
    {
        private readonly IBitacoraRepository _repository;
        private readonly IWebHostEnvironment _environment;

        public EvidenciasController(IBitacoraRepository repository, IWebHostEnvironment environment)
        {
            _repository = repository;
            _environment = environment;
        }

        private static bool IsImageFile(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp";
        }

        private static string? CreateThumbnail(string sourcePath, string thumbPath, int maxWidth = 150, int maxHeight = 150)
        {
            if (!System.IO.File.Exists(sourcePath))
                return null;

            using var image = Image.FromFile(sourcePath);

            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = Math.Max(1, (int)(image.Width * ratio));
            var newHeight = Math.Max(1, (int)(image.Height * ratio));

            using var thumb = new Bitmap(newWidth, newHeight);
            using (var graphics = Graphics.FromImage(thumb))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            var dir = Path.GetDirectoryName(thumbPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            thumb.Save(thumbPath, ImageFormat.Png);
            return thumbPath;
        }

        [HttpPost("bitacora/{bitacoraId:long}")]
        public async Task<ActionResult<List<object>>> UploadForBitacora(long bitacoraId)
        {
            if (Request.Form.Files.Count == 0)
            {
                return BadRequest("No se enviaron archivos.");
            }

            var uploadRoot = Path.Combine(_environment.ContentRootPath, "Evidencias", bitacoraId.ToString());
            Directory.CreateDirectory(uploadRoot);

            var result = new List<object>();

            foreach (var file in Request.Form.Files)
            {
                if (file.Length <= 0)
                    continue;

                var safeFileName = Path.GetFileName(file.FileName);
                var filePath = Path.Combine(uploadRoot, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                var pesoKb = (int)Math.Ceiling(file.Length / 1024.0);
                var relativePath = $"/evidencias/{bitacoraId}/{safeFileName}";
                var publicUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{relativePath}";

                string? thumbLocalPath = null;
                string? thumbRelativePath = null;
                string? thumbPublicUrl = null;

                if (IsImageFile(safeFileName))
                {
                    var thumbFileName = $"thumb_{safeFileName}";
                    var thumbPath = Path.Combine(uploadRoot, thumbFileName);
                    var created = CreateThumbnail(filePath, thumbPath);
                    if (!string.IsNullOrWhiteSpace(created))
                    {
                        thumbLocalPath = created;
                        thumbRelativePath = $"/evidencias/{bitacoraId}/{thumbFileName}";
                        thumbPublicUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{thumbRelativePath}";
                    }
                }

                await _repository.AddEvidenciaAsync(bitacoraId, null, filePath, thumbLocalPath ?? string.Empty, safeFileName, pesoKb);

                result.Add(new
                {
                    RutaPublica = publicUrl,
                    RutaMiniatura = thumbPublicUrl,
                    Nombre = safeFileName,
                    PesoKb = pesoKb
                });
            }

            return Ok(result);
        }

        [HttpPost("bitacora/{bitacoraId:long}/tarea/{tareaId:long}")]
        public async Task<ActionResult<List<object>>> UploadForTarea(long bitacoraId, long tareaId)
        {
            if (Request.Form.Files.Count == 0)
            {
                return BadRequest("No se enviaron archivos.");
            }

            var uploadRoot = Path.Combine(_environment.ContentRootPath, "Evidencias", bitacoraId.ToString());
            Directory.CreateDirectory(uploadRoot);

            var result = new List<object>();

            foreach (var file in Request.Form.Files)
            {
                if (file.Length <= 0)
                    continue;

                var safeFileName = Path.GetFileName(file.FileName);
                var filePath = Path.Combine(uploadRoot, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                var pesoKb = (int)Math.Ceiling(file.Length / 1024.0);
                var relativePath = $"/evidencias/{bitacoraId}/{safeFileName}";
                var publicUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{relativePath}";

                string? thumbLocalPath = null;
                string? thumbRelativePath = null;
                string? thumbPublicUrl = null;

                if (IsImageFile(safeFileName))
                {
                    var thumbFileName = $"thumb_{safeFileName}";
                    var thumbPath = Path.Combine(uploadRoot, thumbFileName);
                    var created = CreateThumbnail(filePath, thumbPath);
                    if (!string.IsNullOrWhiteSpace(created))
                    {
                        thumbLocalPath = created;
                        thumbRelativePath = $"/evidencias/{bitacoraId}/{thumbFileName}";
                        thumbPublicUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{thumbRelativePath}";
                    }
                }

                await _repository.AddEvidenciaAsync(bitacoraId, tareaId, filePath, thumbLocalPath ?? string.Empty, safeFileName, pesoKb);

                result.Add(new
                {
                    RutaPublica = publicUrl,
                    RutaMiniatura = thumbPublicUrl,
                    Nombre = safeFileName,
                    PesoKb = pesoKb
                });
            }

            return Ok(result);
        }
    }
}
