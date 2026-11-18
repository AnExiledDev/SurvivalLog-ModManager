using System;
using System.Collections.Generic;
using System.Linq;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// SAFE START and FIND THE PROBLEM MOD while the game runs. Notes how each session ends, moves the DLLs for each
/// start, and keeps the plan in a file, since every step needs the game started again. Main thread only.
/// </summary>
internal sealed class StartPlans
{
    private readonly SavedFile _markFile;
    private readonly SavedFile _planFile;
    private readonly PluginCatalog _catalog;
    private readonly Action<string> _log;

    /// <summary>The round this session runs, written in its mark so a crash counts against that round only.</summary>
    private readonly string? _loaded;
    private readonly AtFirstFrame _atFirstFrame;
    private StartPlan? _plan;

    /// <summary>The round the player can say how it went in this session; null once they have, or when this session runs none.</summary>
    private string? _answers;
    private bool _hasRoundCrashed;

    /// <summary>Shown instead of the plan once there is none: the safe start running, or a stopped search.</summary>
    private PlanView? _shown;
    private IReadOnlyList<string> _errorMods = Array.Empty<string>();
    private int _errorsNoted = -1;

    public StartPlans(SavedFile markFile, SavedFile planFile, PluginCatalog catalog, Action<string> log)
    {
        _markFile = markFile;
        _planFile = planFile;
        _catalog = catalog;
        _log = log;

        var last = SessionMark.Parse(markFile.Read());
        var read = CulpritSearch.AtLoad(last, PlanText.Read(planFile.Read()));

        _plan = read.Plan;
        _loaded = read.Loaded;
        _answers = read.Answers;
        _hasRoundCrashed = read.HasRoundCrashed;
        _atFirstFrame = read.Then;
        LastCrash = read.LastCrash;

        if (read.IsPlanChanged) Save();

        markFile.Save(SessionMark.Running(_loaded).ToText());
        log("last session " + (last == null ? "unknown" : last.IsClosed ? "closed normally" : "did not close normally") + "; start plan: " + Describe(_plan) + (_loaded != null ? ", running " + _loaded : ""));
    }

    /// <summary>The mods behind the newest errors of the last session, when it did not close normally and no search counted it; null otherwise.</summary>
    public IReadOnlyList<string>? LastCrash { get; }

    /// <summary>The search's or the safe start's mods whose DLLs could not be moved the last time it moved them.</summary>
    public IReadOnlyList<string> Failed { get; private set; } = Array.Empty<string>();

    public PlanView View => _shown ?? CulpritSearch.ViewOf(_plan, _answers, _hasRoundCrashed);

    /// <summary>The mods the search or the safe start moves, which nothing else may switch until it ends.</summary>
    public IReadOnlyList<string> Holds => _plan switch
    {
        StartPlan.Search search => search.Original,
        StartPlan.Safe safe => safe.Off,
        _ => Array.Empty<string>(),
    };

    public bool IsHolding(string guid) => Holds.Contains(guid);

    /// <summary>Why neither can start now, or null when both can.</summary>
    public StartRefusal? WhyNot(IReadOnlyList<ModEntry> mods) => CulpritSearch.WhyNot(mods);

    /// <summary>On the first frame, once every plugin has loaded, since plugin DLLs must not move while BepInEx still loads them.</summary>
    public void Start()
    {
        switch (_atFirstFrame, _plan)
        {
            case (AtFirstFrame.Arm, StartPlan.Search round):
                Arm(round);
                break;
            case (AtFirstFrame.SetRoundAgain, StartPlan.Search round):
                Failed = Lay(CulpritSearch.Layout(round, round));
                _plan = round with { IsArmed = false };
                Save();
                break;
            case (AtFirstFrame.GoOnAfterCrash, StartPlan.Search round):
                GoOn(round, CulpritSearch.After(round, true));

                // So the bar says why the search moved on without an answer.
                _hasRoundCrashed = true;
                break;
            case (AtFirstFrame.EndSafeStart, StartPlan.Safe safe):
                Failed = Lay(safe.Off.ToDictionary(guid => guid, _ => true));
                _shown = new PlanView.SafeNow(safe.Off);
                _plan = null;
                Save();
                break;
        }
    }

    public void SafeStart()
    {
        var mods = _catalog.Mods();

        if (IsRunning || WhyNot(mods) != null) return;

        var safe = CulpritSearch.SafeStart(mods);

        // The plan is saved before any DLL moves, so the next start always knows to put them back.
        if (Begin(safe)) Failed = Lay(safe.Off.ToDictionary(guid => guid, _ => false));
    }

    public void FindCulprit()
    {
        var mods = _catalog.Mods();

        if (IsRunning || WhyNot(mods) != null) return;

        var round = CulpritSearch.Begin(DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), mods);

