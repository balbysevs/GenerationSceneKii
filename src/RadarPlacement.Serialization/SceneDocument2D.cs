using System.Text.Json;
using RadarPlacement.Core.Models;

namespace RadarPlacement.Serialization;

/// <summary>
/// Связывает модель ядра с исходным JSON и идентификаторами генератора.
/// Создаётся только после проверки SceneJsonReader2D. Не требует Dispose.
/// Оригинальные поля сохраняются для экспорта результата без потери метаданных.
/// </summary>
public sealed class SceneDocument2D
{
    /// <summary>Проверенная сцена, которую следует передать методу Solve ядра.</summary>
    public Scene2D Scene { get; }
    /// <summary>Идентификатор сцены из поля sceneId.</summary>
    public string SceneId { get; }
    /// <summary>Идентификатор единственного КИИ; используется для связи радаров с объектом.</summary>
    public string KiiObjectId { get; }
    /// <summary>Неизменяемое дерево исходного документа, включая неизвестные дополнительные поля.</summary>
    public JsonElement OriginalJson { get; }

    internal SceneDocument2D(Scene2D scene, string sceneId, string kiiObjectId, JsonElement original)
    {
        Scene = scene;
        SceneId = sceneId;
        KiiObjectId = kiiObjectId;
        OriginalJson = original;
    }
}
