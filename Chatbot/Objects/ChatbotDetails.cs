

namespace Chatbot.Objects
{
    public class ChatbotDetails
    {
        public string VmName { get; set; }
        public string LandscapeTag { get; set; }
        public bool RunWithCompleteLandscape { get; set; }
        public bool OnlyObligationVms { get; set; }
        public User User { get; set; }
        public Luis.AzureBotLuis.Intent Intent { get; set; }
        public bool RecursionDialog { get; set; }
    }
}
