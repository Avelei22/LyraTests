using AltTester.AltTesterSDK.Driver;
using LyraTests.Helpers;
using LyraTests.Smoke;
using NUnit.Framework;

namespace LyraTests.Gameplay;

[TestFixture]
public class AimShootKillTest : SmokeTestBase
{
    const double PostQuickPlayLoadWaitSeconds = 20;
    const int PlayerFetchAttempts = 2;
    const double PlayerFetchIntervalSeconds = 2;
    const int EnginePositionsRetries = 3;
    const double EnginePositionsRetryDelaySeconds = 4;

    [Test]
    [Retry(2)]
    public void Aim_At_Enemy_Shoot_Until_Dead_Confirm_Kill()
    {
        GameplayHelper.EnterGameplay(Driver, gameplayTimeoutSeconds: 90, useMenuOnly: false);
        Thread.Sleep((int)(PostQuickPlayLoadWaitSeconds * 1000));
        AltObject? player = null;
        for (int attempt = 1; attempt <= PlayerFetchAttempts; attempt++)
        {
            player = GameplayHelper.FindPlayerCharacter(Driver);
            if (player != null)
            {
                Console.WriteLine($"[AimShootKillTest] Player fetch attempt {attempt}/{PlayerFetchAttempts}: success (name={player.name}, type={player.type}, id={player.id}).");
                break;
            }
            Console.WriteLine($"[AimShootKillTest] Player fetch attempt {attempt}/{PlayerFetchAttempts}: not found.");
            if (attempt < PlayerFetchAttempts)
                Thread.Sleep((int)(PlayerFetchIntervalSeconds * 1000));
        }
        bool useSubsystemEnemyPositions = false;
        int initialSubsystemEnemyCount = 0;
        List<(float x, float y, float z)> subsystemPositions = new List<(float, float, float)>();
        if (player == null)
        {
            for (int er = 1; er <= EnginePositionsRetries; er++)
            {
                var enemyOnlyList = new List<(float x, float y, float z)>();
                if (AimingHelper.TryGetControllerAndWorldId(Driver, out _, out int worldIdForFetch) && worldIdForFetch != 0
                    && AimingHelper.TryGetEnemyOnlyTestPositionsWithWorldId(Driver, worldIdForFetch, out _, out enemyOnlyList, verboseLogging: false) && enemyOnlyList.Count > 0)
                {
                    subsystemPositions = enemyOnlyList;
                    useSubsystemEnemyPositions = true;
                    initialSubsystemEnemyCount = subsystemPositions.Count;
                    Console.WriteLine($"[AimShootKillTest] Controller-only mode: no player pawn; using engine positions (enemies only={subsystemPositions.Count}, attempt {er}/{EnginePositionsRetries}).");
                    break;
                }
                if (AimingHelper.TryGetTestPositionsFromEngine(Driver, out _, out subsystemPositions) && subsystemPositions.Count > 0)
                {
                    useSubsystemEnemyPositions = true;
                    initialSubsystemEnemyCount = subsystemPositions.Count;
                    Console.WriteLine($"[AimShootKillTest] Controller-only mode: using all non-local positions (count={subsystemPositions.Count}, attempt {er}/{EnginePositionsRetries}).");
                    break;
                }
                if (er < EnginePositionsRetries)
                {
                    Console.WriteLine($"[AimShootKillTest] No other players from engine (attempt {er}/{EnginePositionsRetries}); waiting {EnginePositionsRetryDelaySeconds}s before retry.");
                    Thread.Sleep((int)(EnginePositionsRetryDelaySeconds * 1000));
                }
            }
            if (!useSubsystemEnemyPositions)
            {
                Assert.Fail("Player pawn not found and engine returned no other-player positions after " + EnginePositionsRetries + " attempts. Build Lyra with LyraTestEnemyQuery (GetTestPositionsAsString), ensure the match has other players, and that the game is in a playable state.");
            }
        }
        const int enemyFindRetries = 3;
        const double enemyFindTimeoutPerTry = 15;
        AltObject? enemy = null;
        if (player != null)
        {
            for (int r = 1; r <= enemyFindRetries; r++)
            {
                enemy = GameplayHelper.TryFindEnemyBot(Driver, player.id, timeoutSeconds: enemyFindTimeoutPerTry);
                if (enemy != null) break;
                Console.WriteLine($"[AimShootKillTest] Find other player attempt {r}/{enemyFindRetries}: not found via AltTester.");
                if (r < enemyFindRetries) Thread.Sleep(2000);
            }
        }
        if (enemy == null && !useSubsystemEnemyPositions && AimingHelper.TryGetTestPositionsFromEngine(Driver, out _, out subsystemPositions) && subsystemPositions.Count > 0)
        {
            useSubsystemEnemyPositions = true;
            initialSubsystemEnemyCount = subsystemPositions.Count;
            Console.WriteLine($"[AimShootKillTest] No enemy AltObject; using engine positions for other players (count={subsystemPositions.Count}).");
        }
        Assert.That(enemy != null || useSubsystemEnemyPositions, Is.True, $"No other players found: none via AltTester and none from engine. Ensure match has other players and test helper is built.");
        bool continuousAimFireOn = false;
        if (useSubsystemEnemyPositions)
        {
            continuousAimFireOn = AimingHelper.TrySetContinuousAimFireEnabled(Driver, true);
            if (continuousAimFireOn) Console.WriteLine("[AimShootKillTest] Continuous aim+fire enabled (every frame); C# will only poll for kill.");
        }

        int playerId = player?.id ?? 0;
        int targetEnemyId = enemy?.id ?? 0;
        AltObject? cachedCtrl = null;
        int cachedWorldId = 0;
        if (useSubsystemEnemyPositions && AimingHelper.TryGetControllerAndWorldId(Driver, out var ctrl, out var wid))
        {
            cachedCtrl = ctrl;
            cachedWorldId = wid;
        }
        float lastWx, lastWy, lastWz;
        if (enemy != null)
        {
            lastWx = enemy.worldX; lastWy = enemy.worldY; lastWz = AimingHelper.GetWorldZ(enemy);
        }
        else
        {
            var pos = subsystemPositions[0];
            lastWx = pos.x; lastWy = pos.y; lastWz = pos.z;
        }
        AimingHelper.EnsureTestCheatsApplied(Driver, bEnable: true, maxAttempts: 6, delayMs: 1500);
        if ((lastWx * lastWx + lastWy * lastWy + lastWz * lastWz) > 1f)
        {
            for (int t = 0; t < 10 && !GameplayHelper.TeleportPlayerTo(Driver, lastWx, lastWy, lastWz); t++)
                Thread.Sleep(300);
        }
        const double shootTimeoutSeconds = 180;
        const float fireIntervalSeconds = 0.12f;
        const double forceFireIntervalSeconds = 10;
        var shootDeadline = DateTime.UtcNow.AddSeconds(shootTimeoutSeconds);
        var lastFireUtc = DateTime.UtcNow;
        var lastForceFireUtc = DateTime.UtcNow;
        var lastLogUtc = DateTime.UtcNow;
        int fireCount = 0, skipNotVisibleCount = 0;
        bool killConfirmed = false;
        int consecutivePollsWithReducedEnemyCount = 0;
        const int RequiredConsecutivePollsForKill = 2;
        AltObject? cachedEnemy = enemy;
        var loopStartUtc = DateTime.UtcNow;
        var lastAimDiagUtc = DateTime.UtcNow;
        var lastNullEnemyLogUtc = DateTime.MinValue;
        var lastMissingTargetCheckUtc = DateTime.MinValue;

        while (DateTime.UtcNow < shootDeadline)
        {
            var now = DateTime.UtcNow;
            bool usedCombinedApi = false;
            AltObject? playerRef = (playerId != 0) ? GameplayHelper.FindObjectById(Driver, playerId) : null;
            AltObject? currentEnemy = null;
            if (targetEnemyId != 0)
            {
                currentEnemy = GameplayHelper.FindObjectById(Driver, targetEnemyId);
                if (currentEnemy != null) cachedEnemy = currentEnemy;
            }
            if (!useSubsystemEnemyPositions && !killConfirmed && targetEnemyId != 0 && currentEnemy == null && playerId != 0
                && (now - lastMissingTargetCheckUtc).TotalSeconds >= 2)
            {
                lastMissingTargetCheckUtc = now;
                AltObject? newTarget = GameplayHelper.TryFindEnemyBot(Driver, playerId, timeoutSeconds: 5);
                if (newTarget != null && newTarget.id != targetEnemyId)
                {
                    targetEnemyId = newTarget.id;
                    cachedEnemy = newTarget;
                    lastWx = newTarget.worldX; lastWy = newTarget.worldY; lastWz = AimingHelper.GetWorldZ(newTarget);
                    AimingHelper.EnsureTestCheatsApplied(Driver, bEnable: true, maxAttempts: 3, delayMs: 500);
                    GameplayHelper.TeleportPlayerTo(Driver, lastWx, lastWy, lastWz);
                    currentEnemy = newTarget;
                }
            }
            if (useSubsystemEnemyPositions)
            {
                if (continuousAimFireOn)
                {
                    var posList = new List<(float x, float y, float z)>();
                    bool gotPositions = cachedWorldId != 0 && AimingHelper.TryGetEnemyOnlyTestPositionsWithWorldId(Driver, cachedWorldId, out _, out posList, verboseLogging: false);
                    if (gotPositions && initialSubsystemEnemyCount >= 1)
                    {
                        bool reducedNow = posList.Count < initialSubsystemEnemyCount;
                        if (reducedNow)
                            consecutivePollsWithReducedEnemyCount++;
                        else
                            consecutivePollsWithReducedEnemyCount = 0;
                        if (consecutivePollsWithReducedEnemyCount >= RequiredConsecutivePollsForKill)
                            killConfirmed = true;
                    }
                    if (killConfirmed) break;
                    continue;
                }
                else
                {
                    List<(float x, float y, float z)> posList = new List<(float, float, float)>();
                    (float x, float y, float z)? playerPos = null;
                    float aimZ = lastWz + AimingHelper.TargetHeightOffsetZFromEngine;
                    bool wantFireThisFrame = (lastWx * lastWx + lastWy * lastWy + lastWz * lastWz) > 1f;
                    usedCombinedApi = cachedWorldId != 0 && AimingHelper.TryGetEnemyOnlyPositionsAndAimAt(Driver, cachedWorldId, lastWx, lastWy, aimZ, wantFireThisFrame, out playerPos, out posList);
                    bool gotPositions = usedCombinedApi;
                    if (!gotPositions && cachedWorldId != 0)
                    {
                        gotPositions = AimingHelper.TryGetEnemyOnlyTestPositionsWithWorldId(Driver, cachedWorldId, out playerPos, out posList, verboseLogging: false);
                        if (gotPositions) AimingHelper.TrySetLookAtWorldPositionViaSubsystem(Driver, lastWx, lastWy, aimZ);
                    }
                    if (gotPositions)
                    {
                        if (posList.Count > 0)
                        {
                            float refZ = playerPos.HasValue ? playerPos.Value.z : lastWz;
                            const float maxZDelta = 500f;
                            var inBounds = new List<(float x, float y, float z)>();
                            foreach (var p in posList)
                            {
                                if (Math.Abs(p.z - refZ) <= maxZDelta)
                                    inBounds.Add(p);
                            }
                            float px = playerPos?.x ?? lastWx, py = playerPos?.y ?? lastWy, pz = playerPos?.z ?? refZ;
                            var toSearch = inBounds.Count > 0 ? inBounds : posList;
                            if (toSearch.Count > 0)
                            {
                                float bestDistSq = float.MaxValue;
                                foreach (var p in toSearch)
                                {
                                    float dx = p.x - px, dy = p.y - py, dz = p.z - pz;
                                    float dSq = dx * dx + dy * dy + dz * dz;
                                    if (dSq < bestDistSq)
                                    {
                                        bestDistSq = dSq;
                                        lastWx = p.x; lastWy = p.y; lastWz = p.z;
                                    }
                                }
                            }
                            if (posList.Count < initialSubsystemEnemyCount && initialSubsystemEnemyCount >= 1)
                            {
                                consecutivePollsWithReducedEnemyCount++;
                                if (consecutivePollsWithReducedEnemyCount >= RequiredConsecutivePollsForKill)
                                    killConfirmed = true;
                            }
                            else
                                consecutivePollsWithReducedEnemyCount = 0;
                        }
                        else if (initialSubsystemEnemyCount >= 1 && posList.Count == 0)
                        {
                            consecutivePollsWithReducedEnemyCount++;
                            if (consecutivePollsWithReducedEnemyCount >= RequiredConsecutivePollsForKill)
                                killConfirmed = true;
                        }
                        else
                            consecutivePollsWithReducedEnemyCount = 0;
                    }
                    if (killConfirmed) break;
                }
            }

            if (cachedCtrl == null && playerRef != null)
                cachedCtrl = AimingHelper.GetController(Driver, playerRef);
            if (currentEnemy != null)
            {
                lastWx = currentEnemy.worldX; lastWy = currentEnemy.worldY; lastWz = AimingHelper.GetWorldZ(currentEnemy);
                if (playerRef != null && playerRef.id == currentEnemy.id && (now - lastNullEnemyLogUtc).TotalSeconds >= 2)
                {
                    lastNullEnemyLogUtc = now;
                    Console.WriteLine($"[AimShootKillTest] skip aim: player and enemy are same object (id={playerRef.id}).");
                }
                else
                {
                    float tz = lastWz + AimingHelper.TargetHeightOffsetZ;
                    if (!AimingHelper.TrySetLookAtWorldPositionViaSubsystem(Driver, lastWx, lastWy, tz))
                        AimingHelper.AimAtEnemyFromCamera(Driver, currentEnemy);
                }
            }
            else if (!useSubsystemEnemyPositions && (lastWx * lastWx + lastWy * lastWy + lastWz * lastWz) > 1f)
            {
                if ((now - lastNullEnemyLogUtc).TotalSeconds >= 2)
                {
                    lastNullEnemyLogUtc = now;
                    Console.WriteLine($"[AimShootKillTest] aim: no currentEnemy ref (targetId={targetEnemyId}), aiming at last known position ({lastWx:F0}, {lastWy:F0}, {lastWz:F0})");
                }
                float aimZ = lastWz + AimingHelper.TargetHeightOffsetZFromEngine;
                if (!AimingHelper.TrySetLookAtWorldPositionViaSubsystem(Driver, lastWx, lastWy, aimZ))
                {
                    if (cachedCtrl != null)
                        AimingHelper.AimAtEnemyFromCamera(Driver, cachedCtrl, lastWx, lastWy, lastWz);
                    else
                        AimingHelper.AimAtEnemyFromCamera(Driver, lastWx, lastWy, lastWz);
                }
            }

            if ((now - lastAimDiagUtc).TotalSeconds >= 5)
            {
                lastAimDiagUtc = now;
                double elapsed = (now - loopStartUtc).TotalSeconds;
                var pw = playerRef != null ? $"id={playerRef.id} ({playerRef.worldX:F0},{playerRef.worldY:F0},{AimingHelper.GetWorldZ(playerRef):F0})" : "null";
                var en = currentEnemy != null ? $"id={currentEnemy.id} name={currentEnemy.name} ({currentEnemy.worldX:F0},{currentEnemy.worldY:F0},{AimingHelper.GetWorldZ(currentEnemy):F0})" : "null";
                if (currentEnemy != null && currentEnemy.id != targetEnemyId)
                    Console.WriteLine($"[AimShootKillTest] aim diag: FindObjectById({targetEnemyId}) returned id={currentEnemy.id} (using returned position for aim)");
                if (playerRef != null && currentEnemy != null && playerRef.id == currentEnemy.id)
                    Console.WriteLine($"[AimShootKillTest] aim diag: WARNING player and enemy are same object (id={playerRef.id})");
                bool samePos = playerRef != null && currentEnemy != null
                    && Math.Abs(playerRef.worldX - currentEnemy.worldX) < 1f && Math.Abs(playerRef.worldY - currentEnemy.worldY) < 1f
                    && Math.Abs(AimingHelper.GetWorldZ(playerRef) - AimingHelper.GetWorldZ(currentEnemy)) < 1f;
                if (samePos)
                    Console.WriteLine($"[AimShootKillTest] aim diag: WARNING playerRef and currentEnemy positions identical (possible same object or FindObjectById bug)");
                Console.WriteLine($"[AimShootKillTest] aim diag t={elapsed:F0}s: playerRef={pw} currentEnemy={en} aimAt=({lastWx:F0},{lastWy:F0},{lastWz:F0})");
            }

            bool shouldFire = false;
            if (useSubsystemEnemyPositions && !continuousAimFireOn)
            {
                shouldFire = (lastWx * lastWx + lastWy * lastWy + lastWz * lastWz) > 1f;
            }
            else if ((now - lastFireUtc).TotalSeconds >= fireIntervalSeconds)
            {
                var targetForVisibility = currentEnemy;
                bool isVisible = targetForVisibility != null && AimingHelper.IsTargetVisible(Driver, targetForVisibility);
                bool forceFire = !isVisible && currentEnemy != null && (now - lastForceFireUtc).TotalSeconds >= forceFireIntervalSeconds;
                shouldFire = isVisible || forceFire;
                if (forceFire) lastForceFireUtc = now;
            }

            if (shouldFire)
            {
                if (!useSubsystemEnemyPositions) lastFireUtc = now;
                if (!useSubsystemEnemyPositions || !usedCombinedApi)
                {
                    AimingHelper.Fire(Driver, holdSeconds: 0.08f);
                }
                fireCount++;
                if (useSubsystemEnemyPositions)
                {
                    if (killConfirmed) break;
                }
                else
                {
                    killConfirmed = AimingHelper.PollUntilTargetDestroyed(Driver, targetEnemyId, timeoutSeconds: 2.0, pollMs: 80);
                    if (killConfirmed) break;
                    var stillExists = GameplayHelper.FindObjectById(Driver, targetEnemyId);
                    if (stillExists == null)
                    {
                        killConfirmed = true;
                        cachedEnemy = null;
                        AltObject? nextEnemy = null;
                        for (int r = 1; r <= enemyFindRetries; r++)
                        {
                            nextEnemy = GameplayHelper.TryFindEnemyBot(Driver, playerId, timeoutSeconds: 8);
                            if (nextEnemy != null) break;
                            Console.WriteLine($"[AimShootKillTest] Find next enemy after kill attempt {r}/{enemyFindRetries}: no other player found.");
                            if (r < enemyFindRetries) Thread.Sleep(1500);
                        }
                        if (nextEnemy == null) break;
                        targetEnemyId = nextEnemy.id;
                        cachedEnemy = nextEnemy;
                        lastWx = nextEnemy.worldX; lastWy = nextEnemy.worldY; lastWz = AimingHelper.GetWorldZ(nextEnemy);
                        AimingHelper.EnsureTestCheatsApplied(Driver, bEnable: true, maxAttempts: 3, delayMs: 500);
                        GameplayHelper.TeleportPlayerTo(Driver, lastWx, lastWy, lastWz);
                    }
                }
            }
            else if (!useSubsystemEnemyPositions && (now - lastFireUtc).TotalSeconds >= fireIntervalSeconds)
                skipNotVisibleCount++;

            if ((now - lastLogUtc).TotalSeconds >= 15)
            {
                Console.WriteLine($"[AimShootKillTest] shoot phase: fired={fireCount}, skipped(not visible)={skipNotVisibleCount}");
                lastLogUtc = now;
            }
        }
        string killMsg = useSubsystemEnemyPositions
            ? $"Kill not confirmed within {shootTimeoutSeconds}s: enemy count from engine never dropped below {initialSubsystemEnemyCount}. Aim may be off (check TargetHeightOffsetZFromEngine) or match has respawns."
            : $"Kill not confirmed within {shootTimeoutSeconds}s (last enemy id={targetEnemyId}). If no other players after kill, ensure match has other players; see [AimShootKillTest] Find next enemy logs.";
        Assert.That(killConfirmed, Is.True, killMsg);
    }

    [TearDown]
    public void RestoreMainMenu()
    {
        try
        {
            AimingHelper.TrySetContinuousAimFireEnabled(Driver, false);
            AimingHelper.TrySetLocalPlayerInvincible(Driver, false);
        }
        catch { }
        try
        {
            var scene = Driver.GetCurrentScene();
            if (!string.IsNullOrEmpty(scene) && scene != LyraMenuHelper.MainMenuScene)
                LyraMenuHelper.LoadMainMenu(Driver, sceneTimeout: 15);
        }
        catch { }
    }
}
