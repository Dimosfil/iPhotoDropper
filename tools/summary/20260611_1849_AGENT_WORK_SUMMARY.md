# AGENT_WORK_SUMMARY

## Session recap
- Инициализировали `iPhotoDropper` как проект с shared instruction-kit через `gi init` (bootstrap из `D:\AI\general-instructions`).
- Сформированы базовые файлы AGENTS/рабочие соглашения/runbook/agent-start и project-memory.
- Добавлены project-memory артефакты: `build_project_memory_index.py`, `pending-tasks`, `instruction-kit.json`, `git-preferences.json`, `system-preferences.json`.
- Выполнен `ги push`: создан коммит `a6d62cd` с инициализацией kit и успешно запушен в `origin/main`.
- Получен и обработан план MVP из `C:\Users\Fil-Dom\Documents\Codex\2026-06-11\new-chat\outputs\iPhotoDropper_MVP_Plan.md`.
- План помещён в проект по правилам в `tools/project-memory/iPhotoDropper_MVP_Plan.md`.
- По содержимому плана: есть все требования для MVP, но код приложения пока не создан.

## Active decisions
- Для MVP достаточно базового набора: подключение iPhone по USB, просмотр фото/видео, кнопка скачивания, выбор целевой папки.
- На текущем шаге приоритет: поднять каркас WinUI и интерфейсы сервисов (`IUsbDeviceService`, `IPhotoLibraryService`, `ITransferService`) до рабочего прототипа.

## Next steps
1. Создать solution `iPhotoDropper` (App + Core + Infrastructure).
2. Реализовать базовый экран WinUI 3 (статус устройства, список медиа, путь назначения, кнопка Download/Import).
3. Подготовить стабильный stub/mock для доступа к iPhone для отладки UI и прогресса.
4. Реализовать импорт с `*.tmp`-atomic write + базовый retry и отчёт.
5. После MVP добавить реальные источники медиа для iPhone.

## Notes
- Командный статус: нет нерешённых ошибок после пуша.
