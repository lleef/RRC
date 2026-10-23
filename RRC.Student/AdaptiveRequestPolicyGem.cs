using System;
using RRC.Abstractions;

namespace RRC.Student;

public sealed class AdaptiveRequestPolicyGem : IRequestPolicy
{
    
    private double _estimatedLatencyMs = 100.0;


    private const double MinTimeoutMs = 80.0;
    private const double MaxTimeoutMs = 450.0;

    public TimeSpan GetTimeout(RequestContext context, Random random)
    {
       double multiplier = context.Attempt switch
        {
            1 => 1.8,
            2 => 2.4,
            _ => 3.0
        };

        double timeoutMs = _estimatedLatencyMs * multiplier;

        
        timeoutMs = Math.Clamp(timeoutMs, MinTimeoutMs, MaxTimeoutMs);
        double jitter = (random.NextDouble() * 0.2 - 0.1) * timeoutMs;

        return TimeSpan.FromMilliseconds(Math.Max(MinTimeoutMs, timeoutMs + jitter));
    }

    public RetryDecision OnResult(RequestObservation observation, Random random)
    {
        if (observation.Outcome == RequestOutcome.Success)
        {
            double elapsedMs = observation.Elapsed.TotalMilliseconds;

            
            _estimatedLatencyMs = _estimatedLatencyMs * 0.85 + elapsedMs * 0.15;

            return RetryDecision.NoRetry();
        }

      
        if (observation.Attempt < 4 && observation.Outcome == RequestOutcome.Timeout)
        {
             double baseDelayMs = observation.Attempt switch
            {
                1 => 100.0,
                2 => 200.0,
                _ => 400.0
            };

            double jitter = (random.NextDouble() * 0.4 - 0.2) * baseDelayMs;
            double delayMs = Math.Max(50.0, baseDelayMs + jitter);

            return RetryDecision.RetryAfter(TimeSpan.FromMilliseconds(delayMs));
        }

        return RetryDecision.NoRetry();
    }
}