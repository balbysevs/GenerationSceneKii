# Проверки SceneGen

## Автономные проверки

Команды выполняются из корня решения:

```sh
dotnet build GenerationSceneKii.sln --configuration Release
dotnet run --project tests/SceneGenerator.OpenStreetMap.Checks --configuration Release
dotnet run --project tests/SceneGenerator.OpenStreetMap.DesktopChecks --configuration Release -- --viewmodel-only
```

Эти команды запускает [GitHub Actions](../.github/workflows/ci.yml). Первый набор проверяет разбор OSM, построение сцен и JSON. Второй проверяет логику окна импорта, включая выбор нескольких КИИ. Оба работают на тестовых данных без запуска окна и обращения к публичному Overpass.

## Оконные и сетевые проверки

Для полного сценария окна запустите `dotnet run --project tests/SceneGenerator.OpenStreetMap.DesktopChecks` в доступной графической сессии.

Флаг `--live` для `SceneGenerator.OpenStreetMap.Checks` включает отдельную проверку публичного Overpass. Сервер можно задать через `--endpoint https://…/api/interpreter`. Результат этой проверки зависит от сети и доступности сервера; в CI она не выполняется.

## 3D-отрисовка

`dotnet run --project tests/SceneGenerator.Rendering.Checks` проверяет проекцию, обратное преобразование, дворы, вогнутые контуры, видимость стен/крыш независимыми лучами, камеру, темы и неизменность JSON. Добавлена проверка масштаба ×1 по умолчанию и отсутствия растеризации на каждом событии движения во время вращения. Нужна доступная графическая сессия. Снимки пишутся в bin/.../artifacts.
