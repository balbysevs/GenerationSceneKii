# SceneGen для Windows

Готовый архив: `artifacts/windows/SceneGen-win-x64.zip`. Сборка для 64-разрядной Windows на процессорах Intel и AMD.

1. На Windows нажмите правой кнопкой по архиву и выберите «Извлечь всё».
2. Откройте папку `SceneGen` и запустите `SceneGen.exe`.

Переносите папку целиком: библиотеки рядом с `.exe` нужны приложению. В сборку [включена среда .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/#publish-as-self-contained), поэтому отдельная установка .NET, Rider и Visual Studio не требуются. Для получения данных OpenStreetMap нужен интернет. Сборка не подписана сертификатом издателя.

## Повторная сборка на Mac

В корне репозитория выполните `./scripts/package-windows.sh`. Потребуются .NET 10 SDK, `zip`, `unzip` и доступ к NuGet для первого восстановления зависимостей. Путь к выходной папке можно передать первым аргументом. Публикация использует собственную папку `artifacts/build/windows`, отдельную от сборок Rider и macOS. ZIP проверяется перед заменой предыдущего архива.

Иконка Windows `packaging/windows/SceneGen.ico` сделана из того же логотипа SceneGen, что и иконка macOS. Название исполняемого файла — `SceneGen.exe`; имя основной библиотеки — `SceneGenerator.Desktop.dll`.
