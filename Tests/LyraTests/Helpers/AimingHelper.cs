using AltTester.AltTesterSDK.Driver;
using NUnit.Framework;

namespace LyraTests.Helpers;

public static class AimingHelper
{
    public static void Fire(AltDriver driver, float holdSeconds = 0.1f)
    {
        driver.PressKey(AltKeyCode.Mouse0, holdSeconds);
    }

    public static bool IsTargetVisible(AltDriver driver, AltObject target)
    {
        try
        {
            var pos = target.GetScreenPosition();
            var hit = driver.FindObjectAtCoordinates(new AltVector2(pos.x, pos.y));
            return hit != null && hit.id == target.id;
        }
        catch
        {
            return false;
        }
    }

    public static bool PollUntilTargetDestroyed(AltDriver driver, int targetId, double timeoutSeconds = 30, int pollMs = 200)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var elements = driver.GetAllElements(enabled: false);
            if (elements == null || elements.All(e => e.id != targetId))
                return true;
            Thread.Sleep(pollMs);
        }
        return false;
    }

    public const float TargetHeightOffsetZ = 135f;
    public const float TargetHeightOffsetZFromEngine = 40f;
    public const float EyeHeightOffsetZ = 80f;

    public static (float x, float y, float z) GetTargetAimPoint(AltObject target)
    {
        return (target.worldX, target.worldY, GetWorldZ(target) + TargetHeightOffsetZ);
    }

    public static (float x, float y, float z) GetExactWorldPosition(AltObject target)
    {
        return (target.worldX, target.worldY, GetWorldZ(target));
    }

    public static bool IsValidAimPointInCombatRange(float playerX, float playerY, float playerZ, float targetX, float targetY, float targetZ, float maxDistance = 50000f)
    {
        float h = (float)Math.Sqrt(targetX * targetX + targetY * targetY);
        if (h < 1f) return false;
        float dx = targetX - playerX, dy = targetY - playerY, dz = targetZ - playerZ;
        float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return dist <= maxDistance && dist >= 0.01f;
    }

    public static AltObject? GetController(AltDriver driver, AltObject? player)
    {
        if (player == null) return null;
        return GetControllerFromPawn(driver, player) ?? FindPlayerController(driver);
    }

    public static void AimExactlyAtWorldPosition(AltDriver driver, float playerX, float playerY, float playerZ, float targetX, float targetY, float targetZ)
    {
        var controller = FindPlayerController(driver);
        if (controller == null) return;

        float fromZ = playerZ + EyeHeightOffsetZ;
        float toZ = targetZ + TargetHeightOffsetZ;
        var desired = DirectionToPitchYaw(playerX, playerY, fromZ, targetX, targetY, toZ);
        if (desired == null) return;

        if (CallSetControlRotation(driver, controller, desired.Value.pitchDeg, desired.Value.yawDeg, 0f, out _))
            return;
        TryApplyLookInput(driver, controller, desired.Value.pitchDeg, desired.Value.yawDeg);
    }

    public static void AimAtEnemyFromCamera(AltDriver driver, AltObject enemy)
    {
        var controller = FindPlayerController(driver);
        if (controller == null) return;

        if (!TryGetCameraWorldLocation(driver, controller, out var camX, out var camY, out var camZ))
            return;

        float targetX = enemy.worldX, targetY = enemy.worldY, targetZ = GetWorldZ(enemy) + TargetHeightOffsetZ;
        var desired = DirectionToPitchYaw(camX, camY, camZ, targetX, targetY, targetZ);
        if (desired == null) return;

        if (CallSetControlRotation(driver, controller, desired.Value.pitchDeg, desired.Value.yawDeg, 0f, out _))
            return;
        TryApplyLookInput(driver, controller, desired.Value.pitchDeg, desired.Value.yawDeg);
    }

    public static void AimAtEnemyFromCamera(AltDriver driver, float enemyX, float enemyY, float enemyZ)
    {
        var controller = FindPlayerController(driver);
        if (controller == null) return;
        AimAtEnemyFromCamera(driver, controller, enemyX, enemyY, enemyZ);
    }

    public static void AimAtEnemyFromCamera(AltDriver driver, AltObject controller, float enemyX, float enemyY, float enemyZ)
    {
        if (controller == null) return;
        if (!TryGetCameraWorldLocation(driver, controller, out var camX, out var camY, out var camZ))
            return;
        float toZ = enemyZ + TargetHeightOffsetZFromEngine;
        var desired = DirectionToPitchYaw(camX, camY, camZ, enemyX, enemyY, toZ);
        if (desired == null) return;
        if (CallSetControlRotation(driver, controller, desired.Value.pitchDeg, desired.Value.yawDeg, 0f, out _))
            return;
        TryApplyLookInput(driver, controller, desired.Value.pitchDeg, desired.Value.yawDeg);
    }

    const string TestSupportSubsystemType = "LyraTestSupportSubsystem";
    static readonly string[] TestSupportSubsystemTypeNames = { "LyraTestSupportSubsystem", "ULyraTestSupportSubsystem" };
    const int AimLogEveryNFrames = 10;

    static AltObject? _cachedSubsystem;
    static int _aimFrameCount;

    static bool LooksLikePawnOrCharacter(AltObject o)
    {
        if (o == null) return false;
        var n = o.name ?? "";
        var t = o.type ?? "";
        if (n.StartsWith("GCNL_", StringComparison.OrdinalIgnoreCase)) return false;
        if (t.StartsWith("GCNL_", StringComparison.OrdinalIgnoreCase)) return false;
        if (t.IndexOf("GameplayCue", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (t.IndexOf("Niagara", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (t.IndexOf("Emitter", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (t.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (t.IndexOf("Pawn", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (t.IndexOf("Character", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (t.IndexOf("LyraCharacter", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (t.IndexOf("LyraPawn", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    public static void AimExactlyAtTarget(AltDriver driver, AltObject? player, AltObject target)
    {
        if (player == null || target == null) return;
        _aimFrameCount++;
        if (player.id == target.id)
        {
            if (_aimFrameCount % AimLogEveryNFrames == 0)
                Console.WriteLine($"[AimingHelper] reject: player and target are same object (id={player.id}); skip aim this frame.");
            return;
        }
        var (tx, ty, tz) = GetTargetAimPoint(target);
        var (px, py, pz) = GetExactWorldPosition(player);
        float viewZ = pz + EyeHeightOffsetZ;

        float ourLen = (float)Math.Sqrt(px * px + py * py + pz * pz);
        if (ourLen < 10f)
        {
            if (_aimFrameCount % AimLogEveryNFrames == 0)
                Console.WriteLine($"[AimingHelper] reject: our position near origin ({px:F0},{py:F0},{pz:F0}); skip aim (id={player.id}).");
            return;
        }
        float dist = (float)Math.Sqrt((tx - px) * (tx - px) + (ty - py) * (ty - py) + (tz - viewZ) * (tz - viewZ));
        if (dist < 1f)
        {
            if (_aimFrameCount % AimLogEveryNFrames == 0)
                Console.WriteLine($"[AimingHelper] reject: our view and target same position (from=({px:F0},{py:F0},{viewZ:F0}) to=({tx:F0},{ty:F0},{tz:F0})); skip aim.");
            return;
        }

        string pathUsed = "none";
        float? desiredPitch = null, desiredYaw = null;
        float? currentPitch = null, currentYaw = null;

        if (TrySetLookAtWorldPositionViaSubsystem(driver, tx, ty, tz))
        {
            pathUsed = "subsystem";
        }
        else
        {
            var controller = GetController(driver, player) ?? FindPlayerController(driver);
            if (controller != null)
            {
                UpdateLookAtFromTo(driver, controller, px, py, pz, tx, ty, tz, out pathUsed, out desiredPitch, out desiredYaw, out currentPitch, out currentYaw);
            }
            else
            {
                pathUsed = "no-controller";
                if (_aimFrameCount % AimLogEveryNFrames == 0)
                    Console.WriteLine("[AimingHelper] no controller; skip aim");
            }
        }

        if (_aimFrameCount % AimLogEveryNFrames == 0)
        {
            bool zeroWarn = (Math.Abs(px) < 0.01f && Math.Abs(py) < 0.01f && Math.Abs(pz) < 0.01f);
            Console.WriteLine($"[AimingHelper] frame={_aimFrameCount} path={pathUsed} subsystemUsed={pathUsed == "subsystem"}");
            Console.WriteLine($"[AimingHelper]   player name={player.name} type={player.type} id={player.id}");
            Console.WriteLine($"[AimingHelper]   target name={target.name} type={target.type} id={target.id}");
            Console.WriteLine($"[AimingHelper]   our position (used for aim) = ({px:F0}, {py:F0}, {pz:F0}) eye=({px:F0}, {py:F0}, {viewZ:F0}){(zeroWarn ? " (WARNING: near 0,0,0)" : "")}");
            Console.WriteLine($"[AimingHelper]   target(aim)  = ({tx:F0}, {ty:F0}, {tz:F0}) dist={dist:F0}");
            if (desiredPitch.HasValue && desiredYaw.HasValue)
                Console.WriteLine($"[AimingHelper]   desired rotation: Pitch={desiredPitch.Value:F1} Yaw={desiredYaw.Value:F1}");
            if (currentPitch.HasValue && currentYaw.HasValue)
                Console.WriteLine($"[AimingHelper]   current rotation: Pitch={currentPitch.Value:F1} Yaw={currentYaw.Value:F1}");
        }
    }

    static int TryGetWorldContextId(AltDriver driver)
    {
        var controller = FindPlayerController(driver);
        if (controller != null)
        {
            foreach (var comp in new[] { "PlayerController", "Controller", controller.type })
            {
                if (string.IsNullOrEmpty(comp)) continue;
                try
                {
                    var worldId = controller.CallComponentMethod<int>(comp, "GetWorld", "Engine", Array.Empty<object>(), Array.Empty<string>());
                    if (worldId != 0) return worldId;
                }
                catch { }
            }
            return controller.id;
        }
        try
        {
            var world = driver.FindObjectWhichContains(By.NAME, "World", enabled: false);
            if (world != null) return world.id;
        }
        catch { }
        try
        {
            var world = driver.FindObject(By.NAME, "PersistentLevel", enabled: false);
            if (world != null) return world.id;
        }
        catch { }
        return 0;
    }

    public static bool TryGetControllerAndWorldId(AltDriver driver, out AltObject? controller, out int worldId)
    {
        controller = null;
        worldId = 0;
        var ctrl = FindPlayerController(driver);
        if (ctrl != null)
        {
            controller = ctrl;
            foreach (var comp in new[] { "PlayerController", "Controller", ctrl.type })
            {
                if (string.IsNullOrEmpty(comp)) continue;
                try
                {
                    var w = ctrl.CallComponentMethod<int>(comp, "GetWorld", "Engine", Array.Empty<object>(), Array.Empty<string>());
                    if (w != 0) { worldId = w; return true; }
                }
                catch { }
            }
            worldId = ctrl.id;
            return true;
        }
        try
        {
            var world = driver.FindObjectWhichContains(By.NAME, "World", enabled: false);
            if (world != null) { worldId = world.id; return true; }
        }
        catch { }
        try
        {
            var world = driver.FindObject(By.NAME, "PersistentLevel", enabled: false);
            if (world != null) { worldId = world.id; return true; }
        }
        catch { }
        return false;
    }

    public static bool TryGetTestPositionsWithWorldId(AltDriver driver, int worldId, out (float x, float y, float z)? playerPosition, out List<(float x, float y, float z)> enemyPositions, bool verboseLogging = false)
    {
        playerPosition = null;
        enemyPositions = new List<(float, float, float)>();
        if (worldId == 0) return false;
        try
        {
            var s = driver.CallStaticMethod<string>("LyraTestEnemyQuery", "GetTestPositionsAsString", "LyraGame",
                new object[] { worldId, 0 }, new string[] { "System.Int32", "System.Int32" });
            if (verboseLogging)
            {
                var preview = string.IsNullOrWhiteSpace(s) ? "(empty)" : (s.Length > 120 ? s.Substring(0, 120) + "..." : s);
                Console.WriteLine($"[EnginePositions] GetTestPositionsAsString returned: {preview}");
            }
            if (string.IsNullOrWhiteSpace(s)) return false;
            return ParseTestPositionsString(s, out playerPosition, out enemyPositions, verboseLogging);
        }
        catch (Exception ex)
        {
            if (verboseLogging) Console.WriteLine($"[EnginePositions] GetTestPositionsWithWorldId failed: {ex.Message}");
            return false;
        }
    }

    public static bool TryGetEnemyOnlyTestPositionsWithWorldId(AltDriver driver, int worldId, out (float x, float y, float z)? playerPosition, out List<(float x, float y, float z)> enemyPositions, bool verboseLogging = false)
    {
        playerPosition = null;
        enemyPositions = new List<(float, float, float)>();
        if (worldId == 0) return false;
        try
        {
            var s = driver.CallStaticMethod<string>("LyraTestEnemyQuery", "GetEnemyOnlyTestPositionsAsString", "LyraGame",
                new object[] { worldId, 0 }, new string[] { "System.Int32", "System.Int32" });
            if (verboseLogging)
            {
                var preview = string.IsNullOrWhiteSpace(s) ? "(empty)" : (s.Length > 120 ? s.Substring(0, 120) + "..." : s);
                Console.WriteLine($"[EnginePositions] GetEnemyOnlyTestPositionsAsString returned: {preview}");
            }
            if (string.IsNullOrWhiteSpace(s)) return false;
            return ParseTestPositionsString(s, out playerPosition, out enemyPositions, verboseLogging);
        }
        catch (Exception ex)
        {
            if (verboseLogging) Console.WriteLine($"[EnginePositions] GetEnemyOnlyTestPositionsWithWorldId failed: {ex.Message}");
            return false;
        }
    }

    public static bool TryGetEnemyOnlyPositionsAndAimAt(AltDriver driver, int worldId, float targetX, float targetY, float targetZ, bool bFire, out (float x, float y, float z)? playerPosition, out List<(float x, float y, float z)> enemyPositions)
    {
        playerPosition = null;
        enemyPositions = new List<(float, float, float)>();
        if (worldId == 0) return false;
        try
        {
            var s = driver.CallStaticMethod<string>("LyraTestEnemyQuery", "GetEnemyOnlyPositionsAndAimAt", "LyraGame",
                new object[] { worldId, 0, targetX, targetY, targetZ, bFire }, new string[] { "System.Int32", "System.Int32", "System.Single", "System.Single", "System.Single", "System.Boolean" });
            if (string.IsNullOrWhiteSpace(s)) return false;
            return ParseTestPositionsString(s, out playerPosition, out enemyPositions, log: false);
        }
        catch { return false; }
    }

    static bool ParseTestPositionsString(string s, out (float x, float y, float z)? playerPosition, out List<(float x, float y, float z)> enemyPositions, bool log = false)
    {
        playerPosition = null;
        enemyPositions = new List<(float, float, float)>();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var part in s.Split('|'))
        {
            var seg = part.Trim();
            if (seg.Length < 5) continue;
            var tri = seg.Split(',');
            if (tri.Length < 4) continue;
            if (!float.TryParse(tri[1].Trim(), System.Globalization.NumberStyles.Float, inv, out var x)
                || !float.TryParse(tri[2].Trim(), System.Globalization.NumberStyles.Float, inv, out var y)
                || !float.TryParse(tri[3].Trim(), System.Globalization.NumberStyles.Float, inv, out var z))
                continue;
            if (seg.StartsWith("P,", StringComparison.OrdinalIgnoreCase))
                playerPosition = (x, y, z);
            else if (seg.StartsWith("E,", StringComparison.OrdinalIgnoreCase))
                enemyPositions.Add((x, y, z));
        }
        if (log) Console.WriteLine($"[EnginePositions] Parsed player={playerPosition.HasValue} enemies={enemyPositions.Count}");
        return enemyPositions.Count > 0 || playerPosition.HasValue;
    }

    public static bool TryGetEnemyOnlyTestPositionsFromEngine(AltDriver driver, out (float x, float y, float z)? playerPosition, out List<(float x, float y, float z)> enemyPositions)
    {
        if (TryGetControllerAndWorldId(driver, out _, out int worldId) && worldId != 0)
        {
            if (TryGetEnemyOnlyTestPositionsWithWorldId(driver, worldId, out playerPosition, out enemyPositions, verboseLogging: false)
                && enemyPositions.Count > 0)
            {
                return true;
            }
        }
        return TryGetTestPositionsFromEngine(driver, out playerPosition, out enemyPositions);
    }

    public static bool TryGetTestPositionsFromEngine(AltDriver driver, out (float x, float y, float z)? playerPosition, out List<(float x, float y, float z)> enemyPositions)
    {
        playerPosition = null;
        enemyPositions = new List<(float, float, float)>();
        int worldId = TryGetWorldContextId(driver);
        if (worldId == 0)
        {
            Console.WriteLine("[EnginePositions] No world context (controller.GetWorld=0 and FindObject World/PersistentLevel failed); cannot call LyraTestEnemyQuery.");
            return false;
        }
        try
        {
            var s = driver.CallStaticMethod<string>("LyraTestEnemyQuery", "GetTestPositionsAsString", "LyraGame",
                new object[] { worldId, 0 }, new string[] { "System.Int32", "System.Int32" });
            var preview = string.IsNullOrWhiteSpace(s) ? "(empty)" : (s.Length > 120 ? s.Substring(0, 120) + "..." : s);
            Console.WriteLine($"[EnginePositions] GetTestPositionsAsString returned: {preview}");
            if (string.IsNullOrWhiteSpace(s)) return FallbackEnemyOnly(driver, out enemyPositions);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var part in s.Split('|'))
            {
                var seg = part.Trim();
                if (seg.Length < 5) continue;
                var tri = seg.Split(',');
                if (tri.Length < 4) continue;
                if (!float.TryParse(tri[1].Trim(), System.Globalization.NumberStyles.Float, inv, out var x)
                    || !float.TryParse(tri[2].Trim(), System.Globalization.NumberStyles.Float, inv, out var y)
                    || !float.TryParse(tri[3].Trim(), System.Globalization.NumberStyles.Float, inv, out var z))
                    continue;
                if (seg.StartsWith("P,", StringComparison.OrdinalIgnoreCase))
                    playerPosition = (x, y, z);
                else if (seg.StartsWith("E,", StringComparison.OrdinalIgnoreCase))
                    enemyPositions.Add((x, y, z));
            }
            Console.WriteLine($"[EnginePositions] Parsed player={playerPosition.HasValue} enemies={enemyPositions.Count}");
            if (enemyPositions.Count > 0) return true;
            if (playerPosition.HasValue) return true;
            return FallbackEnemyOnly(driver, out enemyPositions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnginePositions] GetTestPositionsAsString failed: {ex.Message}. Trying GetEnemyLocationsAsString.");
            return FallbackEnemyOnly(driver, out enemyPositions);
        }
    }

    static bool FallbackEnemyOnly(AltDriver driver, out List<(float x, float y, float z)> enemyPositions)
    {
        var ok = TryGetEnemyLocationsFromTestSupport(driver, out enemyPositions);
        Console.WriteLine($"[EnginePositions] GetEnemyLocationsAsString: {(ok ? enemyPositions.Count + " positions" : "failed or empty")}");
        return ok;
    }

    public static bool TryGetEnemyLocationsFromTestSupport(AltDriver driver, out List<(float x, float y, float z)> positions)
    {
        positions = new List<(float, float, float)>();
        int worldId = TryGetWorldContextId(driver);
        if (worldId == 0) return false;
        try
        {
            var s = driver.CallStaticMethod<string>("LyraTestEnemyQuery", "GetEnemyLocationsAsString", "LyraGame",
                new object[] { worldId, 0 }, new string[] { "System.Int32", "System.Int32" });
            if (string.IsNullOrWhiteSpace(s)) return false;
            foreach (var part in s.Split('|'))
            {
                var tri = part.Split(',');
                if (tri.Length >= 3 && float.TryParse(tri[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                    && float.TryParse(tri[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
                    && float.TryParse(tri[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                    positions.Add((x, y, z));
            }
            return positions.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool TrySetLookAtWorldPositionViaSubsystem(AltDriver driver, float targetX, float targetY, float targetZ)
    {
        var sub = FindTestSupportSubsystem(driver);
        if (sub == null) return false;
        foreach (var typeName in new[] { TestSupportSubsystemType, "ULyraTestSupportSubsystem", sub.type })
        {
            if (string.IsNullOrEmpty(typeName)) continue;
            foreach (var asm in new[] { "LyraGame", "Core" })
                try
                {
                    sub.CallComponentMethod<object>(typeName, "SetLocalPlayerLookAtWorldPosition", asm,
                        new object[] { targetX, targetY, targetZ },
                        new string[] { "System.Single", "System.Single", "System.Single" });
                    return true;
                }
                catch { }
        }
        _cachedSubsystem = null;
        return false;
    }

    public static bool TrySetContinuousAimFireEnabled(AltDriver driver, bool bEnabled)
    {
        var sub = FindTestSupportSubsystem(driver);
        if (sub == null) return false;
        foreach (var typeName in new[] { TestSupportSubsystemType, "ULyraTestSupportSubsystem", sub.type })
        {
            if (string.IsNullOrEmpty(typeName)) continue;
            foreach (var asm in new[] { "LyraGame", "Core" })
                try
                {
                    sub.CallComponentMethod<object>(typeName, "SetContinuousAimFireEnabled", asm,
                        new object[] { bEnabled }, new string[] { "System.Boolean" });
                    return true;
                }
                catch { }
        }
        _cachedSubsystem = null;
        return false;
    }

    public static bool TrySetLocalPlayerInvincible(AltDriver driver, bool bEnable)
    {
        if (TrySetInvincibleViaSubsystem(driver, bEnable)) return true;
        if (TrySetInvincibleViaQuery(driver, bEnable)) return true;
        return false;
    }

    public static bool TrySetLocalPlayerInfiniteAmmo(AltDriver driver, bool bEnable)
    {
        if (TrySetInfiniteAmmoViaSubsystem(driver, bEnable)) return true;
        if (TrySetInfiniteAmmoViaQuery(driver, bEnable)) return true;
        return false;
    }

    static bool TrySetInvincibleViaSubsystem(AltDriver driver, bool bEnable)
    {
        var sub = FindTestSupportSubsystem(driver);
        if (sub == null) return false;
        foreach (var typeName in new[] { "LyraTestSupportSubsystem", "ULyraTestSupportSubsystem", sub.type })
        {
            if (string.IsNullOrEmpty(typeName)) continue;
            foreach (var asm in new[] { "LyraGame", "Core" })
            {
                try
                {
                    sub.CallComponentMethod<object>(typeName, "SetLocalPlayerInvincible", asm,
                        new object[] { bEnable }, new string[] { "System.Boolean" });
                    return true;
                }
                catch { }
            }
        }
        _cachedSubsystem = null;
        return false;
    }

    static bool TrySetInfiniteAmmoViaSubsystem(AltDriver driver, bool bEnable)
    {
        var sub = FindTestSupportSubsystem(driver);
        if (sub == null) return false;
        foreach (var typeName in new[] { "LyraTestSupportSubsystem", "ULyraTestSupportSubsystem", sub.type })
        {
            if (string.IsNullOrEmpty(typeName)) continue;
            foreach (var asm in new[] { "LyraGame", "Core" })
            {
                try
                {
                    sub.CallComponentMethod<object>(typeName, "SetLocalPlayerInfiniteAmmo", asm,
                        new object[] { bEnable }, new string[] { "System.Boolean" });
                    return true;
                }
                catch { }
            }
        }
        _cachedSubsystem = null;
        return false;
    }

    static bool TrySetInvincibleViaQuery(AltDriver driver, bool bEnable)
    {
        if (!TryGetControllerAndWorldId(driver, out _, out int worldId) || worldId == 0) return false;
        foreach (var typeName in new[] { "LyraTestEnemyQuery", "ULyraTestEnemyQuery" })
        foreach (var asm in new[] { "LyraGame", "Core" })
        {
            try
            {
                driver.CallStaticMethod<object>(typeName, "SetLocalPlayerInvincible", asm,
                    new object[] { worldId, bEnable }, new string[] { "System.Int32", "System.Boolean" });
                return true;
            }
            catch { }
        }
        return false;
    }

    static bool TrySetInfiniteAmmoViaQuery(AltDriver driver, bool bEnable)
    {
        if (!TryGetControllerAndWorldId(driver, out _, out int worldId) || worldId == 0) return false;
        foreach (var typeName in new[] { "LyraTestEnemyQuery", "ULyraTestEnemyQuery" })
        foreach (var asm in new[] { "LyraGame", "Core" })
        {
            try
            {
                driver.CallStaticMethod<object>(typeName, "SetLocalPlayerInfiniteAmmo", asm,
                    new object[] { worldId, bEnable }, new string[] { "System.Int32", "System.Boolean" });
                return true;
            }
            catch { }
        }
        return false;
    }

    public static void EnsureTestCheatsApplied(AltDriver driver, bool bEnable, int maxAttempts = 3, int delayMs = 2000)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            bool inv = TrySetLocalPlayerInvincible(driver, bEnable);
            bool ammo = TrySetLocalPlayerInfiniteAmmo(driver, bEnable);
            if (inv && ammo) return;
            if (attempt < maxAttempts) Thread.Sleep(delayMs);
        }
    }

    static AltObject? FindTestSupportSubsystem(AltDriver driver)
    {
        if (_cachedSubsystem != null)
        {
            try
            {
                var byId = driver.FindObject(By.ID, _cachedSubsystem.id.ToString(), enabled: false);
                if (byId != null) return _cachedSubsystem = byId;
            }
            catch { }
            _cachedSubsystem = null;
        }

        AltObject? sub = TryFindSubsystemByDirectSearch(driver);
        if (sub == null)
            sub = TryFindSubsystemViaGameInstance(driver);
        if (sub != null)
            _cachedSubsystem = sub;
        return sub;
    }

    static AltObject? TryFindSubsystemByDirectSearch(AltDriver driver)
    {
        foreach (var name in TestSupportSubsystemTypeNames)
        {
            foreach (var enabled in new[] { false, true })
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
        try
        {
            var all = driver.GetAllElements(enabled: false);
            if (all != null)
                foreach (var o in all)
                    if (o.type != null && (o.type.IndexOf("LyraTestSupportSubsystem", StringComparison.OrdinalIgnoreCase) >= 0 || o.type.IndexOf("ULyraTestSupportSubsystem", StringComparison.OrdinalIgnoreCase) >= 0))
                        return o;
        }
        catch { }
        foreach (var name in TestSupportSubsystemTypeNames)
        {
            try
            {
                var list = driver.FindObjectsWhichContain(By.NAME, name, enabled: false);
                if (list != null && list.Count > 0) return list[0];
            }
            catch { }
        }
        return null;
    }

    static AltObject? TryFindSubsystemViaGameInstance(AltDriver driver)
    {
        AltObject? controller = FindPlayerController(driver);
        if (controller == null) return null;
        try
        {
            AltObject? cdo = null;
            foreach (var name in new[] { "Default__LyraTestSupportSubsystem", "Default__ULyraTestSupportSubsystem", TestSupportSubsystemType, "ULyraTestSupportSubsystem" })
            {
                cdo = driver.FindObject(By.NAME, name, enabled: false);
                if (cdo != null) break;
            }
            if (cdo == null) return null;
            var classId = cdo.CallComponentMethod<int>("Object", "GetClass", "Core", Array.Empty<object>(), Array.Empty<string>());
            if (classId == 0) return null;
            var worldId = controller.CallComponentMethod<int>("PlayerController", "GetWorld", "Engine", Array.Empty<object>(), Array.Empty<string>());
            if (worldId == 0) return null;
            var world = driver.FindObject(By.ID, worldId.ToString(), enabled: false) ?? driver.FindObject(By.ID, worldId.ToString(), enabled: true);
            if (world == null) return null;
            var giId = world.CallComponentMethod<int>("World", "GetGameInstance", "Engine", Array.Empty<object>(), Array.Empty<string>());
            if (giId == 0) return null;
            var gi = driver.FindObject(By.ID, giId.ToString(), enabled: false) ?? driver.FindObject(By.ID, giId.ToString(), enabled: true);
            if (gi == null) return null;
            var subId = gi.CallComponentMethod<int>("GameInstance", "GetSubsystem", "Engine", new object[] { classId }, new string[] { "System.Int32" });
            if (subId == 0) return null;
            var sub = driver.FindObject(By.ID, subId.ToString(), enabled: false) ?? driver.FindObject(By.ID, subId.ToString(), enabled: true);
            return sub;
        }
        catch { }
        return null;
    }

    public const float PitchMinDeg = -89f;
    public const float PitchMaxDeg = 89f;
    public static float ApplyLookInputGain { get; set; } = 25f;
    public static float ApplyLookInputMaxDeltaPerCall { get; set; } = 15f;
    public static float AimStopThresholdDeg { get; set; } = 0.5f;

    public static (float pitchDeg, float yawDeg)? DirectionToPitchYaw(float fromX, float fromY, float fromZ, float toX, float toY, float toZ)
    {
        float dx = toX - fromX, dy = toY - fromY, dz = toZ - fromZ;
        float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 0.0001f) return null;
        dx /= len; dy /= len; dz /= len;
        float yawRad = (float)Math.Atan2(dy, dx);
        float pitchRad = (float)Math.Asin(Math.Clamp(dz, -1f, 1f));
        const float radToDeg = 180f / (float)Math.PI;
        float pitchDeg = Math.Clamp(pitchRad * radToDeg, PitchMinDeg, PitchMaxDeg);
        float yawDeg = yawRad * radToDeg;
        return (pitchDeg, yawDeg);
    }

    static bool _applyLookInputFallbackLogged;

    public static void UpdateLookAtFromTo(AltDriver driver, AltObject? controller, float ourX, float ourY, float ourZ, float targetX, float targetY, float targetZ,
        out string pathUsed, out float? desiredPitch, out float? desiredYaw, out float? currentPitch, out float? currentYaw)
    {
        pathUsed = "none";
        desiredPitch = null;
        desiredYaw = null;
        currentPitch = null;
        currentYaw = null;
        if (controller == null) return;
        float fromZ = ourZ + EyeHeightOffsetZ;
        var desired = DirectionToPitchYaw(ourX, ourY, fromZ, targetX, targetY, targetZ);
        if (desired == null) return;
        desiredPitch = desired.Value.pitchDeg;
        desiredYaw = desired.Value.yawDeg;

        if (CallSetControlRotation(driver, controller, desired.Value.pitchDeg, desired.Value.yawDeg, 0f, out _))
        {
            pathUsed = "SetControlRotation";
            return;
        }

        if (!_applyLookInputFallbackLogged)
        {
            _applyLookInputFallbackLogged = true;
            Console.WriteLine("[AimingHelper] SetControlRotation not available; using Engine AddYawInput/AddPitchInput fallback.");
        }
        if (TryGetControlRotation(controller, out float cy, out float cp))
        {
            currentYaw = cy;
            currentPitch = cp;
        }
        pathUsed = TryApplyLookInput(driver, controller, desired.Value.pitchDeg, desired.Value.yawDeg) ? "ApplyLookInput" : "ApplyLookInput(failed)";
    }

    static bool TryApplyLookInput(AltDriver driver, AltObject controller, float desiredPitchDeg, float desiredYawDeg)
    {
        if (!TryGetControlRotation(controller, out float currentYaw, out float currentPitch))
            return false;
        float yawErr = NormalizeAngleDeg(desiredYawDeg - currentYaw);
        float pitchErr = NormalizeAngleDeg(desiredPitchDeg - currentPitch);
        if (Math.Abs(yawErr) < AimStopThresholdDeg && Math.Abs(pitchErr) < AimStopThresholdDeg)
            return true;
        float yawDelta = ApplyLookInputMaxDeltaPerCall > 0f
            ? Math.Clamp(yawErr, -ApplyLookInputMaxDeltaPerCall, ApplyLookInputMaxDeltaPerCall)
            : yawErr;
        float pitchDelta = ApplyLookInputMaxDeltaPerCall > 0f
            ? Math.Clamp(pitchErr, -ApplyLookInputMaxDeltaPerCall, ApplyLookInputMaxDeltaPerCall)
            : pitchErr;
        yawDelta *= ApplyLookInputGain;
        pitchDelta *= ApplyLookInputGain;
        foreach (var comp in new[] { controller.type, "PlayerController", "Controller" })
        {
            if (string.IsNullOrEmpty(comp)) continue;
            try
            {
                controller.CallComponentMethod<object>(comp, "AddYawInput", "Engine", new object[] { yawDelta }, new string[] { "System.Single" });
                controller.CallComponentMethod<object>(comp, "AddPitchInput", "Engine", new object[] { pitchDelta }, new string[] { "System.Single" });
                return true;
            }
            catch { }
        }
        return false;
    }

    static bool TryGetControlRotation(AltObject controller, out float yawDeg, out float pitchDeg)
    {
        yawDeg = 0f;
        pitchDeg = 0f;
        foreach (var comp in new[] { controller.type, "PlayerController", "Controller" })
        {
            if (string.IsNullOrEmpty(comp)) continue;
            try
            {
                var rotStr = controller.CallComponentMethod<string>(comp, "GetControlRotation", "Engine", Array.Empty<object>(), Array.Empty<string>());
                if (TryParseRotator(rotStr, out pitchDeg, out yawDeg))
                    return true;
            }
            catch { }
        }
        foreach (var comp in new[] { controller.type, "PlayerController", "Controller" })
        {
            if (string.IsNullOrEmpty(comp)) continue;
            try
            {
                yawDeg = controller.CallComponentMethod<float>(comp, "GetControlRotationYaw", "Engine", Array.Empty<object>(), Array.Empty<string>());
                pitchDeg = controller.CallComponentMethod<float>(comp, "GetControlRotationPitch", "Engine", Array.Empty<object>(), Array.Empty<string>());
                return true;
            }
            catch { }
        }
        return false;
    }

    static bool TryParseRotator(string? s, out float pitch, out float yaw)
    {
        pitch = 0f;
        yaw = 0f;
        if (string.IsNullOrEmpty(s)) return false;
        int p = s.IndexOf("Pitch=", StringComparison.OrdinalIgnoreCase);
        int y = s.IndexOf("Yaw=", StringComparison.OrdinalIgnoreCase);
        if (p < 0 || y < 0) return false;
        if (!TryParseFloatAfter(s, p + 6, out pitch)) return false;
        if (!TryParseFloatAfter(s, y + 4, out yaw)) return false;
        pitch = Math.Clamp(pitch, PitchMinDeg, PitchMaxDeg);
        return true;
    }

    static bool TryParseFloatAfter(string s, int startIdx, out float value)
    {
        value = 0f;
        int i = startIdx;
        while (i < s.Length && s[i] == ' ') i++;
        int end = i;
        while (end < s.Length)
        {
            char c = s[end];
            if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'E' || c == 'e')
            { end++; continue; }
            break;
        }
        if (end <= i) return false;
        return float.TryParse(s.Substring(i, end - i), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    static bool TryParseVector(string? s, out float x, out float y, out float z)
    {
        x = y = z = 0f;
        if (string.IsNullOrEmpty(s)) return false;
        int ix = s.IndexOf("X=", StringComparison.OrdinalIgnoreCase);
        int iy = s.IndexOf("Y=", StringComparison.OrdinalIgnoreCase);
        int iz = s.IndexOf("Z=", StringComparison.OrdinalIgnoreCase);
        if (ix < 0 || iy < 0 || iz < 0) return false;
        return TryParseFloatAfter(s, ix + 2, out x)
            && TryParseFloatAfter(s, iy + 2, out y)
            && TryParseFloatAfter(s, iz + 2, out z);
    }

    static float NormalizeAngleDeg(float deg)
    {
        while (deg > 180f) deg -= 360f;
        while (deg < -180f) deg += 360f;
        return deg;
    }

    static AltObject? GetControllerFromPawn(AltDriver driver, AltObject pawn)
    {
        var componentNames = new[] { "Pawn", "Character", pawn.type, "LyraCharacter" };
        foreach (var comp in componentNames)
        {
            if (string.IsNullOrEmpty(comp)) continue;
            try
            {
                var id = pawn.CallComponentMethod<int>(comp, "GetController", "Engine", Array.Empty<object>(), Array.Empty<string>());
                if (id != 0)
                {
                    var c = driver.FindObject(By.ID, id.ToString(), enabled: true);
                    if (c != null) return c;
                    c = driver.FindObject(By.ID, id.ToString(), enabled: false);
                    if (c != null) return c;
                }
            }
            catch { }
        }
        return null;
    }

    public static float GetWorldZ(AltObject o)
    {
        try
        {
            return o.worldZ;
        }
        catch
        {
            return 0f;
        }
    }

    static AltObject? FindPlayerController(AltDriver driver)
    {
        try
        {
            var byName = driver.FindObjectWhichContains(By.NAME, "LyraPlayerController", enabled: false);
            if (byName != null) return byName;
        }
        catch { }
        try
        {
            var byName = driver.FindObjectWhichContains(By.NAME, "PlayerController", enabled: false);
            if (byName != null) return byName;
        }
        catch { }

        var typeNames = new[] { "LyraPlayerController", "PlayerController", "Controller" };
        foreach (var enabled in new[] { true, false })
        {
            var all = driver.GetAllElements(enabled: enabled);
            if (all == null) continue;
            foreach (var o in all)
            {
                if (string.IsNullOrEmpty(o.type)) continue;
                foreach (var typeName in typeNames)
                {
                    if (o.type.Contains(typeName, StringComparison.OrdinalIgnoreCase))
                        return o;
                }
            }
        }
        return null;
    }

    static AltObject? TryGetPlayerCameraManager(AltDriver driver, AltObject controller)
    {
        foreach (var comp in new[] { controller.type, "PlayerController" })
        {
            if (string.IsNullOrEmpty(comp)) continue;
            foreach (var asm in new[] { "Engine", "" })
            {
                try
                {
                    var id = controller.CallComponentMethod<int>(comp, "GetPlayerCameraManager", asm, Array.Empty<object>(), Array.Empty<string>());
                    if (id == 0) continue;
                    return driver.FindObject(By.ID, id.ToString(), enabled: false) ?? driver.FindObject(By.ID, id.ToString(), enabled: true);
                }
                catch { }
            }
        }
        return null;
    }

    static bool TryGetCameraWorldLocation(AltDriver driver, AltObject controller, out float x, out float y, out float z)
    {
        x = y = z = 0f;
        var pcm = TryGetPlayerCameraManager(driver, controller);
        if (pcm != null)
        {
            foreach (var comp in new[] { pcm.type, "PlayerCameraManager" })
            {
                if (string.IsNullOrEmpty(comp)) continue;
                foreach (var asm in new[] { "Engine", "" })
                {
                    try
                    {
                        var loc = pcm.CallComponentMethod<string>(comp, "GetCameraLocation", asm, Array.Empty<object>(), Array.Empty<string>());
                        if (TryParseVector(loc, out x, out y, out z))
                            return true;
                    }
                    catch { }
                }
            }
        }
        foreach (var comp in new[] { controller.type, "PlayerController", "Controller" })
        {
            if (string.IsNullOrEmpty(comp)) continue;
            foreach (var asm in new[] { "Engine", "" })
            {
                try
                {
                    var loc = controller.CallComponentMethod<string>(comp, "GetFocalLocation", asm, Array.Empty<object>(), Array.Empty<string>());
                    if (TryParseVector(loc, out x, out y, out z))
                        return true;
                }
                catch { }
            }
        }
        return false;
    }

    static bool CallSetControlRotation(AltDriver driver, AltObject controller, float pitch, float yaw, float roll, out string? errorMsg)
    {
        errorMsg = null;
        var rotatorStr = $"(Pitch={pitch:F6},Yaw={yaw:F6},Roll={roll:F6})";
        var attempts = new (string componentName, string assembly)[]
        {
            ("Controller", "Engine"),
            ("PlayerController", "Engine"),
            ("Controller", ""),
            ("PlayerController", ""),
            (controller.type ?? "", "Engine"),
            (controller.type ?? "", ""),
        };

        foreach (var (componentName, assembly) in attempts)
        {
            if (string.IsNullOrEmpty(componentName)) continue;
            try
            {
                controller.CallComponentMethod<object>(componentName, "SetControlRotation", assembly,
                    new object[] { rotatorStr },
                    new string[] { "System.String" });
                return true;
            }
            catch (Exception ex) { errorMsg = ex.Message; }
        }
        return false;
    }
}
