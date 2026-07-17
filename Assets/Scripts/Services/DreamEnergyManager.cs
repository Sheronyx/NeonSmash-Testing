using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

// Traumenergie: eigene Ressource neben Coins, gleiche Speicher-/Sync-Struktur wie CoinManager
// (lokal per PlayerPrefs + Cloud Save Backup). Wird nach jeder Infinity-Mode-Session anhand des
// Endscores vergeben (siehe CalculateReward/OnGameFinished, aufgerufen von MixedPointSpawner
// direkt bei Game Over, analog zu AchievementManager/MissionManager.OnGameFinished).
public static class DreamEnergyManager
{
    const string PrefKey  = "dream_energy_balance";
    const string CloudKey = "dreamEnergy";

    // Ensures cloud saves are sequential — prevents out-of-order writes
    // when multiple AddDreamEnergy calls fire rapidly.
    static readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

    public static event Action<int> OnDreamEnergyChanged;

    public static int Balance => PlayerPrefs.GetInt(PrefKey, 0);

    // Design-Tabelle: Score (0..10000, 10er-Schritte) -> Traumenergie-Gesamtbetrag
    // (48 Fix + stufenweise wachsender variabler Anteil). Index i entspricht Score i*10.
    private const int ScoreStep      = 10;
    private const int MaxTableScore  = 10000;

