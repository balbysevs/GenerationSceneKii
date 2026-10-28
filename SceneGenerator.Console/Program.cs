using SceneGenerator.Core.Models;
using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Generation;
using SceneGenerator.Core.Contracts;
using SceneGenerator.Serialization;
using SceneGenerator.Serialization.Dto;

namespace SceneGenerator.Console;
class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 1)
        {
            System.Console.Error.WriteLine("Укажите один путь к файлу настроек или запустите без аргументов.");
            Environment.ExitCode = 1;
            return;
        }

        string settingsPath = args.Length == 1
            ? args[0]
            : Path.Combine(AppContext.BaseDirectory, "settings.json");
        IGenerationSettingsReader settingsReader = new JsonGenerationSettingsReader();
        GenerationSettings settings;
        try
        {
            settings = settingsReader.Read(settingsPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or FormatException or ArgumentException)
        {
            System.Console.Error.WriteLine($"Не удалось загрузить настройки: {error.Message}");
            Environment.ExitCode = 1;
            return;
        }

        System.Console.WriteLine($"Настройки загружены: {Path.GetFullPath(settingsPath)}");

        System.Console.WriteLine(
            $"Режим: {settings.Mode}, сцен: {settings.SceneCount}, " +
            $"помех на сцену: {settings.ObstacleCount}");

        GeneratedSceneSource generatedSource = new(settings);
        ISceneSource sceneSource = generatedSource;
        int savedSceneCount = 0;
        string runFolderName = $"run-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
        string resultDirectory = Path.Combine("Result", runFolderName);

        try
        {
            foreach (Scene currentScene in sceneSource.GetScenes())
            {
                int sequenceIndex = savedSceneCount + 1;
                ISceneExporter sceneExporter = new JsonSceneExporter(new SceneSourceDto
                {
                    Type = "generated",
                    Seed = generatedSource.Seed,
                    SequenceIndex = sequenceIndex
                });
                string filePath = Path.Combine(resultDirectory, $"scene-{sequenceIndex:D6}.json");

                try
                {
                    sceneExporter.Export(currentScene, filePath);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    System.Console.Error.WriteLine(
                        $"Не удалось сохранить сцену {currentScene.Id}: {error.Message}");
                    System.Console.Error.WriteLine($"Успешно сохранено сцен: {savedSceneCount}.");
                    Environment.ExitCode = 1;
                    return;
                }

                savedSceneCount++;
                System.Console.WriteLine(
                    $"{currentScene.Id}: объектов — {currentScene.Objects.Count}. " +
                    $"Сохранена: {Path.GetFullPath(filePath)}");
            }
        }
        catch (InvalidOperationException error)
        {
            System.Console.Error.WriteLine($"Не удалось сгенерировать сцены: {error.Message}");
            System.Console.Error.WriteLine($"Успешно сохранено сцен: {savedSceneCount}.");
            Environment.ExitCode = 1;
            return;
        }

        System.Console.WriteLine($"Всего сохранено сцен: {savedSceneCount}.");
    }
}
