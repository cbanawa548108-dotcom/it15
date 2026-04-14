namespace CRLFruitstandESS.Models.ViewModels
{
    public class DashboardViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public DateTime? LastLoginAt { get; set; }
        public string WelcomeMessage { get; set; } = string.Empty;
    }
}