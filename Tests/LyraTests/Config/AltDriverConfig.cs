namespace LyraTests.Config;

public static class AltDriverConfig
{
    static string Env(string key, string defaultValue) =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)) ? defaultValue : Environment.GetEnvironmentVariable(key)!.Trim();

    static string[] EnvList(string key, string defaultCommaSeparated)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw)) raw = defaultCommaSeparated;
        return raw!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static string Host => Env("ALTTESTER_HOST", "127.0.0.1");
    public static int Port => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_PORT"), out var p) ? p : 13000;
    public static string AppName => Env("ALTTESTER_APP_NAME", "__default__");
    public static int ConnectTimeout => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_CONNECT_TIMEOUT"), out var t) ? t : 60;

    public static string? AimTestMap => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALTTESTER_AIM_TEST_MAP")) ? null : Environment.GetEnvironmentVariable("ALTTESTER_AIM_TEST_MAP")!.Trim();
    public static string? AimTestMapOptions => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALTTESTER_AIM_TEST_MAP_OPTIONS")) ? null : Environment.GetEnvironmentVariable("ALTTESTER_AIM_TEST_MAP_OPTIONS")!.Trim();
    public static bool AimTestTwoPlayers => string.Equals(Environment.GetEnvironmentVariable("ALTTESTER_AIM_TEST_TWO_PLAYERS"), "1", StringComparison.OrdinalIgnoreCase);

    public static string ExperienceButton => Env("ALTTESTER_EXPERIENCE_BUTTON", "Control");
    public static int ExperienceTileIndex => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_EXPERIENCE_TILE_INDEX"), out var i) && i >= 0 ? i : 1;
    public static string[] ExperienceTileNameSubstrings => EnvList("ALTTESTER_EXPERIENCE_TILE_NAMES", "Control,Convolution");
    public static string[] QuickPlayExcludeSubstrings => EnvList("ALTTESTER_QUICKPLAY_EXCLUDE_NAMES", "Quick,Quickplay,Elimination");
    public static float ExperienceTileXBandPx => float.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_EXPERIENCE_TILE_X_BAND"), out var f) && f > 0 ? f : 120f;
    public static float ExperienceMinScreenY => float.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_EXPERIENCE_MIN_SCREEN_Y"), out var y) && y >= 0 ? y : 100f;
    public static int ExperienceAfterClickSleepMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_EXPERIENCE_AFTER_CLICK_MS"), out var ms) && ms >= 0 ? ms : 1000;
    public static int ExperienceBeforeTapMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_EXPERIENCE_BEFORE_TAP_MS"), out var ms) && ms >= 0 ? ms : 450;
    public static string[] LaunchButtonNames => EnvList("ALTTESTER_LAUNCH_BUTTON_NAMES", "Launch,Start,StartGame,Play,Confirm,ConfirmButton,PrimaryButton");

    public static string? BotCountControl => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALTTESTER_BOT_COUNT_CONTROL")) ? null : Environment.GetEnvironmentVariable("ALTTESTER_BOT_COUNT_CONTROL")!.Trim();
    public static string BotCountControlDefault => Env("ALTTESTER_BOT_COUNT_CONTROL_DEFAULT", "CHANGE");
    public static string? BotCountOptionOne => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALTTESTER_BOT_COUNT_OPTION_ONE")) ? null : Environment.GetEnvironmentVariable("ALTTESTER_BOT_COUNT_OPTION_ONE")!.Trim();
    public static string[] BotCountOptionOneFallbacks => EnvList("ALTTESTER_BOT_COUNT_OPTION_ONE_FALLBACKS", "1,One,Option_1,1 Bot,1 bot");
    public static int HostAfterClickSleepMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_HOST_AFTER_CLICK_MS"), out var h) && h >= 0 ? h : 2000;
    public static int HostAfterBotCountSleepMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_HOST_AFTER_BOT_COUNT_MS"), out var b) && b >= 0 ? b : 500;
    public static int BotCountAfterControlClickMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_BOT_COUNT_AFTER_CONTROL_MS"), out var c) && c >= 0 ? c : 400;
    public static int BotCountAfterOptionClickMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_BOT_COUNT_AFTER_OPTION_MS"), out var o) && o >= 0 ? o : 200;

    public static string NetworkElement => Env("ALTTESTER_NETWORK_ELEMENT", "NETWORK");
    public static string? PlayLyraButton => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALTTESTER_PLAY_LYRA_BUTTON")) ? null : Environment.GetEnvironmentVariable("ALTTESTER_PLAY_LYRA_BUTTON")!.Trim();
    public static string HostButton => Env("ALTTESTER_HOST_BUTTON", "HostButton");
    public static string StartGameButton => Env("ALTTESTER_START_GAME_BUTTON", "StartGameButton");
    public static string[] StartGameButtonFallbacks => EnvList("ALTTESTER_START_GAME_FALLBACKS", "StartGame,PlayLyra,Play Lyra,PrimaryButton,W_StartGame");
    public static int StartGameAfterClickSleepMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_START_GAME_AFTER_CLICK_MS"), out var s) && s >= 0 ? s : 2000;
    public static int WaitForHostTimeoutSeconds => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_WAIT_FOR_HOST_TIMEOUT"), out var t) && t > 0 ? t : 10;
    public static int ExperienceWaitIntervalMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_EXPERIENCE_WAIT_INTERVAL_MS"), out var w) && w > 0 ? w : 500;
    public static string? QuickPlayButton => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALTTESTER_QUICK_PLAY_BUTTON")) ? null : Environment.GetEnvironmentVariable("ALTTESTER_QUICK_PLAY_BUTTON")!.Trim();
    public static string QuickPlayButtonDefault => Env("ALTTESTER_QUICK_PLAY_BUTTON_DEFAULT", "QuickPlayButton");
    public static string MainMenuScene => Env("ALTTESTER_MAIN_MENU_SCENE", "L_LyraFrontEnd");

    public static int SetupPlayerWaitPollMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_SETUP_PLAYER_WAIT_POLL_MS"), out var p) && p > 0 ? p : 500;
    public static int SetupPlayerWaitMaxIterations => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_SETUP_PLAYER_WAIT_ITERATIONS"), out var n) && n > 0 ? n : 40;
    public static int SetupAfterPlayerWaitSleepMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_SETUP_AFTER_PLAYER_WAIT_MS"), out var s) && s >= 0 ? s : 1500;
    public static int SetupCheatMaxAttempts => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_SETUP_CHEAT_ATTEMPTS"), out var a) && a > 0 ? a : 8;
    public static int SetupCheatDelayMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_SETUP_CHEAT_DELAY_MS"), out var d) && d >= 0 ? d : 3000;

    public static int QuickPlayBeforeWaitMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_QUICKPLAY_BEFORE_WAIT_MS"), out var q) && q >= 0 ? q : 2000;
    public static int QuickPlayAfterClickSleepMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_QUICKPLAY_AFTER_CLICK_MS"), out var qa) && qa >= 0 ? qa : 1500;
    public static int QuickPlayPollIntervalMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_QUICKPLAY_POLL_MS"), out var qp) && qp > 0 ? qp : 500;

    public static int DismissOverlaySleepMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_DISMISS_OVERLAY_MS"), out var d) && d >= 0 ? d : 200;
    public static int GoToQuickPlayAfterStartMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_GO_QUICKPLAY_AFTER_START_MS"), out var g) && g >= 0 ? g : 2000;
    public static int GoToControlAfterHostMs => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_GO_CONTROL_AFTER_HOST_MS"), out var c) && c >= 0 ? c : 1500;

    public static string ButtonBorder => Env("ALTTESTER_BUTTON_BORDER", "ButtonBorder");
    public static string SubmenuQuickplayButton => Env("ALTTESTER_SUBMENU_QUICKPLAY_BUTTON", "QuickplayButton");
    public static int MiddleButtonBorderIndex => int.TryParse(Environment.GetEnvironmentVariable("ALTTESTER_MIDDLE_BUTTON_INDEX"), out var m) && m >= 0 ? m : 1;
}
