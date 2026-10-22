using System;

public enum GameState
{
    Playing,
    Paused,
    Won,
    Lost
}

/// <summary>Why a session ended in <see cref="GameState.Lost"/>.</summary>
public enum LoseReason
{
    None,
    TimeUp,
    Other
}

/// <summary>
/// Pure C# game state: playing / paused / won / lost plus the collected waste counter.
/// Winning takes both parts of the goal: collecting the target amount of waste and then reaching the end of the course
/// (<see cref="ReachGoal"/>), so the player always sees the whole level.
/// Kept free of Unity types so it can be unit tested in EditMode; <see cref="GameManager"/> wraps it in the scene.
/// Won and Lost are final: once reached, no further transition is accepted until a new session starts.
/// With a time limit, the clock (<see cref="Tick"/>) only runs while Playing, and reaching zero loses the game
/// (<see cref="LoseReason.TimeUp"/>).
/// </summary>
public class GameSession
{
    /// <param name="targetWaste">Waste needed to win, at least 1.</param>
    /// <param name="timeLimitSeconds">Seconds the player has to win; 0 or less means no limit.</param>
    public GameSession(int targetWaste, float timeLimitSeconds = 0f)
    {
        if (targetWaste < 1) throw new ArgumentOutOfRangeException(nameof(targetWaste), "Target must be at least 1.");
        TargetWaste = targetWaste;
        TimeLimit = Math.Max(0f, timeLimitSeconds);
        TimeRemaining = TimeLimit;
        State = GameState.Playing;
    }

    public GameState State { get; private set; }
    public int CollectedWaste { get; private set; }
    public int TargetWaste { get; }

    /// <summary>Seconds the player has to win, or 0 when the level has no time limit.</summary>
    public float TimeLimit { get; }
    public bool HasTimeLimit => TimeLimit > 0f;

    /// <summary>Seconds left on the clock (stays at <see cref="TimeLimit"/>, 0, when there is no limit).</summary>
    public float TimeRemaining { get; private set; }

    /// <summary>Seconds played so far, pauses excluded.</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>Why the game was lost; <see cref="LoseReason.None"/> unless the state is Lost.</summary>
    public LoseReason LoseReason { get; private set; }

    /// <summary>Raised with the seconds left every time the clock moves.</summary>
    public event Action<float> TimeChanged;

    /// <summary>Raised once when the clock reaches zero, just before the game is lost.</summary>
    public event Action TimeRanOut;

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

    /// <summary>
    /// Advances the clock by <paramref name="deltaSeconds"/> of play. Does nothing unless Playing; with a time limit,
    /// reaching zero raises <see cref="TimeRanOut"/> and loses the game. Returns true only when this call lost the game.
    /// </summary>
    public bool Tick(float deltaSeconds)
    {
        if (State != GameState.Playing || deltaSeconds <= 0f) return false;

        ElapsedTime += deltaSeconds;
        if (!HasTimeLimit) return false;

        TimeRemaining = Math.Max(0f, TimeRemaining - deltaSeconds);
        TimeChanged?.Invoke(TimeRemaining);
        if (TimeRemaining > 0f) return false;

        TimeRanOut?.Invoke();
        return Lose(LoseReason.TimeUp);
    }

    public bool Win() => TransitionFrom(GameState.Playing, GameState.Won);
    public bool Lose() => Lose(LoseReason.Other);

    public bool Lose(LoseReason reason)
    {
        if (State != GameState.Playing) return false;

        LoseReason = reason == LoseReason.None ? LoseReason.Other : reason;
        return TransitionFrom(GameState.Playing, GameState.Lost);
    }

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
