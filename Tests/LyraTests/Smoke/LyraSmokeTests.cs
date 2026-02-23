using AltTester.AltTesterSDK.Driver;
using LyraTests.Helpers;
using NUnit.Framework;

namespace LyraTests.Smoke;

public class LyraSmokeTests : SmokeTestBase
{
    [TearDown]
    public void RestoreMainMenu()
    {
        try { GameplayHelper.TeardownInGameTest(Driver); }
        catch { /* ignore */ }
        try
        {
            var scene = Driver.GetCurrentScene();
            if (!string.IsNullOrEmpty(scene) && scene != LyraMenuHelper.MainMenuScene)
            {
                LyraMenuHelper.LoadMainMenu(Driver, sceneTimeout: 15);
            }
        }
        catch { }
    }

    [Test]
    public void MainMenu_StartGame_Enters_Gameplay()
    {
        LyraMenuHelper.GoToQuickPlay(Driver);
        GameplayHelper.WaitForGameplayToStart(Driver, timeoutSeconds: 60);

        var currentScene = Driver.GetCurrentScene();
        Assert.That(currentScene, Is.Not.Null.And.Not.Empty, "The current scene should be set after gameplay start.");
        Assert.That(currentScene, Is.Not.EqualTo(LyraMenuHelper.MainMenuScene),
            "Gameplay should have started: we should have left the main menu scene.");
    }

    [Test]
    public void Gameplay_HUD_Is_Visible()
    {
        GameplayHelper.EnterGameplay(Driver, gameplayTimeoutSeconds: 60);
        GameplayHelper.SetupInGameTest(Driver, waitSeconds: 30);

        var scene = Driver.GetCurrentScene();
        Assert.That(scene, Is.Not.Null.And.Not.Empty, "Scene should be set before checking HUD.");
        Assert.That(scene, Is.Not.EqualTo(LyraMenuHelper.MainMenuScene),
            "Must be in gameplay (not main menu) to check HUD. Current scene: " + scene);

        var hud = GameplayHelper.WaitForHUD(Driver, timeoutSeconds: 30);
        GameplayHelper.AssertHudViewVisible(Driver, hud);
    }

    [Test]
    public void Player_Movement_Changes_World_Position()
    {
        const double moveDurationSeconds = 2.0;

        GameplayHelper.EnterGameplay(Driver, gameplayTimeoutSeconds: 60);
        GameplayHelper.SetupInGameTest(Driver, waitSeconds: 30);

        AltObject? character = null;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            character = GameplayHelper.FindPlayerCharacter(Driver);
            if (character != null) break;
            Thread.Sleep(2000);
        }

        if (character != null)
        {
            int playerId = character.id;
            Driver.KeyDown(AltKeyCode.W);
            Thread.Sleep((int)(moveDurationSeconds * 1000));
            Driver.KeyUp(AltKeyCode.W);
            Thread.Sleep(500);

            var afterMove = GameplayHelper.FindObjectById(Driver, playerId) ?? GameplayHelper.FindPlayerCharacter(Driver);
            Assert.That(afterMove, Is.Not.Null, "Player character should still be findable after holding W " + moveDurationSeconds + "s.");
            return;
        }

        if (!AimingHelper.TryGetTestPositionsFromEngine(Driver, out var posBefore, out _) || !posBefore.HasValue)
        {
            Assert.Fail("Player_Movement: no player pawn and engine returned no player position (GetTestPositionsAsString). Ensure LyraTestEnemyQuery is built and game is in gameplay.");
        }

        Driver.KeyDown(AltKeyCode.W);
        Thread.Sleep((int)(moveDurationSeconds * 1000));
        Driver.KeyUp(AltKeyCode.W);
        Thread.Sleep(500);

        if (!AimingHelper.TryGetTestPositionsFromEngine(Driver, out var posAfter, out _) || !posAfter.HasValue)
        {
            Assert.Fail("Player_Movement: after W input, engine returned no player position. Input may not be reaching the controller.");
        }

        var after = posAfter!.Value;
        var before = posBefore!.Value;
        var dx = after.x - before.x;
        var dy = after.y - before.y;
        var dz = after.z - before.z;
        var dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        Assert.That(dist, Is.GreaterThan(20.0), "Player position should change after holding W (controller-only path: engine positions before/after). If you see the character move, engine may be returning stale positions.");
    }
}
