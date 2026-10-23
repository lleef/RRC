using RRC.Abstractions;

namespace RRC.Student;

public sealed class TSSHBRequestPolicy : IRequestPolicy
{
    private const int MaxAttempts = 3;

    private const double InitialTimeoutMs = 150;   
    private const double MinTimeoutMs = 30;
    private const double MaxTimeoutMs = 1000;
    private const double AttemptTimeoutGrowth = 1.25; 

    private const double ScaleUpOnTimeout = 1.15;     
    private const double ScaleDownOnSuccess = 0.995;  
    private const double MaxScale = 6.0;

    private const double RetryTokensInitial = 10;
    private const double RetryTokensMax = 20;
    private const double RetryTokensPerSuccess = 0.2; 

    private const double MaxDelayMs = 500;

    
    private bool _hasSample;
    private double _srtt;     
    private double _rttvar;   
    private double _scale = 1.0;
    private double _retryTokens = RetryTokensInitial;

    public TimeSpan GetTimeout(RequestContext context, Random random)
    {
        double baseMs = _hasSample ? _srtt + 4 * _rttvar : InitialTimeoutMs;
        double ms = baseMs * _scale * Math.Pow(AttemptTimeoutGrowth, context.Attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Clamp(ms, MinTimeoutMs, MaxTimeoutMs));
    }

    public RetryDecision OnResult(RequestObservation observation, Random random)
    {
        if (observation.Outcome == RequestOutcome.Success)
        {
           
            UpdateLatency(observation.Elapsed.TotalMilliseconds);

            _scale = Math.Max(1.0, _scale * ScaleDownOnSuccess);
            _retryTokens = Math.Min(RetryTokensMax, _retryTokens + RetryTokensPerSuccess);
            return RetryDecision.NoRetry();
        }

        // ---- Timeout ----
        _scale = Math.Min(MaxScale, _scale * ScaleUpOnTimeout);

        if (observation.Attempt >= MaxAttempts)
            return RetryDecision.NoRetry();

        if (_retryTokens < 1.0)
            return RetryDecision.NoRetry(); 

        _retryTokens -= 1.0;

        double baseDelayMs = _hasSample ? 0.5 * _srtt : 50;
        double delayMs = baseDelayMs * Math.Pow(2, observation.Attempt - 1);
        delayMs *= 0.5 + random.NextDouble(); 
        delayMs = Math.Min(delayMs, MaxDelayMs);

        return RetryDecision.RetryAfter(TimeSpan.FromMilliseconds(delayMs));
    }

    private void UpdateLatency(double sampleMs)
    {
        if (!_hasSample)
        {
            _srtt = sampleMs;
            _rttvar = sampleMs / 2;
            _hasSample = true;
            return;
        }

        _rttvar = 0.75 * _rttvar + 0.25 * Math.Abs(_srtt - sampleMs);
        _srtt = 0.875 * _srtt + 0.125 * sampleMs;
    }
}