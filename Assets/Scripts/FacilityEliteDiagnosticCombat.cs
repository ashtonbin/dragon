using System;
using System.Collections;
using UnityEngine;

/// <summary>Used only by explicit CLI regressions to complete the final encounter through actual regional damage.</summary>
public static class FacilityEliteDiagnosticCombat
{
    static int shot = 1900000000;

    public static IEnumerator DefeatSquad(FacilityStoryDirector director)
    {
        var waves = director != null ? director.policeWaves : null;
        if (waves == null || !director.HasCash || !director.HasGold || !waves.EliteAssaultActive)
            throw new InvalidOperationException("The diagnostic must collect both real loot items before fighting the final squad.");
        float deadline = Time.realtimeSinceStartup + 16f;
        while (!waves.EliteDeploymentComplete && Time.realtimeSinceStartup < deadline) yield return null;
        if (!waves.EliteDeploymentComplete) throw new InvalidOperationException("The final squad did not finish spawning.");
        foreach (var elite in waves.GetComponentsInChildren<FacilityEliteSwat>(true))
        {
            var actor = elite.GetComponent<FacilityHumanoid>();
            if (actor.IsDead) continue;
            elite.GetComponent<FacilityPoliceOfficer>().enabled = false;
            actor.enabled = false;
            Collider head = actor.head.GetComponent<Collider>();
            for (int hit = 0; hit < 4 && !actor.IsDead; hit++)
            {
                actor.ReceivePellet(++shot, head.bounds.center, actor.transform.forward, head);
                yield return null;
            }
            if (!actor.IsDead) throw new InvalidOperationException("The diagnostic could not defeat an elite through its actual armored head.");
        }
        yield return null;
        if (!waves.EliteTeamDefeated || !director.ExtractionUnlocked)
            throw new InvalidOperationException("Extraction did not unlock after the complete squad was killed.");
    }
}