        if (Begin(round)) Failed = Lay(CulpritSearch.Layout(round, round));
    }

    /// <summary>The player says how the round this session runs went. A crash needs no answer: the next start notices it.</summary>
    public void Answer(bool hasCrashed)
    {
        if (_plan is not StartPlan.Search round || _answers != round.Token) return;

        _log("round " + round.Token + (hasCrashed ? ": still broken" : ": no crash"));
        GoOn(round, CulpritSearch.After(round, hasCrashed));
    }

    /// <summary>Ends the search or the safe start before it is done, with every mod back on for the next start.</summary>
    public void Stop()
    {
        var layout = _plan switch
        {
            StartPlan.Search round => CulpritSearch.Layout(round, null),
            StartPlan.Safe safe => safe.Off.ToDictionary(guid => guid, _ => true),
            _ => null,
        };

        if (layout == null) return;

        _log("stopped " + Describe(_plan));
        Failed = Lay(layout);
        _shown = new PlanView.Stopped(_plan is StartPlan.Search);
        End();
    }

    /// <summary>The player has read what the search found, or that the safe start or the search ended.</summary>
    public void Dismiss()
    {
        if (_plan is StartPlan.Found or StartPlan.NotFound) End();

        _shown = null;
        Failed = Array.Empty<string>();
    }

    /// <summary>
    /// The game is quitting normally. A round set up for a crash that never came is set back to this round, so the
    /// next start runs it again; then the mark says the session closed.
    /// </summary>
    public void Quitting()
    {
        if (_plan is StartPlan.Search { IsArmed: true } round && Lay(CulpritSearch.Layout(round, round)).Count == 0)
        {
            _plan = round with { IsArmed = false };
            Save();
        }

        _markFile.Save(new SessionMark(true, _loaded, _errorMods).ToText());
    }

    /// <summary>Keeps the mark naming the mods behind the newest errors, so a crash after them can name them at the next start.</summary>
    public void NoteErrors(ProblemBook book)
    {
        if (book.Version == _errorsNoted) return;

        _errorsNoted = book.Version;

        var problems = book.All();

        if (!problems.Any(problem => problem.Level == ProblemLevel.Error)) return;

        var errorMods = SessionMark.ErrorModsOf(problems, _catalog.ErrorOwners());

        if (errorMods.SequenceEqual(_errorMods)) return;

        _errorMods = errorMods;
        _markFile.Save(new SessionMark(false, _loaded, errorMods).ToText());
    }

    private bool IsRunning => _plan is StartPlan.Search or StartPlan.Safe;

    /// <summary>Sets the DLLs up for the round after a crash. If any could not move, they are put back, so this start stays a plain round.</summary>
    private void Arm(StartPlan.Search round)
    {
        var failed = Lay(CulpritSearch.Layout(round, CulpritSearch.After(round, true)));

        if (failed.Count > 0)
        {
            Failed = failed;
            Lay(CulpritSearch.Layout(round, round));

            return;
        }

        _plan = round with { IsArmed = true };
        Save();
    }

    private void GoOn(StartPlan.Search from, StartPlan next)
    {
        Failed = Lay(CulpritSearch.Layout(from, next));
        _plan = next;
        _answers = null;
        _hasRoundCrashed = false;
        Save();
        _log("start plan now " + Describe(next));
    }

    /// <summary>False when the plan could not be saved; then nothing is set and no DLL may move.</summary>
    private bool Begin(StartPlan plan)
    {
        if (!_planFile.Save(PlanText.Write(plan)))
        {
            _log("could not save the start plan, so no mod was moved: " + Describe(plan));
            return false;
        }

        _plan = plan;
        _answers = null;
        _hasRoundCrashed = false;
        _shown = null;
        _log("start plan set: " + Describe(plan));
        return true;
    }

    private void End()
    {
        _plan = null;
        _answers = null;
        _hasRoundCrashed = false;
        Save();
    }

    private IReadOnlyList<string> Lay(IReadOnlyDictionary<string, bool> layout) => _catalog.SwitchAll(layout);

    private void Save()
    {
        if (!_planFile.Save(_plan == null ? "" : PlanText.Write(_plan))) _log("could not save the start plan, so the next start may not follow it");
    }

    private static string Describe(StartPlan? plan) => plan switch
    {
        null => "none",
        StartPlan.Safe safe => "safe start with " + safe.Off.Count + " mods off",
        StartPlan.Search round => "search round " + round.Token + (round.IsArmed ? " (set up for a crash)" : "") + ", " + round.Suspects.Count + " suspects, " + round.Off.Count + " off",
        StartPlan.Found found => "found " + string.Join(", ", found.Culprit),
        StartPlan.NotFound => "search ended with no mod to blame",
        _ => plan.GetType().Name,
    };
}
