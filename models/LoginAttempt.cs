namespace CRLFruitstandESS.Models
{
    /// <summary>
    /// Records every login attempt (success or failure) for security auditing.
    /// </summary>
    public class LoginAttempt
    {
        public int      Id          { get; set; }
        public string   UserName    { get; set; } = string.Empty;
        public string   IpAddress   { get; set; } = string.Empty;
        public string   UserAgent   { get; set; } = string.Empty;
        public bool     Succeeded   { get; set; }
        public string   FailReason  { get; set; } = string.Empty; // "InvalidPassword" | "LockedOut" | "Inactive"
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    }
}
