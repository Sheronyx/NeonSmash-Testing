using System.Collections.Generic;

public struct RewardNotification
{
    public string Title;
    public string Subtitle;
    public int    Amount;
}

public static class RewardNotificationQueue
{
    static readonly Queue<RewardNotification> _queue = new();
    static int _pendingAmount;

    public static int Count         => _queue.Count;
    // Total Dream Energy waiting to be animated — used by DreamEnergyDisplayUI to start from the pre-reward balance.
    public static int PendingAmount => _pendingAmount;

    public static void Enqueue(string title, string subtitle, int amount)
    {
        _pendingAmount += amount;
        _queue.Enqueue(new RewardNotification { Title = title, Subtitle = subtitle, Amount = amount });
    }

    public static bool TryDequeue(out RewardNotification notification)
    {
        bool result = _queue.TryDequeue(out notification);
        if (result) _pendingAmount -= notification.Amount;
        return result;
    }
}
