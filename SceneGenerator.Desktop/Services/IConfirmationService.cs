using System.Threading.Tasks;

namespace SceneGenerator.Desktop.Services;

public interface IConfirmationService
{
    Task<bool> ConfirmAsync(string title, string message);
}
