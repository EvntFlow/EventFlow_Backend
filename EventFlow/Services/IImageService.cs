namespace EventFlow.Services;

public interface IImageService
{
    public Task<Guid?> UploadImageAsync(Stream data, bool dispose = false);

    public Task<Uri?> GetImageAsync(Guid id);

    public Task<bool> DeleteImageAsync(Guid id);
}
