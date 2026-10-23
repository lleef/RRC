using RRC.Abstractions;

namespace RRC.Simulation.Public;

// ---------- Дополнительные модели для своих тестов ----------

/// <summary>90% запросов быстрые (50..150 мс), 10% - медленный хвост (400..1000 мс).</summary>
public sealed class HeavyTailLatencyModel : ILatencyModel
{
    public TimeSpan GetBaseLatency(TimeSpan currentTime, Random random)
    {
        bool slow = random.NextDouble() < 0.10;
        double ms = slow
            ? 400 + random.NextDouble() * 600
            : 50 + random.NextDouble() * 100;
        return TimeSpan.FromMilliseconds(ms);
    }
}

/// <summary>Задержка 50..150 мс, но в окне [from, to) умножается на factor.</summary>
public sealed class StepLatencyModel : ILatencyModel
{
    private readonly TimeSpan _from;
    private readonly TimeSpan _to;
    private readonly double _factor;

    public StepLatencyModel(TimeSpan from, TimeSpan to, double factor)
    {
        _from = from;
        _to = to;
        _factor = factor;
    }

    public TimeSpan GetBaseLatency(TimeSpan currentTime, Random random)
    {
        double ms = 50 + random.NextDouble() * 100;
        if (currentTime >= _from && currentTime < _to)
            ms *= _factor;
        return TimeSpan.FromMilliseconds(ms);
    }
}

/// <summary>Периодические всплески: burstLength с высокой частотой, остальное время period - со спокойной.</summary>
public sealed class BurstArrivalModel : IArrivalModel
{
    private readonly TimeSpan _period;
    private readonly TimeSpan _burstLength;
    private readonly TimeSpan _burstInterval;
    private readonly TimeSpan _calmInterval;

    public BurstArrivalModel(double burstRps, double calmRps, TimeSpan burstLength, TimeSpan period)
    {
        _period = period;
        _burstLength = burstLength;
        _burstInterval = TimeSpan.FromSeconds(1.0 / burstRps);
        _calmInterval = TimeSpan.FromSeconds(1.0 / calmRps);
    }

    public TimeSpan GetNextDelay(TimeSpan currentTime, Random random)
    {
        var phase = TimeSpan.FromTicks(currentTime.Ticks % _period.Ticks);
        return phase < _burstLength ? _burstInterval : _calmInterval;
    }
}

// ---------- Сценарии ----------

public static class StressScenarios
{
    private static readonly TimeSpan Generation = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Simulation = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Тот же поток и задержки, что в открытом сценарии, но ёмкость 12.
    /// Без ретраев сервер справляется (в среднем ~10 попыток в работе), а лишние
    /// повторы толкают его за границу ёмкости, и задержка растёт.
    /// </summary>
    public static SimulationScenario RetryStorm() => new SimulationScenario(
        "Stress: near capacity (retry storm)",
        Generation, Simulation,
        new ConstantRateArrivalModel(100),
        new SimpleRandomLatencyModel(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(150)),
        new CapacityLoadModel(12),
        "100 req/s, uniform 50..150 ms, capacity 12");

    /// <summary>10% запросов в 4-10 раз медленнее обычных. Ёмкость с запасом.</summary>
    public static SimulationScenario HeavyTail() => new SimulationScenario(
        "Stress: heavy tail",
        Generation, Simulation,
        new ConstantRateArrivalModel(100),
        new HeavyTailLatencyModel(),
        new CapacityLoadModel(30),
        "100 req/s, 90% 50..150 ms + 10% 400..1000 ms, capacity 30");

    /// <summary>С 10-й по 20-ю секунду задержка в 3 раза выше.</summary>
    public static SimulationScenario LatencyStep() => new SimulationScenario(
        "Stress: latency step x3",
        Generation, Simulation,
        new ConstantRateArrivalModel(100),
        new StepLatencyModel(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), 3.0),
        new CapacityLoadModel(60),
        "100 req/s, 50..150 ms, x3 between 10s and 20s, capacity 60");

    /// <summary>Каждые 4 секунды 1 секунда на 250 req/s, остальное время 40 req/s.</summary>
    public static SimulationScenario Bursts() => new SimulationScenario(
        "Stress: bursts",
        Generation, Simulation,
        new BurstArrivalModel(250, 40, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4)),
        new SimpleRandomLatencyModel(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(150)),
        new CapacityLoadModel(20),
        "bursts 250 req/s for 1s every 4s, otherwise 40 req/s, capacity 20");
}
