using System;

namespace Ventriloquist
{
    public class TTSRequest
    {

        public string Text { get; set; }
        public int Speed { get; set; }
        public string Language { get; set; }
        public int Voice { get; set; }
        public bool Interrupt { get; set; }

        public TTSRequest()
        {
            Text = "";
            Speed = 5;
            Language = "en";
            Voice = 1;
            Interrupt = false;
        }
    }
}
