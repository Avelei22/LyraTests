using AltTester.AltTesterSDK.Driver;
using LyraTests.Config;
using LyraTests.Helpers;
using NUnit.Framework;

namespace LyraTests.Helpers;

public static class GameplayHelper
{
    public class MoveTowardOptions
    {
        public double TimeoutSeconds { get; set; } = 30;
        public float MoveBurstSeconds { get; set; } = 0.8f;
        public float CloseEnoughDistance { get; set; } = 400f;
        public float StuckDistanceThreshold { get; set; } = 15f;
        public int StuckBurstsBeforeStrafe { get; set; } = 2;
        public float StrafeSeconds { get; set; } = 0.4f;
        public double AimTimeoutPerBurst { get; set; } = 3;
        public int PollMs { get; set; } = 150;
        public int MaxMoveIterations { get; set; } = 0;
    }
    public static void EnterGameplay(AltDriver driver, double gameplayTimeoutSeconds = 60, bool useMenuOnly = false)
    {
        string? aimTestMap = AltDriverConfig.AimTestMap;
        if (!string.IsNullOrEmpty(aimTestMap))
        {
            driver.LoadScene(aimTestMap);
            driver.WaitForCurrentSceneToBe(aimTestMap, timeout: gameplayTimeoutSeconds);
            var scene = driver.GetCurrentScene();
            Assert.That(scene, Is.Not.Null.And.Not.Empty, "EnterGameplay: scene should be set after LoadScene.");
            Assert.That(scene, Is.EqualTo(aimTestMap), "EnterGameplay: expected scene " + aimTestMap + ", got " + scene);
            Thread.Sleep(500);
            _ = AimingHelper.TrySetLocalPlayerInvincible(driver, true);
            _ = AimingHelper.TrySetLocalPlayerInfiniteAmmo(driver, true);
            AimingHelper.EnsureTestCheatsApplied(driver, bEnable: true, maxAttempts: 5, delayMs: 1000);
            Thread.Sleep(1000);
            double pawnWaitSeconds = Math.Min(60, gameplayTimeoutSeconds);
            AltObject? pawn = PlayerFinder.TryWaitForPlayerPawn(driver, timeoutSeconds: pawnWaitSeconds);
            double postLobbySeconds = 30;
            var postDeadline = DateTime.UtcNow.AddSeconds(postLobbySeconds);
            while (DateTime.UtcNow < postDeadline)
            {
                _ = AimingHelper.TrySetLocalPlayerInvincible(driver, true);
                _ = AimingHelper.TrySetLocalPlayerInfiniteAmmo(driver, true);
                if (pawn == null)
                    pawn = PlayerFinder.GetPlayerPawn(driver);
                if (pawn != null)
                    break;
                Thread.Sleep(2000);
            }
            Thread.Sleep(2000);
            _ = AimingHelper.TrySetLocalPlayerInvincible(driver, true);
            _ = AimingHelper.TrySetLocalPlayerInfiniteAmmo(driver, true);
            AimingHelper.EnsureTestCheatsApplied(driver, bEnable: true, maxAttempts: 5, delayMs: 1500);
            Thread.Sleep(500);
            return;
        }
        if (useMenuOnly)
            LyraMenuHelper.GoToHostWithTwoPlayers(driver, sceneTimeout: 15, waitAfterSceneMs: 5000);
        else
            LyraMenuHelper.GoToQuickPlay(driver);
        WaitForGameplayToStart(driver, timeoutSeconds: gameplayTimeoutSeconds);
        var sceneAfter = driver.GetCurrentScene();
        Assert.That(sceneAfter, Is.Not.Null.And.Not.Empty, "EnterGameplay: scene should be set after gameplay start.");
        var inGameplay = sceneAfter != LyraMenuHelper.MainMenuScene || FindPlayerCharacter(driver) != null || FindHUD(driver) != null;
        Assert.That(inGameplay, Is.True, "EnterGameplay: expected scene change or player/HUD (match started).");
        Thread.Sleep(1500);
    }

