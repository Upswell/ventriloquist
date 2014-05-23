using System;

namespace Ventriloquist
{
    public class Voice
    {

        public int VoiceId { get; set; }
        public string VoiceString { get; set; }
        public string Language { get; set; }
        public int Id { get; set; }
        private Config config = Config.GetInstance();

        public Voice()
        {

        }

        public void Save()
        {
            config.setVoice(VoiceId, Language, Id, VoiceString);
        }
    }
}
