using System;

public enum GameState
{
    Playing,
    Paused,
    Won,
    Lost
}

/// <summary>
/// Pure C# game state: playing / paused / won / lost plus the collected waste counter.
/// Winning takes both parts of the goal: collecting the target amount of waste and then reaching the end of the course
/// (<see cref="ReachGoal"/>), so the player always sees the whole level.
/// Kept free of Unity types so it can be unit tested in EditMode; <see cref="GameManager"/> wraps it in the scene.
/// Won and Lost are final: once reached, no further transition is accepted until a new session starts.
/// </summary>
public class GameSession
{
    public GameSession(int targetWaste)
    {
        if (targetWaste < 1) throw new ArgumentOutOfRangeException(nameof(targetWaste), "Target must be at least 1.");
        TargetWaste = targetWaste;
        State = GameState.Playing;
    }

    public GameState State { get; private set; }
    public int CollectedWaste { get; private set; }
    public int TargetWaste { get; }

    /// <summary>Raised with (collected, target) every time a waste item is collected.</summary>
    public event Action<int, int> WasteCollected;

    /// <summary>Raised after every state change with the new state.</summary>
    public event Action<GameState> StateChanged;

    /// <summary>Raised with the number of items still missing when the player reaches the goal too early.</summary>
    public event Action<int> GoalReachedWithMissingWaste;

    public bool HasEnoughWaste => CollectedWaste >= TargetWaste;
    public int MissingWaste => Math.Max(0, TargetWaste - CollectedWaste);

    /// <summary>Counts one waste item. Returns false if the game is not in the Playing state.</summary>
    public bool AddWaste()
    {
        if (State != GameState.Playing) return false;

        CollectedWaste++;
        WasteCollected?.Invoke(CollectedWaste, TargetWaste);
        return true;
    }

    /// <summary>
    /// The player reached the end of the course. Wins if enough waste was collected; otherwise raises
    /// <see cref="GoalReachedWithMissingWaste"/> and keeps playing. Returns true only when this call won the game.
    /// </summary>
    public bool ReachGoal()
    {
        if (State != GameState.Playing) return false;

        if (!HasEnoughWaste)
        {
            GoalReachedWithMissingWaste?.Invoke(MissingWaste);
            return false;
        }

        return Win();
    }

    public bool Win() => TransitionFrom(GameState.Playing, GameState.Won);
    public bool Lose() => TransitionFrom(GameState.Playing, GameState.Lost);
    public bool Pause() => TransitionFrom(GameState.Playing, GameState.Paused);
    public bool Resume() => TransitionFrom(GameState.Paused, GameState.Playing);

    private bool TransitionFrom(GameState expected, GameState next)
    {
        if (State != expected) return false;

        State = next;
        StateChanged?.Invoke(State);
        return true;
    }
}
