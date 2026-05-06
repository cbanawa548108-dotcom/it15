using System.Collections.Concurrent;

namespace CRLFruitstandESS.Services
{
    /// <summary>
    /// In-memory rate limiter for the login endpoint.
    ///
    /// How it works:
    ///   - Tracks failed login attempts per IP address in a thread-safe dictionary.
    ///   - After 10 failed attempts within a 10-minute window, the IP is blocked
    ///     for 10 minutes regardless of which username is being tried.
    ///   - This is separate from ASP.NET Identity's per-user lockout (5 attempts / 15 min)
    ///     and protects against credential-stuffing attacks that rotate usernames.
    ///   - Successful login resets the counter for that IP.
    ///   - A background cleanup removes stale entries every 5 minutes.
    /// </summary>
    public class LoginRateLimiter
    {
        private record AttemptRecord(int Count, DateTime WindowStart, DateTime? BlockedUntil);

        private readonly ConcurrentDictionary<string, AttemptRecord> _attempts = new();
        private readonly int    _maxAttempts   = 10;
        private readonly int    _windowMinutes = 10;
        private readonly int    _blockMinutes  = 10;
        private          DateTime _lastCleanup = DateTime.UtcNow;

        /// <summary>
        /// Returns true if the IP is currently rate-limited (too many failed attempts).
        /// </summary>
        public bool IsBlocked(string ipAddress)
        {
            CleanupIfNeeded();

            if (_attempts.TryGetValue(ipAddress, out var record))
            {
                if (record.BlockedUntil.HasValue && record.BlockedUntil > DateTime.UtcNow)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns how many seconds remain on the block, or 0 if not blocked.
        /// </summary>
        public int BlockedSecondsRemaining(string ipAddress)
        {
            if (_attempts.TryGetValue(ipAddress, out var record) &&
                record.BlockedUntil.HasValue && record.BlockedUntil > DateTime.UtcNow)
            {
                return (int)(record.BlockedUntil.Value - DateTime.UtcNow).TotalSeconds;
            }
            return 0;
        }

        /// <summary>
        /// Records a failed login attempt for the given IP.
        /// Applies a block if the threshold is exceeded.
        /// </summary>
        public void RecordFailure(string ipAddress)
        {
            var now = DateTime.UtcNow;

            _attempts.AddOrUpdate(ipAddress,
                // New entry
                _ => new AttemptRecord(1, now, null),
                // Update existing
                (_, existing) =>
                {
                    // Reset window if it has expired
                    if ((now - existing.WindowStart).TotalMinutes > _windowMinutes)
                        return new AttemptRecord(1, now, null);

                    var newCount = existing.Count + 1;
                    DateTime? blockedUntil = newCount >= _maxAttempts
                        ? now.AddMinutes(_blockMinutes)
                        : existing.BlockedUntil;

                    return new AttemptRecord(newCount, existing.WindowStart, blockedUntil);
                });
        }

        /// <summary>
        /// Clears the failure counter for an IP on successful login.
        /// </summary>
        public void RecordSuccess(string ipAddress)
        {
            _attempts.TryRemove(ipAddress, out _);
        }

        /// <summary>
        /// Returns the current failure count for an IP (for display purposes).
        /// </summary>
        public int GetFailureCount(string ipAddress)
        {
            return _attempts.TryGetValue(ipAddress, out var r) ? r.Count : 0;
        }

        private void CleanupIfNeeded()
        {
            if ((DateTime.UtcNow - _lastCleanup).TotalMinutes < 5) return;
            _lastCleanup = DateTime.UtcNow;

            var staleKeys = _attempts
                .Where(kv => kv.Value.BlockedUntil < DateTime.UtcNow &&
                             (DateTime.UtcNow - kv.Value.WindowStart).TotalMinutes > _windowMinutes * 2)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in staleKeys)
                _attempts.TryRemove(key, out _);
        }
    }
}
