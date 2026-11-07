# SceneGen для macOS

SceneGen — генератор пространственных сцен. Версия 0.1.0.

## Запуск

Готовое приложение находится в `artifacts/macos/osx-arm64/SceneGen.app`. Откройте его двойным щелчком в Finder. Можно скопировать приложение целиком в папку «Программы» и закрепить в Dock. Rider, Visual Studio и отдельная установка .NET для запуска не требуются.

Эта сборка предназначена для Mac с Apple Silicon (M1/M2/M3/M4 и новее), macOS 14 или новее. Внутри находятся .NET и библиотеки Avalonia. Для загрузки OSM необходим интернет; генерация и просмотр сохранённых данных работают локально. Сцены сохраняются в выбранную пользователем папку, кэш OSM — в пользовательскую Application Support, а не внутрь .app.

## Повторная сборка

Разработчику нужны .NET 10 SDK, macOS и доступные `codesign`, `plutil`. В терминале из корня репозитория:

```bash
./scripts/package-macos.sh
```

Для Intel Mac:

```bash
./scripts/package-macos.sh osx-x64
```

Intel-сборка предусмотрена скриптом, но здесь не проверялась. Первое восстановление зависимостей требует интернета. Готовые файлы и промежуточная сборка находятся в игнорируемой Git папке `artifacts/`; сборка .app не использует промежуточные файлы Rider.

Закройте запущенную SceneGen перед повторной упаковкой. Скрипт проверяет подпись готового пакета и только затем заменяет предыдущую сборку. Другое приложение и символические ссылки по пути назначения не заменяются. При ошибке упаковки прежняя .app сохраняется.

## Подпись

Для текущей локальной сборки используется ad-hoc подпись. Hardened Runtime не включён: для локальной подписи без Team ID он препятствует загрузке вложенных библиотек .NET. Системные настройки безопасности macOS не меняются. Это сборка для локального использования; Developer ID и нотарификация Apple для публичного распространения пока не настроены. На другом Mac загруженный пакет может потребовать подтверждения через системные настройки безопасности.

Формат пакета и порядок подписи основаны на [документации Avalonia](https://docs.avaloniaui.net/docs/deployment/macos). Для будущего распространения с Developer ID следует включить Hardened Runtime и выполнить нотарификацию; требования .NET описаны в [документации Microsoft](https://learn.microsoft.com/en-us/dotnet/core/deploying/macos).

## Файлы оформления

- `SceneGenerator.Desktop/Assets/scenegen.png` — исходный логотип, используется в окне и как иконка окна.
- `SceneGenerator.Desktop/Assets/SceneGen.icns` — иконка Finder и Dock с размерами от 16 до 1024 пикселей.
- `packaging/macos/Info.plist` — название, идентификатор и версия приложения.
- `packaging/macos/Entitlements.plist` — разрешение JIT для .NET.

Логотип создан встроенным imagegen; пользователь одобрил результат. Варианты размеров получены системным `sips`, .icns — `iconutil`.

Промпт генерации:

> Use case: logo-brand. Asset type: production macOS application icon for SceneGen, a spatial scene generator. Create one polished square 1024x1024 icon, isolated with genuine transparent outer corners, no text, no letters, no watermark, no mockup. A modern deep midnight navy rounded-square tile with generous inset safe margins and subtle depth. Center a crisp isometric land tile showing THREE very simple architectural blocks: a taller luminous violet central building and two smaller cool blue buildings, connected to a sparse coordinate grid on the tile. The geometry suggests a spatial scene editor rather than a radar application. Elegant electric blue to violet gradient accents, restrained glossy lighting, broad clean silhouette recognizable at 32px. Premium macOS desktop icon, professional geometric precision, few large shapes, no tiny windows, no antenna, no radar waves. Centered balanced composition, fill about 82 percent of image, transparent area outside rounded square.
