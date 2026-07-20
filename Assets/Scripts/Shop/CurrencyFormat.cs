using System.Globalization;

// Zentrale Formatierung für Währungsbeträge — ab 1000 als "80K" statt "80,000", damit
// Preis- und Bilanz-Anzeigen im Shop konsistent bleiben.
public static class CurrencyFormat
{
    public static string Format(int amount)
    {
        if (amount >= 1000)
            return (amount / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        return amount.ToString("N0");
    }
}
