using GameCore.HotUpdate.Battle.Logic;
using GameScale = GameCore.Scripts.TimeManager;

namespace ExiledToolkit.Game;

/// <summary>Sets how fast the game runs, for mods that hold a speed the speed buttons don't have. Toolkit 0.22.0 and newer.</summary>
public static class TimeScale
{
    /// <summary>
    /// The same writes the game's speed change makes: the scale it reads back, the scale Unity runs at, and the
    /// message that tells the page's timers and progress bars the new pace. The longest frame step is set the way the
    /// game's custom speed sets it, so a fast speed is not cut short by the step the game allows at its high speed;
    /// for the game's speeds and slower that is the step it already uses.
    /// </summary>
    public static void Hold(GameTimeManager time, float scale)
    {
        time.BattleTimeScale = scale;
        GameScale.SetBattleTimeScale(scale);
        time.ApplyGmMaxDeltaTime(scale);
        time.NotifyCountDownTimerScaleSync();
    }
}