    private static readonly int[] RewardTable =
    {
        48, 52, 57, 61, 66, 70, 75, 79, 84, 88, 93, 97, 102, 106, 111, 115, 120, 124, 129, 133,
        138, 142, 147, 151, 153, 156, 158, 160, 162, 165, 167, 169, 171, 174, 176, 178, 180, 183, 185, 187,
        190, 192, 194, 196, 199, 201, 203, 205, 208, 210, 212, 215, 217, 219, 221, 224, 226, 228, 230, 233,
        235, 237, 239, 242, 244, 246, 249, 251, 253, 255, 258, 260, 262, 264, 267, 269, 271, 274, 276, 278,
        280, 283, 285, 287, 289, 292, 294, 296, 298, 301, 303, 306, 309, 311, 314, 317, 320, 322, 325, 328,
        331, 333, 336, 339, 342, 344, 347, 350, 353, 355, 358, 361, 364, 366, 369, 372, 375, 377, 380, 383,
        386, 388, 391, 394, 397, 399, 402, 405, 408, 410, 413, 416, 419, 421, 424, 427, 430, 432, 435, 438,
        441, 443, 446, 449, 452, 454, 457, 460, 463, 465, 468, 472, 475, 479, 483, 486, 490, 493, 497, 501,
        504, 508, 512, 515, 519, 523, 526, 530, 534, 537, 541, 544, 548, 552, 555, 559, 563, 566, 570, 574,
        577, 581, 584, 588, 592, 595, 599, 603, 606, 610, 614, 617, 621, 625, 628, 632, 635, 639, 643, 646,
        650, 654, 657, 661, 665, 668, 672, 675, 679, 683, 686, 690, 694, 697, 701, 705, 708, 712, 715, 719,
        723, 726, 730, 734, 737, 741, 745, 748, 752, 756, 759, 763, 766, 770, 774, 777, 781, 785, 788, 792,
        796, 799, 803, 806, 810, 814, 817, 821, 825, 828, 832, 836, 839, 843, 847, 850, 854, 857, 861, 865,
        868, 872, 876, 879, 883, 887, 890, 894, 897, 901, 905, 908, 912, 916, 919, 923, 927, 930, 934, 938,
        941, 945, 948, 952, 956, 959, 963, 966, 970, 973, 976, 979, 983, 986, 989, 993, 996, 999, 1002, 1006,
        1009, 1012, 1015, 1019, 1022, 1025, 1029, 1032, 1035, 1038, 1042, 1045, 1048, 1052, 1055, 1058, 1061, 1065, 1068, 1071,
        1074, 1078, 1081, 1084, 1088, 1091, 1094, 1097, 1101, 1104, 1107, 1111, 1114, 1117, 1120, 1124, 1127, 1130, 1134, 1137,
        1140, 1143, 1147, 1150, 1153, 1156, 1160, 1163, 1166, 1170, 1173, 1176, 1179, 1183, 1186, 1189, 1193, 1196, 1199, 1202,
        1206, 1209, 1212, 1215, 1219, 1222, 1225, 1229, 1232, 1235, 1238, 1242, 1245, 1248, 1252, 1255, 1258, 1261, 1265, 1268,
        1271, 1274, 1278, 1281, 1284, 1288, 1291, 1294, 1297, 1301, 1304, 1307, 1311, 1314, 1317, 1321, 1324, 1327, 1330, 1334,
        1337, 1340, 1344, 1347, 1350, 1354, 1357, 1360, 1363, 1367, 1370, 1373, 1377, 1380, 1383, 1387, 1390, 1393, 1396, 1400,
        1403, 1406, 1410, 1413, 1416, 1420, 1423, 1426, 1429, 1433, 1436, 1439, 1443, 1446, 1449, 1453, 1456, 1459, 1462, 1466,
        1469, 1472, 1476, 1479, 1482, 1486, 1489, 1492, 1495, 1499, 1502, 1505, 1509, 1512, 1515, 1519, 1522, 1525, 1528, 1532,
        1535, 1538, 1542, 1545, 1548, 1552, 1555, 1558, 1561, 1565, 1568, 1571, 1575, 1578, 1581, 1585, 1588, 1591, 1594, 1598,
        1601, 1604, 1608, 1611, 1614, 1618, 1621, 1624, 1627, 1631, 1634, 1637, 1641, 1644, 1647, 1651, 1654, 1657, 1660, 1664,
        1667, 1670, 1674, 1677, 1680, 1683, 1687, 1690, 1693, 1697, 1700, 1703, 1707, 1710, 1713, 1716, 1720, 1723, 1726, 1730,
        1733, 1736, 1740, 1743, 1746, 1749, 1753, 1756, 1759, 1763, 1766, 1769, 1773, 1776, 1779, 1782, 1786, 1789, 1792, 1796,
        1799, 1802, 1806, 1809, 1812, 1815, 1819, 1822, 1825, 1829, 1832, 1835, 1839, 1842, 1845, 1848, 1852, 1855, 1858, 1862,
        1865, 1868, 1872, 1875, 1878, 1881, 1885, 1888, 1891, 1895, 1898, 1901, 1905, 1908, 1911, 1914, 1918, 1921, 1924, 1928,
        1931, 1934, 1938, 1941, 1944, 1947, 1951, 1954, 1957, 1961, 1964, 1967, 1971, 1974, 1977, 1980, 1984, 1987, 1990, 1994,
        1997, 2000, 2004, 2007, 2010, 2013, 2017, 2020, 2023, 2027, 2030, 2033, 2037, 2040, 2043, 2046, 2050, 2053, 2056, 2060,
        2063, 2066, 2070, 2073, 2076, 2079, 2083, 2086, 2089, 2093, 2096, 2099, 2103, 2106, 2109, 2112, 2116, 2119, 2122, 2126,
        2129, 2132, 2136, 2139, 2142, 2145, 2149, 2152, 2155, 2159, 2162, 2165, 2169, 2172, 2175, 2178, 2182, 2185, 2188, 2192,
        2195, 2198, 2202, 2205, 2208, 2212, 2215, 2218, 2221, 2225, 2228, 2231, 2235, 2238, 2241, 2245, 2248, 2251, 2254, 2258,
        2261, 2264, 2268, 2271, 2274, 2278, 2281, 2284, 2287, 2291, 2294, 2297, 2301, 2304, 2307, 2311, 2314, 2317, 2320, 2324,
        2327, 2330, 2334, 2337, 2340, 2344, 2347, 2350, 2353, 2357, 2360, 2363, 2367, 2370, 2373, 2377, 2380, 2383, 2386, 2390,
        2393, 2396, 2400, 2403, 2406, 2410, 2413, 2416, 2419, 2423, 2426, 2429, 2433, 2436, 2439, 2443, 2446, 2449, 2452, 2456,
        2459, 2462, 2466, 2469, 2472, 2476, 2479, 2482, 2485, 2489, 2492, 2495, 2499, 2502, 2505, 2509, 2512, 2515, 2518, 2522,
        2525, 2528, 2532, 2535, 2538, 2542, 2545, 2548, 2551, 2555, 2558, 2561, 2565, 2568, 2571, 2575, 2578, 2581, 2584, 2588,
        2591, 2594, 2598, 2601, 2604, 2608, 2611, 2614, 2617, 2621, 2624, 2627, 2631, 2634, 2637, 2641, 2644, 2647, 2650, 2654,
        2657, 2660, 2664, 2667, 2670, 2674, 2677, 2680, 2683, 2687, 2690, 2693, 2697, 2700, 2703, 2707, 2710, 2713, 2716, 2720,
        2723, 2726, 2730, 2733, 2736, 2740, 2743, 2746, 2749, 2753, 2756, 2759, 2763, 2766, 2769, 2773, 2776, 2779, 2782, 2786,
        2789, 2792, 2796, 2799, 2802, 2806, 2809, 2812, 2815, 2819, 2822, 2825, 2829, 2832, 2835, 2839, 2842, 2845, 2848, 2852,
        2855, 2858, 2862, 2865, 2868, 2872, 2875, 2878, 2881, 2885, 2888, 2891, 2895, 2898, 2901, 2905, 2908, 2911, 2914, 2918,
        2921, 2924, 2928, 2931, 2934, 2938, 2941, 2944, 2947, 2951, 2954, 2957, 2961, 2964, 2967, 2971, 2974, 2977, 2980, 2984,
        2987, 2990, 2994, 2997, 3000, 3004, 3007, 3010, 3013, 3017, 3020, 3023, 3027, 3030, 3033, 3037, 3040, 3043, 3046, 3050,
        3053, 3056, 3060, 3063, 3066, 3070, 3073, 3076, 3079, 3083, 3086, 3089, 3093, 3096, 3099, 3103, 3106, 3109, 3112, 3116,
        3119, 3122, 3126, 3129, 3132, 3136, 3139, 3142, 3145, 3149, 3152, 3155, 3159, 3162, 3165, 3169, 3172, 3175, 3178, 3182,
        3185, 3188, 3192, 3195, 3198, 3202, 3205, 3208, 3211, 3215, 3218, 3221, 3225, 3228, 3231, 3235, 3238, 3241, 3244, 3248,
        3251, 3254, 3258, 3261, 3264, 3268, 3271, 3274, 3277, 3281, 3284, 3287, 3291, 3294, 3297, 3301, 3304, 3307, 3310, 3314,
        3317,
    };

