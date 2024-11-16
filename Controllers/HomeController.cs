using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WebApplication9.Models;

namespace WebApplication9.Controllers
{
    public class HomeController : Controller
    {
        // Upload video and display the conversion option
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile videoFile)
        {
            if (videoFile != null && videoFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string filePath = Path.Combine(uploadsFolder, videoFile.FileName);

                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await videoFile.CopyToAsync(stream);
                    }

                    return View("Convert", new VideoFile { FileName = videoFile.FileName });
                }
                catch (Exception ex)
                {
                    // Log the error for debugging
                    Debug.WriteLine($"Upload Error: {ex.Message}");
                    return StatusCode(500, "An error occurred while uploading the file.");
                }
            }

            return View("Index");
        }

        // Convert video to a different format (e.g., MP4 to AVI)
        [HttpPost]
        public IActionResult Convert(string fileName, string targetFormat)
        {
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            string inputFilePath = Path.Combine(uploadsFolder, fileName);
            string outputFileName = Path.GetFileNameWithoutExtension(fileName) + "." + targetFormat;
            string outputFilePath = Path.Combine(uploadsFolder, outputFileName);

            string ffmpegExePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ffmpeg", "ffmpeg.exe");

            if (!System.IO.File.Exists(ffmpegExePath))
            {
                return BadRequest("FFmpeg executable not found.");
            }

            try
            {
                // Check if the output file already exists, and delete it if it does (with retry logic)
                if (System.IO.File.Exists(outputFilePath))
                {
                    int retryCount = 0;
                    bool fileDeleted = false;
                    while (retryCount < 3 && !fileDeleted)
                    {
                        try
                        {
                            System.IO.File.Delete(outputFilePath); // Attempt to delete the existing file
                            fileDeleted = true;  // If deletion is successful, exit the loop
                        }
                        catch (IOException)
                        {
                            retryCount++;
                            System.Threading.Thread.Sleep(500); // Wait for 500ms before retrying
                        }
                    }

                    if (!fileDeleted)
                    {
                        return BadRequest("Could not delete existing file. It may be locked or in use.");
                    }
                }

                // Set FFmpeg arguments based on the target format
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegExePath,
                    Arguments = $"-i \"{inputFilePath}\" \"{outputFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                string output;
                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    output = process.StandardError.ReadToEnd(); // FFmpeg writes logs to StandardError
                    process.WaitForExit();
                }

                if (!System.IO.File.Exists(outputFilePath))
                {
                    Debug.WriteLine($"FFmpeg Error: {output}");
                    return StatusCode(500, "Video conversion failed. Check server logs for details.");
                }

                return RedirectToAction("Download", new { fileName = outputFileName });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception: {ex.Message}");
                return StatusCode(500, "An error occurred during video conversion.");
            }
        }

        // Download converted file
        public IActionResult Download(string fileName)
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileName);
            if (System.IO.File.Exists(filePath))
            {
                return File(System.IO.File.ReadAllBytes(filePath), "application/octet-stream", fileName);
            }
            return NotFound();
        }
    }
}
