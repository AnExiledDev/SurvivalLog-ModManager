using System;
using System.IO;
using ExiledToolkit.Adapters;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// page.js in the root document of the game's web UI, above the game's own pages. A page reload drops it, so it is
/// checked for every couple of seconds and sent again when missing.
/// </summary>
internal sealed class ModsPage
{
    private const float CheckSeconds = 2f;
    private const string ScriptResource = "ModManager.page.js";

    private readonly Action<string> _log;
    private string? _script;
    private float _nextCheckAt;
    private bool _hasReportedRunFailure;

    public ModsPage(Action<string> log)
    {
        _log = log;
    }

    public void KeepInstalled(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;
        Run(PageScript.Probe);
    }

    /// <summary>The script returns at once when it is already there, so sending it again is harmless.</summary>
    public bool Install() => Run(Script());

    public bool Run(string javaScript)
    {
        var hasRun = WebUi.TryRun(javaScript, out var failure);

        if (failure != null && !_hasReportedRunFailure) _log("could not run script in the web UI (reported once): " + failure.Message);

        _hasReportedRunFailure |= failure != null;

        return hasRun;
    }

    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(ModsPage).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
