using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Npgsql.BackendMessages;
using System.Security.Principal;

namespace RiderIntercom.Services
{
    public class ClaudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public ClaudinaryService(IConfiguration config)
        {
            var acc = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );
            _cloudinary = new Cloudinary(acc);
        }

        public async Task<string> UploadAudio(IFormFile file)
        {
            await using var stream = file.OpenReadStream();
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "rider-intercom-music"
            };
            var result = await _cloudinary.UploadAsync(uploadParams);
            return result.SecureUrl.ToString();
        }
    }
}
