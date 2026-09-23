using System.Runtime.CompilerServices;

// Lets the test assembly set up board positions directly. No other assembly can mutate GameState.
[assembly: InternalsVisibleTo("Ludo.Tests.EditMode")]
