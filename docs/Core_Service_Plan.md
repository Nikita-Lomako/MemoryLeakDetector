# MemoryLeakDetector.Core & Service — дорожная карта

## 1. Цели этапа
- Вынести всю бизнес-логику мониторинга и детектирования утечек в `MemoryLeakDetector.Core`.
- Предоставить сервисному воркеру устойчивый API для сбора метрик до 100 процессов в реальном времени.
- Заложить основу для baseline/аномалий, логирования, отказоустойчивости и последующей интеграции с API/UI.

## 2. Основные подсистемы Core
| Подсистема | Содержание | Примечания |
|------------|------------|------------|
| Models | DTO: `ProcessMetricSnapshot`, `LeakDetectionInsight`, `MonitoringCycleResult`. | Независимы от UI. |
| Metrics Collection | Интерфейсы `IProcessEnumerator`, `IMemoryMetricsProvider`, `IProcessMetricsCollector`. | По умолчанию использует `System.Diagnostics.Process`. |
| Baseline Store | `IBaselineRepository` + in-memory реализация (running stats, last N точек). | Позже вынесем в персистентное хранилище. |
| Leak Detection | `ILeakDetectionStrategy` (статистика + пороги + тренд). | Позже добавим ML.NET/PLINQ. |
| Monitoring Orchestrator | `IMonitoringCoordinator` → агрегирует сбор, baseline, детектор, логирование. | Используется Service и API. |
| Diagnostics bus | Контракты для событий/логов (пока заглушки). | Подготовка к Prometheus/Grafana. |

## 3. Service (Worker) задачи
1. Получить `IMonitoringCoordinator` из DI.
2. Выполнять мониторинг цикла каждые N мс (конфигурируемо, по умолчанию 2 секунды).
3. Логировать результаты, обнаруженные утечки, ретраи при ошибках.
4. Готовить данные для публикации (в дальнейшем UI hub/API/Prometheus).

## 4. Interfaces и минимальные реализации (итерация 1)
- `ProcessMetricSnapshot` — оперативная/виртуальная память (MB), handles, CPU % (TODO), timestamp.
- `LeakDetectionInsight` — флаг, вероятность, описание, delta vs baseline.
- `MonitoringCycleResult` — список снапшотов + инсайтов + метаданные цикла.
- `IProcessMetricsCollector` — сбор метрик с throttling и фильтрацией по allow-list/deny-list (пока full scan).
- `IBaselineRepository` — память baseline (exp smoothing).
- `ILeakDetectionStrategy` — простая эвристика: рост > 20% от baseline + slope тренда > threshold.
- `MonitoringCoordinator` — orchestrator, обрабатывает исключения и выдает `MonitoringCycleResult`.

## 5. Iteration plan
1. **Итерация 1 (текущая):** модели, интерфейсы, базовые реализации + DI в Service.
2. **Итерация 2:** расширенные метрики (GC, CPU), persist baseline, Prometheus exporter.
3. **Итерация 3:** интеграция с UI (stream), рекомендации, ML.NET/TraceEvent анализ.

## 6. Технические замечания
- Все проекты — .NET 8, целим x64/ARM64 (AnyCPU).
- Используем `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Options`.
- Исключаем прямые зависимости от UI/API: Core должен быть чистым.
- Для baseline/детекции — thread-safe коллекции (`ConcurrentDictionary`).
- Service worker: минимальная нагрузка CPU (<5%), поэтому коллекцию делаем с ограничением списка процессов (в дальнейшем конфиг).

Документ будет расширяться по мере итераций.