    public static bool SetupInGameTest(AltDriver driver, double waitSeconds = 30)
    {
        _ = AimingHelper.TrySetLocalPlayerInvincible(driver, true);
        _ = AimingHelper.TrySetLocalPlayerInfiniteAmmo(driver, true);
        AimingHelper.EnsureTestCheatsApplied(driver, bEnable: true, maxAttempts: AltDriverConfig.SetupCheatMaxAttempts, delayMs: AltDriverConfig.SetupCheatDelayMs);
        if (waitSeconds > 0)
            Thread.Sleep((int)(waitSeconds * 1000));
        int pollMs = AltDriverConfig.SetupPlayerWaitPollMs;
        int maxIter = AltDriverConfig.SetupPlayerWaitMaxIterations;
        for (int k = 0; k < maxIter; k++)
        {
            if (FindPlayerCharacter(driver) != null) break;
            Thread.Sleep(pollMs);
        }
        Thread.Sleep(AltDriverConfig.SetupAfterPlayerWaitSleepMs);
        _ = AimingHelper.TrySetLocalPlayerInvincible(driver, true);
        _ = AimingHelper.TrySetLocalPlayerInfiniteAmmo(driver, true);
        AimingHelper.EnsureTestCheatsApplied(driver, bEnable: true, maxAttempts: AltDriverConfig.SetupCheatMaxAttempts, delayMs: AltDriverConfig.SetupCheatDelayMs);
        return AimingHelper.TrySetLocalPlayerInvincible(driver, true);
    }

    public static void TeardownInGameTest(AltDriver driver)
    {
        AimingHelper.TrySetLocalPlayerInvincible(driver, false);
        AimingHelper.TrySetLocalPlayerInfiniteAmmo(driver, false);
    }

    public static bool TeleportPlayerTo(AltDriver driver, float x, float y, float z)
    {
        var pawn = FindPlayerCharacter(driver);
        if (pawn == null) return false;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var vecFormats = new[]
        {
            $"(X={x.ToString(inv)},Y={y.ToString(inv)},Z={z.ToString(inv)})",
            $"X={x.ToString(inv)} Y={y.ToString(inv)} Z={z.ToString(inv)}"
        };
        foreach (var comp in new[] { "Actor", "Pawn", "Character", pawn.type, "LyraCharacter" })
        {
            if (string.IsNullOrEmpty(comp)) continue;
            foreach (var asm in new[] { "Engine", "LyraGame" })
            {
                foreach (var vecStr in vecFormats)
                {
                    try
                    {
                        pawn.CallComponentMethod<object>(comp, "K2_SetActorLocation", asm, new object[] { vecStr, false }, new string[] { "System.String", "System.Boolean" });
                        return true;
                    }
                    catch { }
                    try
                    {
                        pawn.CallComponentMethod<object>(comp, "SetActorLocation", asm, new object[] { vecStr }, new string[] { "System.String" });
                        return true;
                    }
                    catch { }
                }
                try
                {
                    pawn.CallComponentMethod<object>(comp, "K2_SetActorLocation", asm, new object[] { x, y, z, false }, new string[] { "System.Single", "System.Single", "System.Single", "System.Boolean" });
                    return true;
                }
                catch { }
            }
        }
        return false;
    }

