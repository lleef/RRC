using System;
using RRC.Abstractions;

namespace RRC.Simulation.Public;


public sealed class StepSpikeArrivalModel : IArrivalModel
{
    private readonly TimeSpan _spikeStart;
    private readonly TimeSpan _spikeDuration;
    private readonly double _normalRps;
    private readonly double _spikeRps;

    private readonly TimeSpan _normalInterval;
    private readonly TimeSpan _spikeInterval;

    public StepSpikeArrivalModel(TimeSpan spikeStart, TimeSpan spikeDuration, double normalRps, double spikeRps)
    {
        _spikeStart = spikeStart;
        _spikeDuration = spikeDuration;
        _normalRps = normalRps;
        _spikeRps = spikeRps;
        _normalInterval = TimeSpan.FromSeconds(1.0 / normalRps);
        _spikeInterval = TimeSpan.FromSeconds(1.0 / spikeRps);
    }

    public TimeSpan GetNextDelay(TimeSpan currentTime, Random random)
    {
        bool isSpike = currentTime >= _spikeStart && currentTime < (_spikeStart + _spikeDuration);
        return isSpike ? _spikeInterval : _normalInterval;
    }
}


public sealed class FlakyLatencyModel : ILatencyModel
{
    private readonly TimeSpan _outagePeriod;
    private readonly TimeSpan _outageDuration;
    private readonly double _baseMinMs;
    private readonly double _baseMaxMs;

    public FlakyLatencyModel(TimeSpan outagePeriod, TimeSpan outageDuration, double baseMinMs = 50, double baseMaxMs = 150)
    {
        _outagePeriod = outagePeriod;
        _outageDuration = outageDuration;
        _baseMinMs = baseMinMs;
        _baseMaxMs = baseMaxMs;
    }

    public TimeSpan GetBaseLatency(TimeSpan currentTime, Random random)
    {
        double totalSeconds = currentTime.TotalSeconds;
        double periodSeconds = _outagePeriod.TotalSeconds;
        double remainder = totalSeconds % periodSeconds;

        if (remainder < _outageDuration.TotalSeconds)
        {
            return TimeSpan.FromMilliseconds(1200 + random.NextDouble() * 800);
        }

       
        double ms = _baseMinMs + random.NextDouble() * (_baseMaxMs - _baseMinMs);
        return TimeSpan.FromMilliseconds(ms);
    }
}


public static partial class StressScenariosgg
{
   
    public static SimulationScenario StepSpikeStress() => new SimulationScenario(
        "Stress: sudden traffic spike + low capacity",
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(40),
        new StepSpikeArrivalModel(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), 40, 300),
        new SimpleRandomLatencyModel(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(150)),
        new CapacityLoadModel(10),
        "Spike to 300 req/s between 10s and 15s, capacity 10");

    
    public static SimulationScenario FlakyServerStress() => new SimulationScenario(
        "Stress: flaky intermittent outage",
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(40),
        new ConstantRateArrivalModel(80),
        new FlakyLatencyModel(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(2)),
        new CapacityLoadModel(25),
        "80 req/s, server blacks out for 2s every 8s, capacity 25");
}