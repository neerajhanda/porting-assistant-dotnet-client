using System.IO;
using System.Threading.Tasks;

namespace PortingAssistant.Client.NuGet.Interfaces
{
    public interface IHttpService
    {
        public Task<bool> DoesS3FileExistAsync(string filePath);
        public Task<Stream> DownloadS3FileAsync(string fileToDownload);

        public Task<bool> DoesGitHubFileExistAsync(string filePath);
        public Task<Stream> DownloadGitHubFileAsync(string fileToDownload);
    }
}
