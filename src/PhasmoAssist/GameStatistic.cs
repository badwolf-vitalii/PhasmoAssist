namespace PhasmoAssist
{
    internal class GameStatistic
    {
        public int Huntings {  get; set; }
        public int GhostEvents {  get; set; }
        public int ShadeGhostEvents {  get; set; }
        public bool SingingGhostEvent {  get; set; }
        public bool ChangedLocations {  get; set; }
        public bool FuseBoxTurnedOff {  get; set; }

        public void Reset()
        {
            Huntings = 0;
            GhostEvents = 0;
            ShadeGhostEvents = 0;
            SingingGhostEvent = false;
            ChangedLocations = false;
            FuseBoxTurnedOff = false;
        }
    }
}
