
using System.Runtime.CompilerServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace EventFlow.Services;

public class CloudinaryImageService : IImageService
{
    private readonly string _prefix;
    private readonly ICloudinary _cloudinary;
    private readonly ILogger _logger;

    public CloudinaryImageService(
        ICloudinary cloudinary,
        IConfiguration configuration,
        ILogger<CloudinaryImageService> logger
    )
    {
        _cloudinary = cloudinary;
        _prefix = configuration["ResourcePrefix"]
            ?? throw new InvalidOperationException("No resource prefix.");
        _logger = logger;
    }

    public async Task<Uri?> GetImageAsync(Guid id)
    {
        try
        {
            var result = await _cloudinary.ListResourcesByPublicIdsAsync(
                [ GetPublicId(id) ]
            );
            if (result.Error is not null)
            {
                LogError(result.Error);
                return null;
            }
            return result.Resources.Single().SecureUrl;
        }
        catch (Exception e)
        {
            LogException(e);
            return null;
        }
    }

    public async Task<Guid?> UploadImageAsync(Stream data, bool dispose = false)
    {
        var id = Guid.NewGuid();
        try
        {
            var result = await _cloudinary.UploadAsync(new ImageUploadParams()
            {
                File = new FileDescription($"{id}", data),
                PublicId = GetPublicId(id),
                Transformation = new Transformation().Quality("auto")
            });
            if (result.Error is not null)
            {
                LogError(result.Error);
                return null;
            }
            return id;
        }
        catch (Exception e)
        {
            LogException(e);
            return null;
        }
        finally
        {
            if (dispose)
            {
                await data.DisposeAsync();
            }
        }
    }

    public async Task<bool> DeleteImageAsync(Guid id)
    {
        try
        {
            var result =
                await _cloudinary.DestroyAsync(new DeletionParams(publicId: GetPublicId(id)));
            if (result.Error is not null)
            {
                LogError(result.Error);
                return false;
            }
            return true;
        }
        catch (Exception e)
        {
            LogException(e);
            return false;
        }
    }

    private string GetPublicId(Guid id)
        => Path.Combine(_prefix, $"{id}").Replace(Path.DirectorySeparatorChar, '/');

    private void LogError(Error error, [CallerMemberName] string caller = "")
        => _logger.LogError($"{{Caller}} failed with error {{Error}}", caller, error.Message);

    private void LogException(Exception e, [CallerMemberName] string caller = "")
        => _logger.LogError(e, $"{{Caller}} failed with exception", caller);
}