    // Rundet auf die nächstniedrigere 10er-Stufe ab (Score-Erhöhungen sind im Spiel immer
    // Vielfache von 10, außer bei krummen Sequence-Bonus-Rundungen — dafür ist das Abrunden
    // hier die sichere Absicherung) und klemmt auf den Tabellenbereich.
    public static int CalculateReward(int score)
    {
        int clamped = Mathf.Clamp(score, 0, MaxTableScore);
        int index   = clamped / ScoreStep;
        return RewardTable[index];
    }

    // Berechnet UND vergibt sofort die Traumenergie fürs beendete Spiel — Rückgabewert dient
    // nur der Anzeige (Score Canvas), ohne dass dort nochmal neu gerechnet werden muss.
    public static int OnGameFinished(int score)
    {
        int reward = CalculateReward(score);
        AddDreamEnergy(reward);
        return reward;
    }

    public static void AddDreamEnergy(int amount)
    {
        if (amount <= 0) return;
        int newBalance = Balance + amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnDreamEnergyChanged?.Invoke(newBalance);
        _ = SaveToCloudAsync();
        Debug.Log($"[DreamEnergy] +{amount} → {newBalance}");
    }

    public static bool TrySpendDreamEnergy(int amount)
    {
        if (Balance < amount) return false;
        int newBalance = Balance - amount;
        PlayerPrefs.SetInt(PrefKey, newBalance);
        PlayerPrefs.Save();
        OnDreamEnergyChanged?.Invoke(newBalance);
        _ = SaveToCloudAsync();
        return true;
    }

    public static async Task LoadFromCloudAsync()
    {
        try
        {
            var result = await CloudSaveService.Instance.Data.Player.LoadAsync(
                new HashSet<string> { CloudKey });

            if (result.TryGetValue(CloudKey, out var item))
            {
                int cloudBalance = item.Value.GetAs<int>();
                if (cloudBalance > Balance)
                {
                    PlayerPrefs.SetInt(PrefKey, cloudBalance);
                    PlayerPrefs.Save();
                    OnDreamEnergyChanged?.Invoke(cloudBalance);
                    Debug.Log($"[DreamEnergy] Cloud wiederhergestellt: {cloudBalance}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DreamEnergy] Cloud Load fehlgeschlagen: " + e.Message);
        }
    }

    // Reads the current balance at the time the lock is acquired, so that even if
    // multiple saves are queued, the last one always writes the most recent value.
    static async Task SaveToCloudAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            int balance = Balance;
            await CloudSaveService.Instance.Data.Player.SaveAsync(
                new Dictionary<string, object> { { CloudKey, balance } });
            Debug.Log($"[DreamEnergy] Cloud gespeichert: {balance}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DreamEnergy] Cloud Save fehlgeschlagen: " + e.Message);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
