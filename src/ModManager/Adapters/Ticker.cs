using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModManager.Adapters;

/// <summary>
/// The Unity object that hands frames to the Mods screen. Each distinct failure is logged once and none is rethrown
/// into the game; one that repeats every frame would otherwise flood the log.
/// </summary>
public sealed class Ticker : MonoBehaviour
{
    private const int MaxReportedFailures = 20;

    internal static ModsSession? Session;
    internal static Action<string> Log = _ => { };

    private static readonly HashSet<string> ReportedFailures = new();

    public Ticker(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        var session = Session;

        if (session == null) return;

        try
        {
            session.Update(Time.realtimeSinceStartup, Time.frameCount);
        }
        catch (Exception ex)
        {
            ReportOnce(ex);
        }
    }

    private static void ReportOnce(Exception ex)
    {
        var key = ex.GetType().Name + ": " + ex.Message;

        if (ReportedFailures.Count >= MaxReportedFailures || !ReportedFailures.Add(key)) return;

        Log("Mods screen update failed (each kind of failure is logged once): " + ex);
    }
}
