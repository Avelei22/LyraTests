This is a project testing LyraStarterGame base functionalities
Requirements:
- LyraStarterGame
- AltTester for unreal with a license
- Unreal v5.3.2

How to run:
1. Lyra + AltTester: Lyra (UE 5.3.2) with AltTester plugin, game running so the agent is up.
2. Copy this repo’s ProjectMods/Testing/ into your Lyra project as Source/LyraGame/Testing/ (all six .h/.cpp). Add Testing to the Lyra game module build, rebuild Lyra.
3. From repo root: dotnet restore Tests\LyraTests\LyraTests.csproj && dotnet build Tests\LyraTests\LyraTests.csproj && dotnet test Tests\LyraTests\LyraTests.csproj (or open LyraTests.sln and run tests in Visual Studio). Default connection 127.0.0.1:13000.


Smoke Test Design:
| Test | What it checks | Rationale / risk covered |
|------|----------------|---------------------------|
| Game_IsRunning_AndResponds | AltDriver gets at least one enabled element from the game. | High value: If the game or AltTester agent is not running or connected, every other test is meaningless. Covers "is the automation pipeline alive?" |
| Application_Has_Valid_Viewport | Viewport size > 0 and within a reasonable range (e.g. 7680×4320). | High value: Invalid or zero viewport would break any screen-position or input logic. Covers "did the app start correctly?" |
| MainMenu_StartGame_Enters_Gameplay | Main menu → Start Game → Quick Play → scene leaves the front-end (current scene is not the main menu). | High value: Core user path into a session. Covers "can the player get into gameplay at all?" (level load, UI, flow). |
| Gameplay_HUD_Is_Visible | In gameplay, HUD exists, is enabled, and its screen position is within the viewport (actually on screen). | High value: Confirms the game HUD (ALyraHUD) is present and displayed. Covers "is the game HUD up?" |
| Player_Movement_Changes_World_Position | After entering gameplay, find the player, hold W for a duration, then confirm the player character is still findable (movement and possession work). | High value: Possession, movement component, and input. Covers "does the controlled pawn actually move?" |

Code structure: Test logic is separated from screen/flow abstractions and helpers. `LyraMenuHelper` handles main menu and Start/Quick Play; `GameplayHelper` handles entering gameplay, finding player/enemy, and HUD checks. Smoke tests call these helpers so the suite can scale (e.g. 50+ tests) without duplicating flows.

Aim-shoot-kill design:
The aiming helper accepts a reference to a target actor (enemy bot) or target world coordinates. It centers the crosshair on the target by:

1. Camera-to-target direction: Computing (pitch, yaw) from the camera position (PlayerCameraManager or controller focal location) to the target position (with a height offset for torso/head).
2. Applying rotation: Using the PlayerController — either SetControlRotation or AddYawInput / AddPitchInput — so the solution does not depend on raw mouse timing under Lyra's Enhanced Input.
3. Game Side support: Used for direct look-at (SetLocalPlayerLookAtWorldPosition), invincibility, infinite ammo, and optional continuous aim+fire. This makes automation robust under Enhanced Input and when the pawn is not exposed to AltTester (controller-only mode).

The method is reusable: it works for any target in any position.

Robustness: 
- Target movement: The test loop re-aims every iteration using the current enemy position (FindObjectById or engine-reported positions from LyraTestEnemyQuery).
- Enhanced Input under automation: Raw key/mouse timing for aim is avoided by using controller rotation or the test support subsystem; Fire() still uses key simulation (Mouse0).
- Timing and synchronization: The test fires at a fixed interval and polls for kill confirmation (target gone or enemy count drop) with a timeout so the test either passes or fails with a clear assertion.

Kill validation:
1. AltObject path (enemy found via AltTester):

- PollUntilTargetDestroyed(Driver, targetEnemyId, …) polls the object hierarchy; when the target's object id no longer appears, the actor was destroyed (eliminated). That is treated as kill confirmed.
- FindObjectById(Driver, targetEnemyId) == null also means the target is gone; kill confirmed is set and, if applicable, the test switches to the next enemy.
- The test asserts Assert.That(killConfirmed, Is.True, …) so if the enemy never disappears within the shoot timeout, the test fails with a message (e.g. last enemy id, or "no other players after kill").

2. Engine path (controller-only / no pawn, using LyraTestEnemyQuery):

- The game-side helper returns current "other player" positions. The current enemy count is compared to the initial count.
- When posList.Count < initialSubsystemEnemyCount for a required number of consecutive polls, kill confirmed is set (one or more enemies were eliminated).
- If the count never drops, the assertion fails with a message explaining that the enemy count never dropped (aim may be off or match has respawns).

Challenges: 
- Lyra Enhanced Input under automation: Input can lag or behave differently. Controller-level APIs (SetControlRotation / AddYawInput / AddPitchInput) or the optional test support subsystem are used so the solution does not depend on raw input timing for aim.
- No kill feed or game event API: "Target object gone from hierarchy" or "engine enemy count dropped" is used as the kill signal.
- Player/enemy identification: The player is identified via IsPlayerControlled (CallComponentMethod on the pawn) or controller→GetPawn(); enemies are found via FindObjectsWhichContain (LyraCharacter) excluding the player, or via LyraTestEnemyQuery when the pawn is not exposed to AltTester.

Relialability:
- Main caveat is testing in 2v2 enironment. Best case scenario (also for future tests) creating a plain map with one player agains one bot and withouth the "start game" waiting time.
- Obstacles and the bot running behind them. In this iteration movement across the map was not introduced (hence a plain map would greatly increase reliability)
- Added invincibility and infinite ammuniton to ensure player is not killed by enemy and does not run out of bullets
- If the shot never registers, kill is never confirmed and the test fails with a clear assertion.
- retry logic

Invasivness:

- Existing Lyra game classes and Blueprints are not modified.
- Isolated additions in the Lyra project are limited to a Testing/ module: LyraTestSupportSubsystem, LyraTestEnemyQuery, LyraTestSupportAimTickComponent. These are self-contained and can be removed by deleting the Testing folder.

By default the tests connect to `127.0.0.1:13000`. Override with environment variables (e.g. `ALTTESTER_HOST`, `ALTTESTER_PORT`, `ALTTESTER_APP_NAME`, `ALTTESTER_CONNECT_TIMEOUT`). Optional: `ALTTESTER_AIM_TEST_MAP` to load a map directly for the aim test; `ALTTESTER_EXPERIENCE_BUTTON` for the tile to click after Start Game.


Known limitations and final words:
- As to be fair to the task it took me about ~6.5h with testing but wanted to show what I can achieve not knowing alttester and lyra project in this timeframe even if the solution is far from perfect.
- Initial problems with controls
- No dedicated test maps
- Definetly using timeouts and waiting for screens or test starts could be greatly improved

Thanks for reviewing it. Definetly a lot can be improved but I wanted to show what I can do in that timeframe. If I were to upscale it I would definetly make an overheading collecting data of each test and constructing a report.
I would also make a lot better configuration settings. I am uploading all of the code so you can see some of my fail attempts aswell there at trying different solutions.
