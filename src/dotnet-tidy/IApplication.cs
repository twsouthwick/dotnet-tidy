using System.Threading;
using System.Threading.Tasks;

namespace PackageVersionUpdater
{
    public interface IApplication
    {
        Task RunAsync(CancellationToken token);
    }
}
