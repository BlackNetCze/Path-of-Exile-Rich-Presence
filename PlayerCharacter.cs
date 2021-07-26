using Newtonsoft.Json;

namespace Path_of_Exile_Rich_Presence
{
    public class PlayerCharacter
    {
        public string name;
        public string league;
        public int classId;
        public int ascendancyClass;
        [JsonProperty(PropertyName = "class")]
        public string ascendancy;
        public int level;
        public long experience;
        public bool? lastActive;
    }
}
