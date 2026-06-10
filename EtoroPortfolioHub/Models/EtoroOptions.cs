namespace EtoroPortfolioHub.Models
{
    public class EtoroOptions
    {

        public const string SectionName = "eToro";

        public string BaseUrl { get; set; } = "https://public-api.etoro.com/api/v1";
        public string WebSocketUrl { get; set; } = "wss://ws.etoro.com/ws";
        public string ApiKey { get; set; } = string.Empty;
        public string UserKey { get; set; } = string.Empty;
        public string Environment { get; set; } = "Real"; // Real | Demo
        public int RefreshIntervalSeconds { get; set; } = 60;

    }
}
