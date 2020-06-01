using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Chatbot.Database
{
    [Obsolete]
    public class UserInDb
    {
        [JsonProperty("uniqueName")]
        public string UniqueName { get; set; }
        [JsonProperty("landscapeObligationVmList")]
        public Dictionary<string, List<string>> LandscapeWithObligationVmList { get; set; } = new Dictionary<string, List<string>>();

    }
}
