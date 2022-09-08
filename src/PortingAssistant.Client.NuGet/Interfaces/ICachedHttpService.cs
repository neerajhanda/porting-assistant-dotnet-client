using System.IO;
using System.Threading.Tasks;

namespace PortingAssistant.Client.NuGet.Interfaces;

public interface ICachedHttpService
{
    public Task<bool> DoesFileExistAsync(string filePath);
    public Task<Stream> DownloadFileAsync(string filePath);
}