    public static bool WaitForGameplayScene(AltDriver driver, double timeoutSeconds = 60, double pollIntervalSeconds = 0.5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var scene = driver.GetCurrentScene();
                if (!string.IsNullOrEmpty(scene) && scene != LyraMenuHelper.MainMenuScene)
                    return true;
            }
            catch (CommandResponseTimeoutException) { }
            Thread.Sleep((int)(pollIntervalSeconds * 1000));
        }
        return false;
    }

    private static readonly string[] PawnNamePatterns = { "LyraCharacter", "SimpleHeroPawn", "SimpleHero", "Character", "Pawn" };
    private static readonly string[] HudNamePatterns = { "LyraHUD", "HUD" };

    private static bool IsEnemyCharacterType(AltObject o)
    {
        if (string.IsNullOrEmpty(o.type)) return false;
        var t = o.type;
        var n = o.name ?? "";
        if (t.Contains("Pickup", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Spawner", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Spawning", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Respawn", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Timer", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Widget", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("W_", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Item", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Weapon", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Ability", StringComparison.OrdinalIgnoreCase))
            return false;
        if (n.Contains("Spawning", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Respawn", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Timer", StringComparison.OrdinalIgnoreCase))
            return false;
        return t.Contains("LyraCharacter", StringComparison.OrdinalIgnoreCase) ||
               t.Contains("Character", StringComparison.OrdinalIgnoreCase) ||
               (t.Contains("Pawn", StringComparison.OrdinalIgnoreCase) && !t.Contains("Spawning", StringComparison.OrdinalIgnoreCase));
    }

    public static AltObject? FindPlayerCharacter(AltDriver driver) => PlayerFinder.GetPlayerPawn(driver);

    public static AltObject WaitForPlayerCharacter(AltDriver driver, double timeoutSeconds = 15)
        => PlayerFinder.WaitForPlayerPawn(driver, timeoutSeconds);

    public static AltObject? FindObjectById(AltDriver driver, int id)
    {
        try
        {
            return driver.FindObject(By.ID, id.ToString(), enabled: true);
        }
        catch
        {
            try
            {
                return driver.FindObject(By.ID, id.ToString(), enabled: false);
            }
            catch
            {
                return null;
            }
        }
    }

    public static void WaitForGameplayToStart(AltDriver driver, double timeoutSeconds = 60)
    {
        var started = WaitForGameplayScene(driver, timeoutSeconds);
        Assert.That(started, Is.True, "WaitForGameplayToStart: scene did not change from " + LyraMenuHelper.MainMenuScene + " within " + timeoutSeconds + "s.");
    }

    public static AltObject FindEnemyBot(AltDriver driver, int playerId, double timeoutSeconds = 20)
    {
        var enemy = TryFindEnemyBot(driver, playerId, timeoutSeconds);
        Assert.That(enemy, Is.Not.Null, $"FindEnemyBot: no enemy pawn found within {timeoutSeconds}s (playerId={playerId}). Set ALTTESTER_EXPERIENCE_BUTTON to a tile that starts a match with bots (e.g. Control or LAN).");
        return enemy!;
    }

    public static AltObject? TryFindEnemyBot(AltDriver driver, int playerId, double timeoutSeconds = 20)
    {
        var playerObj = FindObjectById(driver, playerId);
        int? playerTeamId = playerObj != null ? TryGetTeamId(driver, playerObj) : null;

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                foreach (var name in PawnNamePatterns)
                {
                    var pawns = driver.FindObjectsWhichContain(By.NAME, name, enabled: true);
                    var enemy = FirstEnemyFromList(pawns, playerId, playerTeamId, driver);
                    if (enemy != null) return enemy;
                    pawns = driver.FindObjectsWhichContain(By.NAME, name, enabled: false);
                    enemy = FirstEnemyFromList(pawns, playerId, playerTeamId, driver);
                    if (enemy != null) return enemy;
                }
                var candidates = GetCharacterCandidatesExcludingPlayer(driver, playerId);
                var best = FirstEnemyFromList(candidates, playerId, playerTeamId, driver);
                if (best != null) return best;
            }
            catch { }
            Thread.Sleep(150);
        }
        return null;
    }

    private static AltObject? FirstEnemyFromList(List<AltObject>? list, int playerId, int? playerTeamId, AltDriver driver)
    {
        if (list == null || list.Count == 0) return null;
        var candidates = list.Where(o => o.id != playerId && IsEnemyCharacterType(o)).ToList();
        if (candidates.Count == 0) return null;
        if (playerTeamId != null)
        {
            var onOtherTeam = candidates.Where(o => TryGetTeamId(driver, o) is int t && t >= 0 && t != playerTeamId.Value).ToList();
            if (onOtherTeam.Count > 0)
            {
                var lyra = onOtherTeam.FirstOrDefault(o => o.type?.Contains("LyraCharacter", StringComparison.OrdinalIgnoreCase) ?? false);
                return lyra ?? onOtherTeam[0];
            }
        }
        var botControlled = candidates.Where(o => TryIsBotControlled(driver, o)).ToList();
        if (botControlled.Count > 0)
        {
            var lyra = botControlled.FirstOrDefault(o => o.type?.Contains("LyraCharacter", StringComparison.OrdinalIgnoreCase) ?? false);
            return lyra ?? botControlled[0];
        }
        return PreferLyraCharacterFromList(candidates, playerId);
    }

    public static bool TryIsBotControlled(AltDriver driver, AltObject pawn)
    {
        if (pawn == null) return false;
        foreach (var comp in new[] { "Pawn", "Character", pawn.type, "LyraCharacter" })
        {
            if (string.IsNullOrEmpty(comp)) continue;
            try
            {
                var ctrlId = pawn.CallComponentMethod<int>(comp, "GetController", "Engine", Array.Empty<object>(), Array.Empty<string>());
                if (ctrlId == 0) continue;
                var ctrl = FindObjectById(driver, ctrlId);
                if (ctrl == null || string.IsNullOrEmpty(ctrl.type)) continue;
                var t = ctrl.type;
                if (t.Contains("Bot", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("AIController", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("AI_", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
        }
        return false;
    }

    private static AltObject? PreferLyraCharacter(AltObject first, List<AltObject> list, int playerId)
    {
        if (list == null || list.Count == 0) return first;
        var enemies = list.Where(o => o.id != playerId && IsEnemyCharacterType(o)).ToList();
        var lyra = enemies.FirstOrDefault(o => o.type?.Contains("LyraCharacter", StringComparison.OrdinalIgnoreCase) ?? false);
        return lyra ?? first;
    }

    private static AltObject? PreferLyraCharacterFromList(List<AltObject> candidates, int playerId)
    {
        if (candidates == null || candidates.Count == 0) return null;
        var lyra = candidates.FirstOrDefault(o => (o.type?.Contains("LyraCharacter", StringComparison.OrdinalIgnoreCase) ?? false));
        return lyra ?? candidates.FirstOrDefault();
    }

    public static int? TryGetTeamId(AltDriver driver, AltObject pawn)
    {
        if (pawn == null) return null;
        foreach (var comp in new[] { "LyraPawn", "LyraCharacter", pawn.type, "Pawn", "Character" })
        {
            if (string.IsNullOrEmpty(comp)) continue;
            try
            {
                var teamId = pawn.CallComponentMethod<int>(comp, "GetTeamId", "LyraGame", Array.Empty<object>(), Array.Empty<string>());
                if (teamId >= 0) return teamId;
            }
            catch { }
        }
        try
        {
            foreach (var comp in new[] { "Pawn", "Character", pawn.type })
            {
                if (string.IsNullOrEmpty(comp)) continue;
                var psId = pawn.CallComponentMethod<int>(comp, "GetPlayerState", "Engine", Array.Empty<object>(), Array.Empty<string>());
                if (psId == 0) continue;
                var ps = FindObjectById(driver, psId);
                if (ps == null) continue;
                foreach (var psComp in new[] { "LyraPlayerState", ps.type, "PlayerState" })
                {
                    if (string.IsNullOrEmpty(psComp)) continue;
                    try
                    {
                        var teamId = ps.CallComponentMethod<int>(psComp, "GetTeamId", "LyraGame", Array.Empty<object>(), Array.Empty<string>());
                        if (teamId >= 0) return teamId;
                    }
                    catch { }
                }
            }
        }
        catch { }
        return null;
    }

    private static List<AltObject> GetCharacterCandidatesExcludingPlayer(AltDriver driver, int playerId)
    {
        var all = SafeGetAllElements(driver, enabled: true);
        var list = FilterCharacterCandidates(all).Where(o => o.id != playerId).ToList();
        if (list.Count == 0)
        {
            all = SafeGetAllElements(driver, enabled: false);
            list = FilterCharacterCandidates(all).Where(o => o.id != playerId).ToList();
        }
        return list;
    }

    private static List<AltObject> SafeGetAllElements(AltDriver driver, bool enabled = true)
    {
        try
        {
            return driver.GetAllElements(enabled: enabled) ?? new List<AltObject>();
        }
        catch
        {
            return new List<AltObject>();
        }
    }

    private static List<AltObject> FilterCharacterCandidates(List<AltObject> all)
    {
        if (all == null || all.Count == 0) return new List<AltObject>();
        return all
            .Where(o => !string.IsNullOrEmpty(o.type))
            .Where(o => !o.type.Contains("Spawner", StringComparison.OrdinalIgnoreCase))
            .Where(o => !o.type.Contains("Spawning", StringComparison.OrdinalIgnoreCase))
            .Where(o => !o.type.Contains("Respawn", StringComparison.OrdinalIgnoreCase))
            .Where(o => !o.type.Contains("Timer", StringComparison.OrdinalIgnoreCase))
            .Where(o => !o.type.Contains("Widget", StringComparison.OrdinalIgnoreCase))
            .Where(o => !(o.name ?? "").Contains("Spawning", StringComparison.OrdinalIgnoreCase))
            .Where(o =>
                o.type.Contains("LyraCharacter", StringComparison.OrdinalIgnoreCase) ||
                o.type.Contains("Character", StringComparison.OrdinalIgnoreCase) ||
                (o.type.Contains("Pawn", StringComparison.OrdinalIgnoreCase) && !o.type.Contains("Spawning", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static AltObject FindVisibleEnemyBot(AltDriver driver, int playerId, double timeoutSeconds = 25)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                foreach (var name in PawnNamePatterns)
                {
                    var pawns = driver.FindObjectsWhichContain(By.NAME, name, enabled: true);
                    var enemies = pawns?.Where(o => o.id != playerId && IsEnemyCharacterType(o)).ToList() ?? new List<AltObject>();
                    foreach (var enemy in enemies)
                    {
                        var screenPos = enemy.GetScreenPosition();
                        var hit = driver.FindObjectAtCoordinates(new AltVector2(screenPos.x, screenPos.y));
                        if (hit != null && hit.id == enemy.id)
                            return enemy;
                    }
                }
            }
            catch { }
            Thread.Sleep(150);
        }
        Assert.Fail($"FindVisibleEnemyBot: no visible (unobstructed) enemy found within {timeoutSeconds}s. Set ALTTESTER_EXPERIENCE_BUTTON to a tile that starts a match with bots (e.g. Control or LAN).");
        return null!;
    }

    public static AltObject FindEnemyForAim(AltDriver driver, int playerId, double visibleTimeoutSeconds = 20, double anyEnemyTimeoutSeconds = 30)
    {
        try
        {
            return FindVisibleEnemyBot(driver, playerId, timeoutSeconds: visibleTimeoutSeconds);
        }
        catch (AssertionException)
        {
            return FindEnemyBot(driver, playerId, timeoutSeconds: (int)(anyEnemyTimeoutSeconds - visibleTimeoutSeconds));
        }
    }

    public static AltObject? FindHUD(AltDriver driver)
    {
        foreach (var name in HudNamePatterns)
        {
            try
            {
                var obj = driver.FindObjectWhichContains(By.NAME, name, enabled: true);
                if (obj != null) return obj;
            }
            catch { }
        }
        return null;
    }

    public static AltObject WaitForHUD(AltDriver driver, double timeoutSeconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var obj = FindHUD(driver);
            if (obj != null) return obj;
            Thread.Sleep(500);
        }
        Assert.Fail($"WaitForHUD: HUD not found within {timeoutSeconds}s (tried names containing: {string.Join(", ", HudNamePatterns)}).");
        return null!;
    }

    public static void AssertHudViewVisible(AltDriver driver, AltObject hud)
    {
        Assert.That(hud.enabled, Is.True, "HUD should be enabled.");
        var viewport = driver.GetApplicationScreenSize();
        Assert.That(viewport, Is.Not.Null, "GetApplicationScreenSize must not return null.");
        Assert.That(viewport!.x, Is.GreaterThan(0), "Viewport width must be positive.");
        Assert.That(viewport.y, Is.GreaterThan(0), "Viewport height must be positive.");
        var screenPos = hud.GetScreenPosition();
        Assert.That(screenPos.x, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(viewport.x), "HUD should be on screen (x within viewport).");
        Assert.That(screenPos.y, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(viewport.y), "HUD should be on screen (y within viewport).");
    }

    public static float HorizontalDistance(AltObject from, AltObject to)
    {
        float dx = to.worldX - from.worldX;
        float dy = to.worldY - from.worldY;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public static bool WaitUntilTargetVisible(AltDriver driver, AltObject target, double timeoutSeconds = 15, int pollMs = 150)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var current = FindObjectById(driver, target.id);
            if (current != null && AimingHelper.IsTargetVisible(driver, current))
                return true;
            Thread.Sleep(pollMs);
        }
        return false;
    }

    public static bool MoveTowardTarget(AltDriver driver, int playerId, AltObject target, MoveTowardOptions? options = null)
    {
        options ??= new MoveTowardOptions();
        var deadline = DateTime.UtcNow.AddSeconds(options.TimeoutSeconds);
        float lastDist = float.MaxValue;
        int stuckCount = 0;
        int iterations = 0;

        while (DateTime.UtcNow < deadline)
        {
            if (options.MaxMoveIterations > 0 && iterations >= options.MaxMoveIterations)
                break;
            iterations++;

            var player = FindObjectById(driver, playerId);
            var targetObj = FindObjectById(driver, target.id);
            if (player == null || targetObj == null)
                return false;

            float dist = HorizontalDistance(player, targetObj);
            if (dist <= options.CloseEnoughDistance)
                return true;

            if (dist >= lastDist - options.StuckDistanceThreshold)
                stuckCount++;
            else
                stuckCount = 0;
            lastDist = dist;

            if (stuckCount >= options.StuckBurstsBeforeStrafe)
            {
                driver.KeyDown(AltKeyCode.D);
                Thread.Sleep((int)(options.StrafeSeconds * 1000));
                driver.KeyUp(AltKeyCode.D);
                Thread.Sleep(100);
                driver.KeyDown(AltKeyCode.A);
                Thread.Sleep((int)(options.StrafeSeconds * 1000));
                driver.KeyUp(AltKeyCode.A);
                Thread.Sleep(100);
                stuckCount = 0;
            }

            const int frameMs = 16;
            int burstFrames = Math.Max(1, (int)(options.MoveBurstSeconds * 1000 / frameMs));
            driver.KeyDown(AltKeyCode.W);
            for (int f = 0; f < burstFrames; f++)
            {
                var p = FindObjectById(driver, playerId);
                var t = FindObjectById(driver, target.id);
                if (p != null && t != null)
                {
                    var ctrl = AimingHelper.GetController(driver, p);
                    if (ctrl != null)
                    {
                        var (wx, wy, wz) = AimingHelper.GetTargetAimPoint(t);
                        var (px, py, pz) = AimingHelper.GetExactWorldPosition(p);
                        if (AimingHelper.IsValidAimPointInCombatRange(px, py, pz, wx, wy, wz))
                            AimingHelper.UpdateLookAtFromTo(driver, ctrl, px, py, pz, wx, wy, wz, out _, out _, out _, out _, out _);
                    }
                }
                Thread.Sleep(frameMs);
            }
            driver.KeyUp(AltKeyCode.W);
            Thread.Sleep(options.PollMs);
        }

        return false;
    }
}
