using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using AltTester.AltTesterSDK.Driver;
using NUnit.Framework;

namespace LyraTests.Helpers;

public static class PlayerFinder
{
    public static AltObject? GetPlayerPawn(AltDriver driver)
    {
        var candidates = GetCharacterCandidates(driver);
        var knownController = FindPlayerController(driver);
        foreach (var c in candidates)
        {
            if (TryIsPlayerControlled(driver, c, out var isPlayer) && isPlayer)
            {
                LogPlayerFound(c, "candidates+IsPlayerControlled");
                return c;
            }
            if (knownController != null && TryIsPawnControlledBy(driver, c, knownController.id))
            {
                LogPlayerFound(c, "candidates+ControllerMatch");
                return c;
            }
            if (LooksLikePlayerByName(c))
            {
                LogPlayerFound(c, "candidates+LooksLikePlayerByName");
                return c;
            }
        }
        var viaController = TryGetPlayerPawnViaController(driver);
        if (viaController != null)
        {
            LogPlayerFound(viaController, "GetPawn");
            return viaController;
        }
        var viaStatics = TryGetPlayerPawnViaGameplayStatics(driver);
        if (viaStatics != null)
        {
            LogPlayerFound(viaStatics, "GameplayStatics");
            return viaStatics;
        }
        var viaEngineId = TryGetPlayerPawnViaEngineId(driver);
        if (viaEngineId != null)
        {
            LogPlayerFound(viaEngineId, "GetLocalPlayerPawnId");
            return viaEngineId;
        }
        return null;
    }

    static void LogPlayerFound(AltObject pawn, string source)
    {
        Console.WriteLine($"[PlayerFinder] Found player: name={pawn.name}, type={pawn.type}, id={pawn.id} (source={source}).");
    }

    public static AltObject WaitForPlayerPawn(AltDriver driver, double timeoutSeconds = 30)
    {
        var sw = Stopwatch.StartNew();
        AltObject? lastCandidate = null;

        while (sw.Elapsed.TotalSeconds < timeoutSeconds)
        {
            var candidates = GetCharacterCandidates(driver);
            if (candidates.Count > 0)
            {
                DumpOncePerSecond(candidates);
                var knownController = FindPlayerController(driver);
                foreach (var c in candidates)
                {
                    lastCandidate = c;
                    if (TryIsPlayerControlled(driver, c, out var isPlayer) && isPlayer)
                        return c;
                    if (knownController != null && TryIsPawnControlledBy(driver, c, knownController.id))
                        return c;
                    if (LooksLikePlayerByName(c))
                        return c;
                }
            }
            else
                DumpRawElementsOncePerSecond(driver);

            var pawnViaController = TryGetPlayerPawnViaController(driver);
            if (pawnViaController != null)
            {
                LogPlayerFound(pawnViaController, "GetPawn(wait)");
                return pawnViaController;
            }
            var pawnViaStatics = TryGetPlayerPawnViaGameplayStatics(driver);
            if (pawnViaStatics != null)
            {
                LogPlayerFound(pawnViaStatics, "GameplayStatics(wait)");
                return pawnViaStatics;
            }
            var pawnViaEngineId = TryGetPlayerPawnViaEngineId(driver);
            if (pawnViaEngineId != null)
            {
                LogPlayerFound(pawnViaEngineId, "GetLocalPlayerPawnId(wait)");
                return pawnViaEngineId;
            }

            Thread.Sleep(150);
        }

        var msg = lastCandidate == null
            ? $"PlayerFinder.WaitForPlayerPawn: no character candidates within {timeoutSeconds}s."
            : $"PlayerFinder.WaitForPlayerPawn: no player pawn within {timeoutSeconds}s. LastCandidate={lastCandidate.name} ({lastCandidate.type}) id={lastCandidate.id}";
        Assert.Fail(msg);
        return null!;
    }

