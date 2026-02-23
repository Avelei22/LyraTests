using System.Linq;
using AltTester.AltTesterSDK.Driver;
using LyraTests.Config;
using NUnit.Framework;

namespace LyraTests.Helpers;

public static class LyraMenuHelper
{
    public static void SetSessionBotCountViaUI(AltDriver driver, int desiredCount, double timeout = 5)
    {
        string? controlName = AltDriverConfig.BotCountControl;
        string? optionOneName = AltDriverConfig.BotCountOptionOne;
        if (string.IsNullOrEmpty(controlName))
            controlName = AltDriverConfig.BotCountControlDefault;
        try
        {
            AltObject? control = null;
            try { control = driver.WaitForObject(By.NAME, controlName, timeout: timeout, enabled: true); } catch { }
            if (control == null)
                try { control = driver.FindObjectWhichContains(By.NAME, controlName, enabled: true); } catch { }
            if (control != null)
            {
                Activate(driver, control);
                Thread.Sleep(AltDriverConfig.BotCountAfterControlClickMs);
            }

            if (desiredCount == 1)
            {
                if (!string.IsNullOrEmpty(optionOneName))
                {
                    var optionOne = driver.WaitForObject(By.NAME, optionOneName, timeout: timeout, enabled: true);
                    if (optionOne != null)
                    {
                        Activate(driver, optionOne);
                        Thread.Sleep(AltDriverConfig.BotCountAfterOptionClickMs);
                    }
                }
                else
                {
                    foreach (var tryName in AltDriverConfig.BotCountOptionOneFallbacks)
                    {
                        try
                        {
                            var o = driver.FindObject(By.NAME, tryName, enabled: true);
                            if (o != null) { Activate(driver, o); Thread.Sleep(AltDriverConfig.BotCountAfterOptionClickMs); break; }
                        }
                        catch { }
                        try
                        {
                            var o = driver.FindObjectWhichContains(By.NAME, tryName, enabled: true);
                            if (o != null) { Activate(driver, o); Thread.Sleep(AltDriverConfig.BotCountAfterOptionClickMs); break; }
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    public static string MainMenuScene => AltDriverConfig.MainMenuScene;
    public static string StartGameButton => AltDriverConfig.StartGameButton;
    public static string HostButton => AltDriverConfig.HostButton;
    public const string ButtonBorder = "ButtonBorder";
    public static string QuickPlayButton => AltDriverConfig.QuickPlayButton ?? AltDriverConfig.QuickPlayButtonDefault;
    const string SubmenuQuickplayButton = "QuickplayButton";

    static void Activate(AltDriver driver, AltObject o)
    {
        o.Tap();
        try { driver.PressKey(AltKeyCode.Return, 0.1f); } catch { }
        try { driver.PressKey(AltKeyCode.Space, 0.1f); } catch { }
        try { driver.PressKey(AltKeyCode.JoystickButton0, 0.1f); } catch { }
    }

    static void DismissOverlay(AltDriver driver)
    {
        try { driver.PressKey(AltKeyCode.Escape, 0.1f); } catch { }
        Thread.Sleep(AltDriverConfig.DismissOverlaySleepMs);
    }

    public static void GoToQuickPlay(AltDriver driver, double sceneTimeout = 15, int waitAfterSceneMs = 6000)
    {
        LoadMainMenu(driver, sceneTimeout, waitAfterSceneMs);
        ClickStartGameButton(driver);
        Thread.Sleep(AltDriverConfig.GoToQuickPlayAfterStartMs);
        ClickQuickPlay(driver);
    }

    public static void GoToControl(AltDriver driver, double sceneTimeout = 10, int waitAfterSceneMs = 5000)
    {
        LoadMainMenu(driver, sceneTimeout, waitAfterSceneMs);
        ClickStartGameButton(driver);
        ClickHostButton(driver);
        Thread.Sleep(AltDriverConfig.GoToControlAfterHostMs);
        ClickMiddleButtonBorder(driver);
    }

    public static void GoToHostWithTwoPlayers(AltDriver driver, double sceneTimeout = 10, int waitAfterSceneMs = 5000)
    {
        LoadMainMenu(driver, sceneTimeout, waitAfterSceneMs);
        ClickStartGameButton(driver, dismissOverlayFirst: false);
        ClickHostButton(driver);
        Thread.Sleep(AltDriverConfig.HostAfterClickSleepMs);
        SetSessionBotCountViaUI(driver, 1);
        Thread.Sleep(AltDriverConfig.HostAfterBotCountSleepMs);
        ClickExperienceButton(driver);
    }

    public static void LoadMainMenu(AltDriver driver, double sceneTimeout = 15, int waitAfterSceneMs = 8000)
    {
        driver.LoadScene(MainMenuScene);
        driver.WaitForCurrentSceneToBe(MainMenuScene, timeout: sceneTimeout);
        var scene = driver.GetCurrentScene();
        Assert.That(scene, Is.EqualTo(MainMenuScene), "LoadMainMenu: scene should be main menu after load.");
        Thread.Sleep(waitAfterSceneMs);
    }

    public static void ClickStartGameButton(AltDriver driver, double timeout = 30, bool dismissOverlayFirst = true)
    {
        if (dismissOverlayFirst)
            DismissOverlay(driver);
        var name = AltDriverConfig.PlayLyraButton ?? StartGameButton;
        int hostTimeout = AltDriverConfig.WaitForHostTimeoutSeconds;
        int afterClickMs = AltDriverConfig.StartGameAfterClickSleepMs;
        try
        {
            var btn = driver.WaitForObject(By.NAME, name, timeout: timeout, enabled: true);
            if (btn != null)
            {
                Activate(driver, btn);
                Thread.Sleep(afterClickMs);
                var host = driver.WaitForObject(By.NAME, HostButton, timeout: hostTimeout, enabled: true);
                Assert.That(host, Is.Not.Null, "StartGame did not open the main menu cards (HostButton not found).");
                return;
            }
        }
        catch (AssertionException) { throw; }
        catch { }

        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        int pollMs = AltDriverConfig.ExperienceWaitIntervalMs;
        while (DateTime.UtcNow < deadline)
        {
            AltObject? btn = TryFindPlayLyraButton(driver);
            if (btn != null)
            {
                Activate(driver, btn);
                Thread.Sleep(afterClickMs);
                var host = driver.WaitForObject(By.NAME, HostButton, timeout: hostTimeout, enabled: true);
                Assert.That(host, Is.Not.Null, "StartGame did not open the main menu cards (HostButton not found).");
                return;
            }
            Thread.Sleep(pollMs);
        }
        TryClickPlayLyraByScreenPosition(driver);
        Thread.Sleep(afterClickMs);
        var hostFallback = driver.WaitForObject(By.NAME, HostButton, timeout: hostTimeout, enabled: true);
        Assert.That(hostFallback, Is.Not.Null, "StartGame (position fallback) did not open the main menu cards.");
    }

    static AltObject? TryFindPlayLyraButton(AltDriver driver)
    {
        var fallbacks = new[] { StartGameButton }.Concat(AltDriverConfig.StartGameButtonFallbacks).Distinct().ToArray();
        var names = AltDriverConfig.PlayLyraButton != null
            ? new[] { AltDriverConfig.PlayLyraButton }.Concat(fallbacks).ToArray()
            : fallbacks;
        foreach (var name in names)
        {
            foreach (var enabled in new[] { true, false })
            {
                try
                {
                    var o = driver.FindObject(By.NAME, name, enabled: enabled);
                    if (o != null) return o;
                }
                catch { }
                try
                {
                    var o = driver.FindObjectWhichContains(By.NAME, name, enabled: enabled);
                    if (o != null) return o;
                }
                catch { }
            }
        }
        return null;
    }

    static void TryClickPlayLyraByScreenPosition(AltDriver driver)
    {
        try
        {
            var size = driver.GetApplicationScreenSize();
            if (size.x <= 0 || size.y <= 0) return;
            int x = (int)(size.x / 2);
            int y = (int)(size.y * 0.65);
            var v = new AltVector2(x, y);
            try { driver.Tap(v); } catch { driver.Click(v); }
        }
        catch { }
    }

    public static void ClickHostButton(AltDriver driver, double timeout = 15)
    {
        var btn = driver.WaitForObject(By.NAME, HostButton, timeout: timeout, enabled: true);
        Assert.That(btn, Is.Not.Null, "ClickHostButton: HostButton (START A GAME) not found.");
        Activate(driver, btn);
        var submenuVisible = driver.WaitForObject(By.NAME, SubmenuQuickplayButton, timeout: timeout, enabled: true);
        Assert.That(submenuVisible, Is.Not.Null, "ClickHostButton: submenu not open after activating START A GAME.");
    }

    public static void ClickMiddleButtonBorder(AltDriver driver, double timeoutSeconds = 15, double intervalSeconds = 0.5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        int lastCount = -1;
        while (DateTime.UtcNow < deadline)
        {
            System.Collections.Generic.List<AltObject>? buttons = null;
            try { buttons = driver.FindObjects(By.NAME, AltDriverConfig.ButtonBorder, enabled: true); } catch { }
            var byContain = driver.FindObjectsWhichContain(By.NAME, AltDriverConfig.ButtonBorder, enabled: true);
            if (buttons == null || (byContain != null && byContain.Count > (buttons?.Count ?? 0)))
                buttons = byContain;
            var count = buttons?.Count ?? 0;
            if (count >= 3)
            {
                var middleButton = buttons![Math.Min(AltDriverConfig.MiddleButtonBorderIndex, buttons!.Count - 1)];
                middleButton.Tap();
                return;
            }
            if (count != lastCount)
                lastCount = count;
            Thread.Sleep((int)(intervalSeconds * 1000));
        }
        var final = driver.FindObjects(By.NAME, ButtonBorder, enabled: true) ?? driver.FindObjectsWhichContain(By.NAME, ButtonBorder, enabled: true);
        var found = final?.Count ?? 0;
        Assert.Fail("Expected at least 3 ButtonBorder widgets within " + timeoutSeconds + "s. Found: " + found + ". Try checking the widget name in the game UI hierarchy.");
    }

    public static void ClickExperienceButton(AltDriver driver, double timeout = 10)
    {
        var btn = WaitForExperienceButton(driver, timeout);
        if (btn == null)
            btn = TryFindFirstExperienceCard(driver);
        Assert.That(btn, Is.Not.Null, "ClickExperienceButton: no experience tile found. Set ALTTESTER_EXPERIENCE_TILE_NAMES and ALTTESTER_EXPERIENCE_TILE_INDEX (and ALTTESTER_QUICKPLAY_EXCLUDE_NAMES if needed).");
        if (btn != null)
        {
            int beforeTapMs = AltDriverConfig.ExperienceBeforeTapMs;
            if (beforeTapMs > 0)
                Thread.Sleep(beforeTapMs);
            Activate(driver, btn);
            try
            {
                var pos = btn.GetScreenPosition();
                if (pos.x > 0 && pos.y > 0)
                {
                    var v = new AltVector2(pos.x, pos.y);
                    try { driver.Tap(v); } catch { }
                    Thread.Sleep(80);
                    try { driver.PressKey(AltKeyCode.Return, 0.12f); } catch { }
                }
            }
            catch { }
            Thread.Sleep(AltDriverConfig.ExperienceAfterClickSleepMs);
            TryClickLaunchOrStartButton(driver);
        }
    }

    static void TryClickLaunchOrStartButton(AltDriver driver)
    {
        foreach (var name in AltDriverConfig.LaunchButtonNames)
        {
            try
            {
                var o = driver.FindObject(By.NAME, name, enabled: true);
                if (o != null && !IsQuickPlayElement(o))
                {
                    Activate(driver, o);
                    return;
                }
            }
            catch { }
            try
            {
                var o = driver.FindObjectWhichContains(By.NAME, name, enabled: true);
                if (o != null && !IsQuickPlayElement(o))
                {
                    Activate(driver, o);
                    return;
                }
            }
            catch { }
        }
    }

    static AltObject? WaitForExperienceButton(AltDriver driver, double timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        int tileIndex = AltDriverConfig.ExperienceTileIndex;
        int intervalMs = AltDriverConfig.ExperienceWaitIntervalMs;
        while (DateTime.UtcNow < deadline)
        {
            var btn = TryFindExperienceTileByIndex(driver, tileIndex);
            if (btn != null) return btn;
            Thread.Sleep(intervalMs);
        }
        return null;
    }

    static AltObject? TryFindExperienceTileByIndex(AltDriver driver, int indexFromLeft)
    {
        var seen = new HashSet<int>();
        var list = new List<(float x, AltObject o)>();
        var nameSubstrings = AltDriverConfig.ExperienceTileNameSubstrings;
        float minY = AltDriverConfig.ExperienceMinScreenY;
        float bandPx = AltDriverConfig.ExperienceTileXBandPx;
        foreach (var name in nameSubstrings)
        {
            if (string.IsNullOrEmpty(name)) continue;
            try
            {
                var arr = driver.FindObjectsWhichContain(By.NAME, name, enabled: true);
                if (arr == null) continue;
                foreach (var o in arr)
                {
                    if (o?.name == null || seen.Contains(o.id) || IsQuickPlayElement(o)) continue;
                    bool nameMatch = false;
                    foreach (var sub in nameSubstrings)
                        if (o.name.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0) { nameMatch = true; break; }
                    if (!nameMatch) continue;
                    try
                    {
                        var pos = o.GetScreenPosition();
                        if (pos.x > 0 && pos.y > minY) { seen.Add(o.id); list.Add((pos.x, o)); }
                    }
                    catch { }
                }
            }
            catch { }
        }
        if (list.Count == 0) return null;
        var byTile = new Dictionary<int, List<(float x, AltObject o)>>();
        foreach (var t in list)
        {
            int band = (int)(t.x / bandPx);
            if (!byTile.ContainsKey(band)) byTile[band] = new List<(float x, AltObject o)>();
            byTile[band].Add(t);
        }
        var tilesOrdered = byTile.OrderBy(kv => kv.Value.Min(e => e.x)).Select(kv => kv.Value).ToList();
        if (tilesOrdered.Count == 0) return null;
        int idx = Math.Min(indexFromLeft, tilesOrdered.Count - 1);
        var tile = tilesOrdered[idx];
        tile.Sort((a, b) => a.x.CompareTo(b.x));
        return tile[0].o;
    }

    static bool IsQuickPlayElement(AltObject o)
    {
        var n = o.name ?? "";
        var quickPlayBtn = AltDriverConfig.QuickPlayButton ?? QuickPlayButton;
        if (!string.IsNullOrEmpty(quickPlayBtn) && n.Equals(quickPlayBtn, StringComparison.OrdinalIgnoreCase)) return true;
        if (n.Equals(SubmenuQuickplayButton, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var sub in AltDriverConfig.QuickPlayExcludeSubstrings)
            if (!string.IsNullOrEmpty(sub) && n.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    static AltObject? TryFindFirstExperienceCard(AltDriver driver)
    {
        return TryFindExperienceTileByIndex(driver, AltDriverConfig.ExperienceTileIndex);
    }

    public static void ClickQuickPlay(AltDriver driver, double timeout = 25)
    {
        Thread.Sleep(AltDriverConfig.QuickPlayBeforeWaitMs);

        var name = AltDriverConfig.QuickPlayButton ?? AltDriverConfig.QuickPlayButtonDefault;
        try
        {
            var btn = driver.WaitForObject(By.NAME, name, timeout: timeout);
            if (btn != null)
            {
                Activate(driver, btn);
                Thread.Sleep(AltDriverConfig.QuickPlayAfterClickSleepMs);
                return;
            }
        }
        catch { }

        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        int pollMs = AltDriverConfig.QuickPlayPollIntervalMs;
        while (DateTime.UtcNow < deadline)
        {
            AltObject? btn = TryFindQuickPlayButton(driver);
            if (btn != null)
            {
                Activate(driver, btn);
                Thread.Sleep(AltDriverConfig.QuickPlayAfterClickSleepMs);
                return;
            }
            Thread.Sleep(pollMs);
        }
        Assert.Fail("ClickQuickPlay: Quick Play button (name: " + name + ") not found within " + timeout + "s.");
    }

    static AltObject? TryFindQuickPlayButton(AltDriver driver)
    {
        var name = AltDriverConfig.QuickPlayButton ?? AltDriverConfig.QuickPlayButtonDefault;
        foreach (var enabled in new[] { true, false })
        {
            try
            {
                var o = driver.FindObject(By.NAME, name, enabled: enabled);
                if (o != null) return o;
            }
            catch { }
            try
            {
                var o = driver.FindObjectWhichContains(By.NAME, name, enabled: enabled);
                if (o != null) return o;
            }
            catch { }
        }
        return null;
    }
}
