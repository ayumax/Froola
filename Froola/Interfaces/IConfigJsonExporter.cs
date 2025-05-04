using System.Threading.Tasks;

namespace Froola.Interfaces;

public interface IConfigJsonExporter
{
    Task ExportConfigJson(string path, object[] configs);
}