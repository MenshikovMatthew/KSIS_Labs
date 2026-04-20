using Microsoft.AspNetCore.Mvc;

namespace FileStorageApi.Controllers
{
    [ApiController]
    [Route("")]
    public class StorageController : ControllerBase
    {
        // --- Конфигурация ---

        private readonly string _storageRoot = @"D:\_University_\2 Curse\КСиС\_STORAGE_";

        public StorageController()
        {
            if (!Directory.Exists(_storageRoot))
            {
                Directory.CreateDirectory(_storageRoot);
            }
        }

        // --- Вспомогательные методы ---

        private string GetPhysicalPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return _storageRoot;

            string normalizedPath = path.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_storageRoot, normalizedPath);
        }

        // --- API Эндпоинты ---

        [HttpGet("")]
        [HttpGet("{*path}")]
        public IActionResult Get(string? path = "")
        {
            string physicalPath = GetPhysicalPath(path ?? "");

            if (System.IO.File.Exists(physicalPath))
            {
                return PhysicalFile(physicalPath, "application/octet-stream");
            }

            if (Directory.Exists(physicalPath))
            {
                var items = Directory.GetFileSystemEntries(physicalPath)
                    .Select(p => new
                    {
                        Name = Path.GetFileName(p),
                        Type = Directory.Exists(p) ? "Directory" : "File"
                    });

                return Ok(items);
            }

            return NotFound();
        }

        [HttpPut("{*path}")]
        [HttpPut("")]
        public async Task<IActionResult> Put(string? path = "")
        {
            // Проверяем, что путь не пустой и в запросе есть данные
            if (string.IsNullOrEmpty(path)) return BadRequest("Path is empty");

            string physicalPath = GetPhysicalPath(path);
            string? directory = Path.GetDirectoryName(physicalPath);

            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Используем FileStream с явным указанием прав на запись
            using (var fileStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await Request.Body.CopyToAsync(fileStream);
            }

            return Ok();
        }

        [HttpHead("")]
        [HttpHead("{*path}")]
        public IActionResult Head(string? path = "")
        {
            string physicalPath = GetPhysicalPath(path ?? "");

            if (System.IO.File.Exists(physicalPath))
            {
                var fileInfo = new FileInfo(physicalPath);

                // Буква 'R' форматирует дату по стандарту RFC1123, требуемому для HTTP
                Response.Headers.Append("Content-Length", fileInfo.Length.ToString());
                Response.Headers.Append("Last-Modified", fileInfo.LastWriteTimeUtc.ToString("R"));

                return Ok();
            }

            return NotFound();
        }

        [HttpDelete("")]
        [HttpDelete("{*path}")]
        public IActionResult Delete(string? path = "")
        {
            string physicalPath = GetPhysicalPath(path ?? "");

            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
                return NoContent();
            }

            if (Directory.Exists(physicalPath))
            {
                // Флаг true включает рекурсивное удаление папки вместе со всем содержимым
                Directory.Delete(physicalPath, true);
                return NoContent();
            }

            return NotFound();
        }
    }
}