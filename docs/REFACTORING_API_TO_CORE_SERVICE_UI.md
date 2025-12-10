# Рефакторинг функционала API в Core, Service и UI

## Обзор
Функционал из проекта `MemoryLeakDetector.API` был перенесен в соответствующие проекты согласно принципам чистой архитектуры и SOLID.

## Изменения

### 1. MemoryLeakDetector.Core

#### Добавленные абстракции:
- **`IMonitoringHistoryStore`** (`Core/Abstractions/IMonitoringHistoryStore.cs`)
  - Интерфейс для хранения истории результатов мониторинга
  - Методы: `Add`, `GetLatest`, `GetRange`

- **`IReportGenerator`** (`Core/Abstractions/IReportGenerator.cs`)
  - Интерфейс для генерации отчетов в различных форматах
  - Методы: `GenerateJson`, `GenerateHtml`, `GeneratePdf`

#### Добавленные реализации:
- **`InMemoryMonitoringHistoryStore`** (`Core/Services/History/InMemoryMonitoringHistoryStore.cs`)
  - In-memory реализация хранилища истории
  - Ограничение по количеству элементов (по умолчанию 1000)

#### Добавленные модели:
- **`MonitoringReportModel`** (`Core/Models/MonitoringReportModel.cs`)
  - Модель данных для отчетов
  - Независима от UI/API слоев

#### Обновления:
- **`ServiceCollectionExtensions`** - добавлена регистрация `IMonitoringHistoryStore`

### 2. MemoryLeakDetector.Service

#### Изменения:
- **`Worker.cs`**
  - Добавлена зависимость от `IMonitoringHistoryStore`
  - Результаты мониторинга теперь сохраняются в историю в дополнение к публикации в поток
  - Комментарии объясняют назначение каждой операции

### 3. MemoryLeakDetector.UI

#### Добавленные сервисы:
- **`MonitoringReportGenerator`** (`UI/Services/Reporting/MonitoringReportGenerator.cs`)
  - Реализация `IReportGenerator` для UI
  - Генерация отчетов в форматах JSON, HTML, PDF
  - Использует QuestPDF для генерации PDF

- **`InMemoryHistoryProvider`** (`UI/Services/Reporting/InMemoryHistoryProvider.cs`)
  - Провайдер истории для UI
  - Накапливает данные из потока результатов мониторинга
  - Предоставляет методы для получения истории и утечек

#### Изменения:
- **`MonitoringResultListener.cs`**
  - Добавлена интеграция с `InMemoryHistoryProvider`
  - Все результаты мониторинга теперь накапливаются в истории

- **`App.xaml.cs`**
  - Регистрация новых сервисов: `InMemoryHistoryProvider` и `IReportGenerator`

#### Зависимости:
- Добавлен пакет `QuestPDF` версии 2024.3.0

## Архитектурные принципы

### Разделение ответственности:
- **Core** - бизнес-логика, абстракции, модели данных
- **Service** - фоновый мониторинг и сохранение истории
- **UI** - визуализация и генерация отчетов для пользователя

### Dependency Inversion:
- Все зависимости направлены на абстракции (`Core/Abstractions`)
- Конкретные реализации в соответствующих проектах

### Single Responsibility:
- Каждый класс имеет одну четкую ответственность
- Интерфейсы определены на уровне абстракций

## Использование

### В Service:
```csharp
// Worker автоматически сохраняет результаты в IMonitoringHistoryStore
// Доступно через DI контейнер
```

### В UI:
```csharp
// Получение истории
var historyProvider = serviceProvider.GetRequiredService<InMemoryHistoryProvider>();
var latest = historyProvider.GetLatest();
var range = historyProvider.GetRange(from, to);

// Генерация отчетов
var reportGenerator = serviceProvider.GetRequiredService<IReportGenerator>();
var html = reportGenerator.GenerateHtml(model);
var pdf = reportGenerator.GeneratePdf(model);
var json = reportGenerator.GenerateJson(model);
```

## Будущие улучшения

1. **Расширение IMonitoringHistoryStore**:
   - Добавить поддержку персистентного хранилища (база данных)
   - Добавить фильтрацию и пагинацию

2. **Интеграция UI с Service**:
   - Добавить Named Pipe endpoint для получения полной истории из Service
   - Синхронизация истории между Service и UI

3. **Оптимизация отчетов**:
   - Кэширование сгенерированных отчетов
   - Асинхронная генерация для больших отчетов

## Примечания

- Проект `MemoryLeakDetector.API` остается в solution, но больше не используется
- Все функциональные требования из API проекта перенесены
- Сохранена обратная совместимость через абстракции

