# Прогресс добавления комментариев к коду

## ✅ Полностью задокументированные файлы

### Core Layer
- `ThresholdLeakDetectionStrategy.cs` - основная стратегия обнаружения утечек
- `ProcessMetricSnapshot.cs` - модель снимка метрик процесса
- `ILeakDetectionStrategy.cs` - интерфейс стратегий обнаружения
- `ProcessBaseline.cs` - модель baseline процесса (частично)

### Service Layer
- `Program.cs` - точка входа и конфигурация DI
- `Worker.cs` - фоновый worker для мониторинга

## 📝 Частично задокументированные файлы

### Core Layer
- `MonitoringCoordinator.cs` - требует дополнительные комментарии к методам
- `InMemoryBaselineRepository.cs` - требует комментарии к алгоритмам вычисления
- `ProcessMetricsCollector.cs` - требует комментарии к сбору метрик
- `LeakSuspicionTracker.cs` - базовые комментарии есть, можно расширить

## 📋 Файлы, требующие документирования

### Core Layer
- Все интерфейсы в `Abstractions/`
- Все модели в `Models/`
- Все сервисы в `Services/`
- `Options/*.cs`

### Service Layer
- `Services/NamedPipeMonitoringPublisher.cs`

### UI Layer
- Все ViewModels
- Все Services
- Все Views (.xaml.cs)

## Рекомендации

Все публичные классы, методы и свойства должны иметь XML-документацию.
Сложные алгоритмы должны иметь пояснительные комментарии с объяснением логики.

