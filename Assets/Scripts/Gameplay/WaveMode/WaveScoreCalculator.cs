using UnityEngine;

// Reine Score-Formel für den Wellen/Korb-Modus, isoliert vom Controller
// damit die exponentielle Multiplikator-Mathematik leicht von Hand nachrechenbar bleibt.
public static class WaveScoreCalculator
{
    public const int NormalPoints = 10;
    public const int SpecialPoints = 50;
    public const float MultiplierStep = 1.2f;

    public static float ComputeMultiplier(int multiplierCount) => Mathf.Pow(MultiplierStep, multiplierCount);

    public static int ComputeFinalScore(int normalCount, int specialCount, int multiplierCount)
    {
        int basePoints = normalCount * NormalPoints + specialCount * SpecialPoints;
        return Mathf.RoundToInt(basePoints * ComputeMultiplier(multiplierCount));
    }
}

// Aufschlüsselung für den Ergebnis-Screen (Element-Breakdown-Reihen).
public struct WaveResultBreakdown
{
    public int NormalCount;
    public int SpecialCount;
    public int MultiplierCount;
}
