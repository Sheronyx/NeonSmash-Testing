using UnityEngine;

// Reine Score-Formel für den Wellen/Korb-Modus, isoliert vom Controller
// damit die exponentielle Multiplikator-Mathematik leicht von Hand nachrechenbar bleibt.
public static class WaveScoreCalculator
{
    public const int NormalPoints = 10;
    public const int SpecialPoints = 50;
    public const float MultiplierStep = 1.2f;

    public static int ComputeFinalScore(int normalCount, int specialCount, int multiplierCount)
    {
        int basePoints = normalCount * NormalPoints + specialCount * SpecialPoints;
        float multiplier = Mathf.Pow(MultiplierStep, multiplierCount);
        return Mathf.RoundToInt(basePoints * multiplier);
    }
}
