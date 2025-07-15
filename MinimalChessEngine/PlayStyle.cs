namespace MinimalChessEngine
{
    public class PlayStyle
    {
        public string Name { get; }
        public int MaxDepth { get; }

        // ADD NEW PARAMETERS FOR THE CHEATER STYLE
        public int PanicThreshold { get; } // Score below which the engine might panic.
        public double PanicChance { get; }  // Probability (0.0 to 1.0) of panicking when losing.

        public PlayStyle(string name, int maxDepth, int panicThreshold = -10000, double panicChance = 0.0)
        {
            Name = name;
            MaxDepth = maxDepth;
            PanicThreshold = panicThreshold;
            PanicChance = panicChance;
        }

        // Define static instances for each play style
        public static readonly PlayStyle Normal = new PlayStyle("Normal", 64);
        public static readonly PlayStyle Easy = new PlayStyle("Easy", 5);

        // ADD THE NEW STYLE
        // It plays at Medium (depth 8) normally.
        // If the score drops below -1.5 pawns (-150), it has a 75% chance to panic.
        public static readonly PlayStyle Cheater = new PlayStyle("TheChessDotComCheater", 8, -150, 0.75);
    }
}