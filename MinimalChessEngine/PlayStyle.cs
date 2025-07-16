namespace MinimalChessEngine
{
    public class PlayStyle
    {
        // These properties will be mapped directly from the JSON file.
        public string Name { get; set; }
        public int MaxDepth { get; set; }
        public int PanicThreshold { get; set; }
        public double PanicChance { get; set; }
        public double MaterialWeightMultiplier { get; set; }
        public int KingAttackBonus { get; set; }

        // Default constructor for a "Normal" style.
        public PlayStyle()
        {
            Name = "Normal";
            MaxDepth = 99;
            PanicThreshold = -10000;
            PanicChance = 0.0;
            MaterialWeightMultiplier = 1.0;
            KingAttackBonus = 0;
        }
    }
}