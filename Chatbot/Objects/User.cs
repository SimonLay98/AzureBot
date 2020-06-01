using Microsoft.Bot.Schema;

namespace Chatbot.Objects
{
    public class User
    {
        public bool HasShownToken { get; set; }
        public TokenResponse TokenResponse { get; set; }
        public string Tenant { get; set; }
        //public UserInDb DbUserData { get; set; }
        public string Mail { get; set; }//uniqueName
        public string Name { get; set; }
    }
}