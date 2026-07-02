using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

/// <summary>
/// Zentraler IAP-Handler für NeonSmash.
/// Initialisiert beim Start alle ShopItems mit iapProductId.
/// Currency-Items = Consumable (Coins), alle anderen = NonConsumable (Unlock).
/// </summary>
public class IAPManager : MonoBehaviour, IDetailedStoreListener
{
    public static IAPManager Instance { get; private set; }

    public static event Action         OnStoreInitialized;
    public static event Action<string> OnPurchaseSuccess; // productId
    public static event Action<string> OnPurchaseError;   // productId

    [SerializeField] private ShopCatalogue catalogue;

    private IStoreController   _store;
    private IExtensionProvider _extensions;

    public bool IsInitialized => _store != null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Initialize();
    }

    private void Initialize()
    {
        if (catalogue == null) { Debug.LogWarning("[IAP] Kein Catalogue zugewiesen."); return; }

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        foreach (var item in catalogue.allItems)
        {
            if (item == null || string.IsNullOrEmpty(item.iapProductId)) continue;
            var productType = item.type == ShopItemType.Currency
                ? ProductType.Consumable
                : ProductType.NonConsumable;
            builder.AddProduct(item.iapProductId, productType);
        }

        UnityPurchasing.Initialize(this, builder);
    }

    // ── Öffentliche API ──────────────────────────────────────────────────────

    public void BuyProduct(string productId)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[IAP] Store noch nicht initialisiert.");
            return;
        }
        _store.InitiatePurchase(productId);
    }

    /// <summary>Nur auf iOS nötig — stellt NonConsumable-Käufe wieder her.</summary>
    public void RestorePurchases()
    {
#if UNITY_IOS
        if (!IsInitialized) return;
        var apple = _extensions.GetExtension<IAppleExtensions>();
        apple.RestoreTransactions((result, error) =>
        {
            Debug.Log(result
                ? "[IAP] Restore erfolgreich."
                : $"[IAP] Restore fehlgeschlagen: {error}");
        });
#else
        Debug.Log("[IAP] Restore nur auf iOS verfügbar.");
#endif
    }

    // ── IDetailedStoreListener ───────────────────────────────────────────────

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        _store      = controller;
        _extensions = extensions;
        Debug.Log("[IAP] Store erfolgreich initialisiert.");
        OnStoreInitialized?.Invoke();
    }

    public void OnInitializeFailed(InitializationFailureReason error) =>
        Debug.LogWarning($"[IAP] Initialisierung fehlgeschlagen: {error}");

    public void OnInitializeFailed(InitializationFailureReason error, string message) =>
        Debug.LogWarning($"[IAP] Initialisierung fehlgeschlagen: {error} — {message}");

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string productId = args.purchasedProduct.definition.id;
        Debug.Log($"[IAP] Kauf erfolgreich: {productId}");

        var item = FindItem(productId);
        if (item != null)
        {
            if (item.type == ShopItemType.Currency && item.coinReward > 0)
                CoinManager.AddCoins(item.coinReward);
            else
                ShopInventory.ClaimFree(item);
        }
        else
        {
            Debug.LogWarning($"[IAP] Kein ShopItem für productId '{productId}' gefunden.");
        }

        OnPurchaseSuccess?.Invoke(productId);
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
    {
        Debug.LogWarning($"[IAP] Kauf fehlgeschlagen: {product.definition.id} — {reason}");
        OnPurchaseError?.Invoke(product.definition.id);
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription description)
    {
        Debug.LogWarning($"[IAP] Kauf fehlgeschlagen: {product.definition.id} — {description.message}");
        OnPurchaseError?.Invoke(product.definition.id);
    }

    /// <summary>
    /// Gibt den lokalisierten Preis zurück (z.B. "0,99 €").
    /// Solange der Store noch nicht initialisiert ist, wird "..." zurückgegeben.
    /// </summary>
    public string GetLocalizedPrice(string productId)
    {
        if (!IsInitialized) return "...";
        var product = _store.products.WithID(productId);
        return product?.metadata.localizedPriceString ?? "...";
    }

    // ────────────────────────────────────────────────────────────────────────

    private ShopItem FindItem(string productId)
    {
        if (catalogue == null) return null;
        return Array.Find(catalogue.allItems,
            i => i != null && i.iapProductId == productId);
    }
}
