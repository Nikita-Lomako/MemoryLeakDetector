using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MemoryLeakTestApp;

/// <summary>
/// Тестовое приложение для генерации утечек памяти.
/// Используется для тестирования MemoryLeak Detector.
/// </summary>
class Program
{
    private static readonly List<byte[]> _memoryLeaks = new();
    private static readonly List<System.Threading.Timer> _timerLeaks = new();
    private static readonly object _lock = new();
    private static bool _running = true;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Memory Leak Test Application");
        Console.WriteLine("============================");
        Console.WriteLine();
        Console.WriteLine("Это приложение намеренно создает утечки памяти для тестирования детектора.");
        Console.WriteLine("Запустите MemoryLeak Detector и наблюдайте за этим процессом.");
        Console.WriteLine();
        Console.WriteLine("Доступные команды:");
        Console.WriteLine("  [1] - Утечка managed памяти (byte arrays)");
        Console.WriteLine("  [2] - Утечка таймеров (timers)");
        Console.WriteLine("  [3] - Комбинированная утечка (оба типа)");
        Console.WriteLine("  [4] - Утечка с постепенным ростом");
        Console.WriteLine("  [q] - Выход");
        Console.WriteLine();
        Console.WriteLine("Выберите тип утечки (1-4 или q для выхода):");

        var cancellationTokenSource = new CancellationTokenSource();

        // Обработка команд
        _ = Task.Run(() => HandleCommands(cancellationTokenSource.Token));

        // Обработка завершения
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _running = false;
            cancellationTokenSource.Cancel();
            Console.WriteLine("\nЗавершение работы...");
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Нормальное завершение
        }

        Cleanup();
        Console.WriteLine("Приложение завершено.");
    }

    private static void HandleCommands(CancellationToken cancellationToken)
    {
        while (_running && !cancellationToken.IsCancellationRequested)
        {
            var key = Console.ReadKey(true);
            
            if (cancellationToken.IsCancellationRequested)
                break;

            switch (key.KeyChar)
            {
                case '1':
                    Console.WriteLine("Запущена утечка managed памяти...");
                    StartManagedMemoryLeak();
                    break;
                case '2':
                    Console.WriteLine("Запущена утечка таймеров...");
                    StartTimerLeak();
                    break;
                case '3':
                    Console.WriteLine("Запущена комбинированная утечка...");
                    StartCombinedLeak();
                    break;
                case '4':
                    Console.WriteLine("Запущена постепенная утечка...");
                    StartGradualLeak(cancellationToken);
                    break;
                case 'q':
                case 'Q':
                    Console.WriteLine("Завершение работы...");
                    _running = false;
                    return;
                default:
                    Console.WriteLine($"Неизвестная команда: {key.KeyChar}");
                    break;
            }
        }
    }

    private static void StartManagedMemoryLeak()
    {
        _ = Task.Run(() =>
        {
            while (_running)
            {
                // Создаем большие массивы и сохраняем ссылки на них
                var leak = new byte[10 * 1024 * 1024]; // 10 MB
                
                // Заполняем случайными данными, чтобы память реально использовалась
                Random.Shared.NextBytes(leak);

                lock (_lock)
                {
                    _memoryLeaks.Add(leak);
                    Console.WriteLine($"Создан memory leak #{_memoryLeaks.Count} (всего ~{_memoryLeaks.Count * 10} MB)");
                }

                Thread.Sleep(1000); // 1 секунда между утечками
            }
        });
    }

    private static void StartTimerLeak()
    {
        _ = Task.Run(() =>
        {
            while (_running)
            {
                // Создаем таймеры, которые не освобождаются
                var timer = new System.Threading.Timer(
                    _ => { /* таймер что-то делает */ },
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(1));

                lock (_lock)
                {
                    _timerLeaks.Add(timer);
                    Console.WriteLine($"Создан timer leak #{_timerLeaks.Count}");
                }

                Thread.Sleep(500); // 0.5 секунды между таймерами
            }
        });
    }

    private static void StartCombinedLeak()
    {
        StartManagedMemoryLeak();
        Thread.Sleep(500);
        StartTimerLeak();
    }

    private static void StartGradualLeak(CancellationToken cancellationToken)
    {
        _ = Task.Run(() =>
        {
            var iteration = 0;
            while (_running && !cancellationToken.IsCancellationRequested)
            {
                iteration++;
                
                // Постепенно увеличиваем размер утечки
                var size = (iteration % 10 + 1) * 5 * 1024 * 1024; // От 5 до 50 MB
                var leak = new byte[size];
                Random.Shared.NextBytes(leak);

                lock (_lock)
                {
                    _memoryLeaks.Add(leak);
                    var totalMb = _memoryLeaks.Count * (size / (1024.0 * 1024.0));
                    Console.WriteLine($"Постепенная утечка #{iteration}: +{size / (1024 * 1024)} MB (всего ~{totalMb:F0} MB)");
                }

                Thread.Sleep(2000); // 2 секунды между утечками
            }
        });
    }

    private static void Cleanup()
    {
        Console.WriteLine("\nОчистка ресурсов...");
        
        lock (_lock)
        {
            Console.WriteLine($"Освобождение {_memoryLeaks.Count} memory leaks...");
            _memoryLeaks.Clear();
            
            Console.WriteLine($"Освобождение {_timerLeaks.Count} timers...");
            foreach (var timer in _timerLeaks)
            {
                timer?.Dispose();
            }
            _timerLeaks.Clear();
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        Console.WriteLine("Очистка завершена.");
    }
}

