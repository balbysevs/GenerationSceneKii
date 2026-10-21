using SceneGenerator.Core.Configuration;
using SceneGenerator.Core.Contracts;
using SceneGenerator.Core.Models;

namespace SceneGenerator.Core.Generation;

public class GeneratedSceneSource : ISceneSource
{
    private readonly GenerationSettings _settings;

    public int Seed { get; }

    public GeneratedSceneSource(GenerationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        GenerationSettingsValidator validator = new();
        IReadOnlyList<string> errors = validator.Validate(settings);

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join(Environment.NewLine, errors),
                nameof(settings));
        }

        _settings = settings;
        Seed = settings.Seed ?? Random.Shared.Next();
    }

    public IEnumerable<Scene> GetScenes()
    {
        Random random = new Random(Seed);
        RandomSceneGenerator generator = new(random);

        for (int index = 0; index < _settings.SceneCount; index++)
        {
            string sceneId = $"scene-{index + 1:D6}";
            yield return generator.Generate(sceneId, _settings);
        }
    }
}
