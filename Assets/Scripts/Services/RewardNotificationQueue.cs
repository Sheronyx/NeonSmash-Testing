using System.Collections.Generic;

public struct RewardNotification
{
    public string Title;
    public string Subtitle;
    public int    Coins;
}

public static class RewardNotificationQueue
{
    static readonly Queue<RewardNotification> _queue = new();
    static int _pendingCoins;

    public static int Count        => _queue.Count;
    // Total coins waiting to be animated — used by CoinDisplayUI to start from the pre-reward balance.
    public static int PendingCoins => _pendingCoins;

    public static void Enqueue(string title, string subtitle, int coins)
    {
        _pendingCoins += coins;
        _queue.Enqueue(new RewardNotification { Title = title, Subtitle = subtitle, Coins = coins });
    }

    public static bool TryDequeue(out RewardNotification notification)
    {
        bool result = _queue.TryDequeue(out notification);
        if (result) _pendingCoins -= notification.Coins;
        return result;
    }
}
