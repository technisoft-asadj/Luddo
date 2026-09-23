namespace Ludo.Game
{
    /// <summary>Every sound effect the game can ask for. Game code names the sound; the AudioLibrary decides which clip plays.</summary>
    public enum SfxId
    {
        Click,       // any button
        Back,        // a back arrow
        DiceRoll,    // dice starts shaking
        DiceLand,    // dice settles on its value
        Step,        // a token hops onto the next cell
        Capture,     // a token was sent back to base
        Home,        // a token reached the centre
        Win,         // the game is won
        Error        // a refused action
    }
}
