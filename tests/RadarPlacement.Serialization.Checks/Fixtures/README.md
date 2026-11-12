# Происхождение контрольной сцены

`generated-2d.json` создан настоящим `JsonSceneExporter` первой команды из репозитория
`balbysevs/GenerationSceneKii`, коммит `05616095b811eefa532b0eba4f2d16c143ea644a`.

Источники контракта:

- [DTO сцены](https://github.com/balbysevs/GenerationSceneKii/blob/05616095b811eefa532b0eba4f2d16c143ea644a/SceneGenerator.Serialization/Dto/SceneDto.cs)
- [Преобразование модели в DTO](https://github.com/balbysevs/GenerationSceneKii/blob/05616095b811eefa532b0eba4f2d16c143ea644a/SceneGenerator.Serialization/Mapping/SceneDtoMapper.cs)
- [Экспортёр](https://github.com/balbysevs/GenerationSceneKii/blob/05616095b811eefa532b0eba4f2d16c143ea644a/SceneGenerator.Serialization/JsonSceneExporter.cs)

Контрольную геометрию собрали через исходные классы Scene, SceneObject, Box3D, Point3D,
Size3D: сцена 10000 × 10000 м, КИИ от (4500,4500) до (5500,5500), две помехи размером
100 × 100 и 60 × 80 м. Высоты и отметки — нулевые. Экспортёр получил метаданные
`type: generated`, `seed: 123`, `sequenceIndex: 1`. Геометрия задана явно для
воспроизводимости; это проверка настоящего формата экспорта, а не воспроизведение случайной
генерации по seed.

Исходники экспортёра временно собирались отдельно в игнорируемой папке obj.
В отслеживаемых файлах сохранён только выходной JSON. Ни рабочие проекты, ни проверки
не зависят от библиотеки первой команды и не требуют сети для повторного запуска.
