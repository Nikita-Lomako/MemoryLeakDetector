# MemoryLeakDetector.UI — план реализации

## 1. Цели и контекст
- Обеспечить удобный и понятный интерфейс для наблюдения за процессами, выявления утечек и генерации отчетов.
- UI служит основным входом пользователя и взаимодействует с `MemoryLeakDetector.Core` через сервисы/DTO.
- Поддерживаемые платформы: Windows 10/11 (WPF, .NET 8, x86_64/ARM64).

## 2. Общая структура приложения
| Слой | Назначение | Примечания |
|------|------------|------------|
| Presentation (Views) | WPF окна, страницы, пользовательские контролы. | Используем MVVM-подход. |
| ViewModels | Управление состоянием, команды, валидация. | Реактивные обновления (INotifyPropertyChanged). |
| Services (UI) | Обертки над Core/API (ProcessMonitorService, ReportService, AlertService). | Позже будут проксировать реальные реализации. |
| Models (UI DTO) | Представление данных для UI (ProcessSnapshot, LeakAlert, ReportDescriptor). | Маппинг к доменным моделям Core. |

## 3. Экранная навигация
1. **Dashboard** — обзор состояния, текущие процесс-пулы, статус сервиса, быстрые действия.
2. **Processes** — таблица процессов с метриками (RAM, виртуальная память, handles, тренды).
3. **Leak Analyzer** — временные графики, baseline сравнение, стек вызовов.
4. **Reports** — генерация/история отчетов (PDF, HTML, JSON).
5. **Settings** — параметры мониторинга, интеграции (Slack, Prometheus, JIRA и т.д.).

Навигация: Hamburger меню (NavigationView) слева + шапка с индикаторами статуса.

## 4. Ключевые UX-компоненты
- **Live charts**: используем `LiveCharts2` (позже добавить пакет) для построения графиков RAM/VM/handles.
- **Process grid**: `DataGrid` с виртуализацией, быстрая фильтрация/поиск.
- **Leak timeline**: комбинированный график baseline vs фактическое потребление, подсветка аномалий.
- **Stack trace viewer**: collapsible panel, отображает зашифрованные данные (placeholder до имплементации безопасности).
- **Notifications**: toast/StatusBar для предупреждений и советов по исправлению.

## 5. Состояние и данные
- ViewModels получают данные через интерфейсы (будут реализованы позже в Core/API).
- Для начального UI: моковые данные (in-memory) c интерфейсами `IProcessDataProvider`, `IReportProvider`.
- Обновление в реальном времени: `DispatcherTimer` (позже заменить на реактивные стримы).

## 6. План работ по итерациям
1. **Итерация 1 (текущая)**:
   - Настроить базовую структуру MVVM.
   - Реализовать Shell (главное окно) с навигацией и заглушками страниц.
   - Добавить моковые сервисы и модели.
2. **Итерация 2**:
   - Dashboard + Processes страницы с таблицами и простыми графиками.
   - Инъекция зависимости на Core (как только будет готов API).
3. **Итерация 3**:
   - Leak Analyzer (графики baseline/аномалии) + stack trace viewer.
   - Настройка уведомлений и рекомендаций.
4. **Итерация 4**:
   - Reports и Settings, интеграция с генерацией PDF/HTML/JSON.
   - Связка с Service/API для реальных данных, авторизация/логирование.

## 7. Dependecies & пакеты
- `CommunityToolkit.Mvvm` — упрощение MVVM-паттерна.
- `LiveChartsCore.SkiaSharpView.WPF` — графики.
- `MahApps.Metro` (опционально) — современная оболочка/темы.
- `Microsoft.Extensions.Hosting` — единый DI в WPF (позже, после Core).

## 8. Следующие шаги
1. Подключить `CommunityToolkit.Mvvm`.
2. Создать базовые директории (`Views`, `ViewModels`, `Services`, `Models`, `Resources`).
3. Реализовать главное окно с SplitView/NavigationView.
4. Добавить моковые ViewModels для Dashboard/Processes (пустые заглушки).
5. Связать DI контейнер для ViewModels (SimpleIoc/ServiceCollection).

Документ будет дополняться по мере реализации.

