using System;
using UnityEngine;

/// <summary>
/// Retired instant-hit diagnostic. Its script type/GUID remains valid for existing scene references.
/// Both -facilityCombatTest and -facilityImpactTest now run FacilityImpactSmokeTest, which verifies
/// actual buckshot flight, regional damage, nonlethal recovery and supported surface impacts.
/// </summary>
[Obsolete("Use FacilityImpactSmokeTest. The legacy CLI flag is an alias for its integration check.")]
public sealed class FacilityCombatSmokeTest : MonoBehaviour
{
}
