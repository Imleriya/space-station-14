// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Respawn;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Mind;
using Robust.Shared.Player;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.SS220.DarkReaper;

public sealed class DarkReaperMajorRuleSystem : GameRuleSystem<DarkReaperMajorRuleComponent>
{
    [Dependency] private readonly SpecialRespawnSystem _specialRespawn = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;

    private readonly ISawmill _sawmill = Logger.GetSawmill("DarkReaperMajorRule");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayersSpawning);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        //SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);

        //SubscribeLocalEvent<DarkReaperRuneComponent, GhostRoleSpawnerUsedEvent>(OnPlayersGhostSpawning);
        //SubscribeLocalEvent<DarkReaperRuneComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var mindQuery = GetEntityQuery<MindComponent>();
        foreach (var reaperRule in EntityQuery<DarkReaperMajorRuleComponent>())
        {
            var mindId = reaperRule.ReaperMind;
            if (mindQuery.TryGetComponent(mindId, out var mind) && mind.Session != null)
            {
                ev.AddLine(Loc.GetString("darkreaper-roundend-user", ("user", mind.Session.Name)));
            }
        }
    }

    private void OnPlayersSpawning(RulePlayerSpawningEvent ev)
    {
        var query = EntityQueryEnumerator<DarkReaperMajorRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            // Try find primary grid of a station
            EntityUid? grid = null;
            EntityUid? map = null;
            foreach (var station in _station.GetStationsSet())
            {
                if (!TryComp<StationDataComponent>(station, out var data))
                    continue;

                grid = _station.GetLargestGrid(data);
                if (!grid.HasValue)
                    continue;

                map = Transform(grid.Value).MapUid;
                if (!map.HasValue)
                    continue;

                break;
            }

            if (!grid.HasValue || !map.HasValue)
            {
                _chatManager.SendAdminAnnouncement(Loc.GetString("darkreaper-failed-spawn-grid"));
                return;
            }

            if (!_specialRespawn.TryFindRandomTile(grid.Value, map.Value, 30, out var runeCoords))
            {
                _chatManager.SendAdminAnnouncement(Loc.GetString("darkreaper-failed-spawn-tile"));
                return;
            }

            _sawmill.Info($"Spawning {comp.RunePrototypeId} at {runeCoords}");
            var rune = Spawn(comp.RunePrototypeId, runeCoords);

            var eligible = _antagSelection.GetEligibleSessions(ev.PlayerPool, "DarkReaper");
            var chosenPlayer = _antagSelection.ChooseAntags(1, eligible, ev.PlayerPool).FirstOrDefault();
            if (chosenPlayer == null)
                continue;

            ev.PlayerPool.Remove(chosenPlayer);
            GameTicker.PlayerJoinGame(chosenPlayer);

            var newMind = _mind.CreateMind(chosenPlayer.UserId, Loc.GetString("darkreaper-mind-name"));
            _mind.SetUserId(newMind, chosenPlayer.UserId);
            _mind.TransferTo(newMind, rune);
        }
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        var query = EntityQueryEnumerator<DarkReaperMajorRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
            {
                continue;
            }

            var minPlayers = comp.MinPlayers;
            if (!ev.Forced && ev.Players.Length < minPlayers)
            {
                _chatManager.SendAdminAnnouncement(Loc.GetString("darkreaper-not-enough-ready-players",
                    ("readyPlayersCount", ev.Players.Length), ("minimumPlayers", minPlayers)));
                ev.Cancel();
                continue;
            }

            if (ev.Players.Length != 0)
                continue;

            _chatManager.DispatchServerAnnouncement(Loc.GetString("darkreaper-no-one-ready"));
            ev.Cancel();
        }
    }
}
