using System;
using NUnit.Framework;

public class GameSessionTests
{
    [Test]
    public void NewSession_StartsPlayingWithZeroWaste()
    {
        var session = new GameSession(10);

        Assert.AreEqual(GameState.Playing, session.State);
        Assert.AreEqual(0, session.CollectedWaste);
        Assert.AreEqual(10, session.TargetWaste);
    }

    [Test]
    public void Constructor_RejectsTargetBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameSession(0));
    }

    [Test]
    public void AddWaste_RaisesEventWithCountAndTarget()
    {
        var session = new GameSession(3);
        int collected = -1, target = -1;
        session.WasteCollected += (c, t) => { collected = c; target = t; };

        session.AddWaste();

        Assert.AreEqual(1, collected);
        Assert.AreEqual(3, target);
    }

    [Test]
    public void CollectingTarget_AloneDoesNotWin()
    {
        var session = new GameSession(2);

        session.AddWaste();
        session.AddWaste();

        Assert.IsTrue(session.HasEnoughWaste);
        Assert.AreEqual(0, session.MissingWaste);
        Assert.AreEqual(GameState.Playing, session.State, "Winning also needs the goal at the end of the course.");
    }

    [Test]
    public void ReachGoal_WithEnoughWaste_WinsTheGame()
    {
        var session = new GameSession(2);
        session.AddWaste();
        session.AddWaste();

        Assert.IsTrue(session.ReachGoal());
        Assert.AreEqual(GameState.Won, session.State);
    }

    [Test]
    public void ReachGoal_WithWasteMissing_ReportsHowManyAndKeepsPlaying()
    {
        var session = new GameSession(10);
        for (int i = 0; i < 7; i++) session.AddWaste();
        int reported = -1;
        session.GoalReachedWithMissingWaste += missing => reported = missing;

        Assert.IsFalse(session.ReachGoal());

        Assert.AreEqual(3, reported);
        Assert.AreEqual(GameState.Playing, session.State);
    }

    [Test]
    public void ReachGoal_WhenNotPlaying_IsIgnored()
    {
        var session = new GameSession(1);
        session.AddWaste();
        session.Pause();
        bool raised = false;
        session.GoalReachedWithMissingWaste += _ => raised = true;

        Assert.IsFalse(session.ReachGoal());
        Assert.AreEqual(GameState.Paused, session.State);
        Assert.IsFalse(raised);
    }

    [Test]
    public void AddWaste_AfterWin_IsIgnored()
    {
        var session = new GameSession(1);
        session.AddWaste();
        session.ReachGoal();

        bool accepted = session.AddWaste();

        Assert.IsFalse(accepted);
        Assert.AreEqual(1, session.CollectedWaste);
    }

    [Test]
    public void Lose_FromPlaying_SetsLostAndRaisesStateChanged()
    {
        var session = new GameSession(10);
        GameState? raised = null;
        session.StateChanged += s => raised = s;

        Assert.IsTrue(session.Lose());

        Assert.AreEqual(GameState.Lost, session.State);
        Assert.AreEqual(GameState.Lost, raised);
    }

    [Test]
    public void WonAndLost_AreFinal()
    {
        var won = new GameSession(1);
        won.AddWaste();
        won.ReachGoal();
        Assert.IsFalse(won.Lose());
        Assert.IsFalse(won.Pause());
        Assert.AreEqual(GameState.Won, won.State);

        var lost = new GameSession(5);
        lost.Lose();
        Assert.IsFalse(lost.Win());
        Assert.IsFalse(lost.Resume());
        Assert.AreEqual(GameState.Lost, lost.State);
    }

    [Test]
    public void PauseAndResume_ToggleBetweenPlayingAndPaused()
    {
        var session = new GameSession(10);

        Assert.IsTrue(session.Pause());
        Assert.AreEqual(GameState.Paused, session.State);
        Assert.IsFalse(session.Pause(), "Pausing twice must be rejected.");

        Assert.IsTrue(session.Resume());
        Assert.AreEqual(GameState.Playing, session.State);
        Assert.IsFalse(session.Resume(), "Resuming while playing must be rejected.");
    }

    [Test]
    public void WhilePaused_WasteAndLoseAreIgnored()
    {
        var session = new GameSession(10);
        session.Pause();

        Assert.IsFalse(session.AddWaste());
        Assert.IsFalse(session.Lose());
        Assert.AreEqual(0, session.CollectedWaste);
        Assert.AreEqual(GameState.Paused, session.State);
    }
}