    public static AltObject? TryWaitForPlayerPawn(AltDriver driver, double timeoutSeconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeoutSeconds)
        {
            var candidates = GetCharacterCandidates(driver);
            if (candidates.Count > 0)
            {
                var knownController = FindPlayerController(driver);
                foreach (var c in candidates)
                {
                    if (TryIsPlayerControlled(driver, c, out var isPlayer) && isPlayer)
                        return c;
                    if (knownController != null && TryIsPawnControlledBy(driver, c, knownController.id))
                        return c;
                    if (LooksLikePlayerByName(c))
                        return c;
                }
            }
            var pawnViaController = TryGetPlayerPawnViaController(driver);
            if (pawnViaController != null) return pawnViaController;
            var pawnViaStatics = TryGetPlayerPawnViaGameplayStatics(driver);
            if (pawnViaStatics != null) return pawnViaStatics;
            var pawnViaEngineId = TryGetPlayerPawnViaEngineId(driver);
            if (pawnViaEngineId != null) return pawnViaEngineId;
            Thread.Sleep(200);
        }
        return null;
    }

    static AltObject? TryGetPlayerPawnViaController(AltDriver driver)
    {
        AltObject? controller = FindPlayerController(driver);
        if (controller == null)
        {
            LogControllerPawnOnce("[PlayerFinder] TryGetPlayerPawnViaController: no controller found.");
            return null;
        }
        var comps = new[] { "PlayerController", controller.type, "Controller" };
        var assemblies = new[] { "LyraGame", "Engine", "" };
        foreach (var assembly in assemblies)
        {
            foreach (var comp in comps)
            {
                if (string.IsNullOrEmpty(comp)) continue;
                foreach (var method in new[] { "GetPawn", "GetCharacter" })
                {
                    try
                    {
                        long pawnIdLong = 0;
                        try
                        {
                            pawnIdLong = controller.CallComponentMethod<int>(comp, method, assembly, Array.Empty<object>(), Array.Empty<string>());
                        }
                        catch
                        {
                            try { pawnIdLong = controller.CallComponentMethod<long>(comp, method, assembly, Array.Empty<object>(), Array.Empty<string>()); }
                            catch { }
                        }
                        if (pawnIdLong == 0) continue;
                        var pawnIdStr = pawnIdLong.ToString();
                        var pawn = driver.FindObject(By.ID, pawnIdStr, enabled: false) ?? driver.FindObject(By.ID, pawnIdStr, enabled: true);
                        if (pawn != null)
                        {
                            LogControllerPawnOnce($"[PlayerFinder] TryGetPlayerPawnViaController: controller={controller.name}, {method}=>id={pawnIdStr}, pawn={pawn.name}.");
                            return pawn;
                        }
                        LogControllerPawnOnce($"[PlayerFinder] TryGetPlayerPawnViaController: {method} returned id={pawnIdStr} but FindObject(id) returned null.");
                    }
                    catch (Exception ex)
                    {
                        LogControllerPawnOnce($"[PlayerFinder] TryGetPlayerPawnViaController: controller={controller.name}, {method}({comp},{assembly}) threw: {ex.Message}");
                    }
                }
            }
        }
        LogControllerPawnOnce($"[PlayerFinder] TryGetPlayerPawnViaController: controller={controller.name}, GetPawn/GetCharacter returned 0 or FindObject failed for all attempts.");
        return null;
    }

    static AltObject? TryGetPlayerPawnViaGameplayStatics(AltDriver driver)
    {
        AltObject? world = null;
        try
        {
            world = driver.FindObjectWhichContains(By.NAME, "World", enabled: false);
        }
        catch { }
        if (world == null)
        {
            try
            {
                world = driver.FindObject(By.NAME, "PersistentLevel", enabled: false);
            }
            catch { }
        }

        var anyContext = world ?? FindPlayerController(driver);
        if (anyContext == null) return null;

        foreach (var asm in new[] { "Engine", "" })
        {
            try
            {
                var pawnId = driver.CallStaticMethod<int>(
                    "GameplayStatics",
                    "GetPlayerPawn",
                    asm,
                    new object[] { anyContext.id, 0 },
                    new string[] { "System.Int32", "System.Int32" });
                if (pawnId != 0)
                {
                    var pawn = driver.FindObject(By.ID, pawnId.ToString(), enabled: false) ?? driver.FindObject(By.ID, pawnId.ToString(), enabled: true);
                    if (pawn != null) return pawn;
                }
            }
            catch { }

            try
            {
                var charId = driver.CallStaticMethod<int>(
                    "GameplayStatics",
                    "GetPlayerCharacter",
                    asm,
                    new object[] { anyContext.id, 0 },
                    new string[] { "System.Int32", "System.Int32" });
                if (charId != 0)
                {
                    var pawn = driver.FindObject(By.ID, charId.ToString(), enabled: false) ?? driver.FindObject(By.ID, charId.ToString(), enabled: true);
                    if (pawn != null) return pawn;
                }
            }
            catch { }
        }
        return null;
    }

    static DateTime _lastControllerPawnLog = DateTime.MinValue;
    static void LogControllerPawnOnce(string msg)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastControllerPawnLog).TotalSeconds < 2) return;
        _lastControllerPawnLog = now;
        Console.WriteLine(msg);
    }

    static AltObject? FindPlayerController(AltDriver driver)
    {
        foreach (var enabled in new[] { true, false })
        {
            var list = SafeFindObjectsWhichContain(driver, "PlayerController", enabled);
            if (list != null && list.Count > 0)
            {
                var c = list.FirstOrDefault(o => o.type?.Contains("PlayerController", StringComparison.OrdinalIgnoreCase) == true);
                if (c != null) return c;
                if (list.Count > 0) return list[0];
            }
        }
        foreach (var enabled in new[] { true, false })
        {
            var list = SafeFindObjectsWhichContain(driver, "Lyra", enabled);
            if (list == null || list.Count == 0) continue;
            var c = list.FirstOrDefault(o => o.type?.Contains("PlayerController", StringComparison.OrdinalIgnoreCase) == true);
            if (c != null) return c;
        }
        try
        {
            var o = driver.FindObjectWhichContains(By.NAME, "PlayerController", enabled: true);
            if (o != null) return o;
        }
        catch { }
        try
        {
            var o = driver.FindObjectWhichContains(By.NAME, "PlayerController", enabled: false);
            if (o != null) return o;
        }
        catch { }
        return null;
    }

    static List<AltObject> GetCharacterCandidates(AltDriver driver)
    {
        var candidates = GetCharacterCandidatesFromList(SafeGetAll(driver, enabled: true));
        if (candidates.Count == 0)
            candidates = GetCharacterCandidatesFromList(SafeGetAll(driver, enabled: false));
        if (candidates.Count == 0)
        {
            candidates = SafeFindObjectsWhichContain(driver, "LyraCharacter", enabled: true);
            if (candidates.Count == 0)
                candidates = SafeFindObjectsWhichContain(driver, "LyraCharacter", enabled: false);
        }
        return candidates;
    }

    static List<AltObject> SafeFindObjectsWhichContain(AltDriver driver, string nameSubstring, bool enabled = true)
    {
        try
        {
            var list = driver.FindObjectsWhichContain(By.NAME, nameSubstring, enabled: enabled);
            return list ?? new List<AltObject>();
        }
        catch
        {
            return new List<AltObject>();
        }
    }

    static List<AltObject> GetCharacterCandidatesFromList(List<AltObject> all)
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
            .Where(o => !(o.name ?? "").StartsWith("GCNL_", StringComparison.OrdinalIgnoreCase))
            .Where(o => !(o.name ?? "").StartsWith("GCN_", StringComparison.OrdinalIgnoreCase))
            .Where(o => !(o.type ?? "").Contains("GCNL_", StringComparison.OrdinalIgnoreCase))
            .Where(o => !(o.type ?? "").Contains("GCN_", StringComparison.OrdinalIgnoreCase))
            .Where(o => !(o.type ?? "").Contains("GameplayCue", StringComparison.OrdinalIgnoreCase))
            .Where(o => !(o.type ?? "").Contains("Niagara", StringComparison.OrdinalIgnoreCase))
            .Where(o => !(o.type ?? "").Contains("Emitter", StringComparison.OrdinalIgnoreCase))
            .Where(o => !(o.type ?? "").Contains("Effect", StringComparison.OrdinalIgnoreCase))
            .Where(o =>
                o.type.Contains("LyraCharacter", StringComparison.OrdinalIgnoreCase) ||
                o.type.Contains("Character", StringComparison.OrdinalIgnoreCase) ||
                (o.type.Contains("Pawn", StringComparison.OrdinalIgnoreCase) && !o.type.Contains("Spawning", StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    static List<AltObject> SafeGetAll(AltDriver driver, bool enabled = true)
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

    static bool TryIsPlayerControlled(AltDriver driver, AltObject obj, out bool value)
    {
        value = false;

        var attempts = new (string componentName, string assemblyName)[]
        {
            (obj.type, "Engine"),
            ("Character", "Engine"),
            ("Pawn", "Engine"),
            (obj.type, "LyraGame"),
            ("Character", "LyraGame"),
            ("Pawn", "LyraGame"),
        };

        foreach (var (componentName, assemblyName) in attempts)
        {
            if (TryCallBool(obj, componentName, "IsPlayerControlled", assemblyName, out var v))
            {
                value = v;
                return true;
            }
        }

        return false;
    }

    static bool TryCallBool(AltObject obj, string componentName, string methodName, string assemblyName, out bool result)
    {
        result = false;
        try
        {
            result = obj.CallComponentMethod<bool>(componentName, methodName, assemblyName, new object[] { }, new string[] { });
            return true;
        }
        catch
        {
            return false;
        }
    }

    static bool TryIsPawnControlledBy(AltDriver driver, AltObject pawn, int controllerId)
    {
        var comps = new[] { "Pawn", "Character", pawn.type, "Actor" };
        foreach (var comp in comps)
        {
            if (string.IsNullOrEmpty(comp)) continue;
            foreach (var asm in new[] { "Engine", "LyraGame", "" })
            {
                try
                {
                    var ctrlId = pawn.CallComponentMethod<int>(comp, "GetController", asm, Array.Empty<object>(), Array.Empty<string>());
                    if (ctrlId != 0 && ctrlId == controllerId)
                        return true;
                }
                catch { }
            }
        }
        return false;
    }

    static AltObject? TryGetPlayerPawnViaEngineId(AltDriver driver)
    {
        AltObject? world = null;
        try { world = driver.FindObjectWhichContains(By.NAME, "World", enabled: false); }
        catch { }
        if (world == null)
        {
            try { world = driver.FindObject(By.NAME, "PersistentLevel", enabled: false); }
            catch { }
        }
        var anyContext = world ?? FindPlayerController(driver);
        if (anyContext == null) return null;
        try
        {
            var pawnId = driver.CallStaticMethod<long>("LyraTestEnemyQuery", "GetLocalPlayerPawnId", "LyraGame",
                new object[] { anyContext.id, 0 }, new string[] { "System.Int32", "System.Int32" });
            if (pawnId == 0) return null;
            var pawn = driver.FindObject(By.ID, pawnId.ToString(), enabled: false) ?? driver.FindObject(By.ID, pawnId.ToString(), enabled: true);
            return pawn;
        }
        catch { return null; }
    }

    static bool LooksLikePlayerByName(AltObject obj)
    {
        if (string.IsNullOrEmpty(obj.name)) return false;

        if (obj.name.StartsWith("GCNL_", StringComparison.OrdinalIgnoreCase)) return false;
        if (obj.name.StartsWith("GCN_", StringComparison.OrdinalIgnoreCase)) return false;

        if (obj.name.Contains("Bot", StringComparison.OrdinalIgnoreCase)) return false;
        if (obj.name.Contains("AI", StringComparison.OrdinalIgnoreCase)) return false;

        if (obj.name.Contains("Player", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    static DateTime _lastDump = DateTime.MinValue;
    static DateTime _lastRawDump = DateTime.MinValue;

    static void DumpOncePerSecond(List<AltObject> candidates)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastDump).TotalSeconds < 1) return;
        _lastDump = now;

        Console.WriteLine($"[PlayerFinder] Candidates={candidates.Count}");
        foreach (var c in candidates.Take(10))
            Console.WriteLine($"[PlayerFinder] - name='{c.name}', type='{c.type}', id={c.id}");
    }

    static void DumpRawElementsOncePerSecond(AltDriver driver)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastRawDump).TotalSeconds < 1) return;
        _lastRawDump = now;

        Console.WriteLine("[PlayerFinder] No character candidates. Sampling what AltTester returns:");
        foreach (var pattern in new[] { "Character", "Pawn", "Actor", "Lyra" })
        {
            var list = SafeFindObjectsWhichContain(driver, pattern, enabled: true);
            if (list.Count == 0)
                list = SafeFindObjectsWhichContain(driver, pattern, enabled: false);
            var sample = list.Take(12).Select(o => $"{o.name}|{o.type}").ToList();
            Console.WriteLine($"[PlayerFinder]   FindObjectsWhichContain('{pattern}'): {list.Count} total. First: [{string.Join(" ; ", sample)}]");
        }
    }
}
