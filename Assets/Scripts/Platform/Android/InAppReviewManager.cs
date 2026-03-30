using UnityEngine;
using System.Collections;

#if UNITY_ANDROID
using Google.Play.Review;
#endif

public class InAppReviewManager : MonoBehaviour
{
    public static InAppReviewManager Instance;

#if UNITY_ANDROID
    private ReviewManager reviewManager;
    private PlayReviewInfo playReviewInfo;
#endif

    private const string PLAY_COUNT_KEY = "PlayCount";
    private const string REVIEW_SHOWN_KEY = "ReviewShown";

    // 👉 DEINE iOS APP ID HIER EINTRAGEN
    private const string IOS_APP_ID = "6754193997";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_ANDROID
        reviewManager = new ReviewManager();
#endif
    }

    // 👉 Wird nach jedem Spiel aufgerufen
    public void OnGameFinished()
    {
        if (PlayerPrefs.GetInt(REVIEW_SHOWN_KEY, 0) == 1)
            return;

        int playCount = PlayerPrefs.GetInt(PLAY_COUNT_KEY, 0);
        playCount++;
        PlayerPrefs.SetInt(PLAY_COUNT_KEY, playCount);

        Debug.Log("PlayCount: " + playCount);

        if (playCount >= 3)
        {
            TriggerReview();
        }
    }

    public void TriggerReview()
    {
#if UNITY_ANDROID
        StartCoroutine(RequestReviewFlow());
#elif UNITY_IOS
        OpenIOSReview();
#else
        Debug.Log("Review not supported on this platform");
#endif
    }

#if UNITY_ANDROID
    private IEnumerator RequestReviewFlow()
    {
        var requestFlowOperation = reviewManager.RequestReviewFlow();
        yield return requestFlowOperation;

        Debug.Log("Request Error: " + requestFlowOperation.Error);

        if (requestFlowOperation.Error != ReviewErrorCode.NoError)
        {
            yield break;
        }

        playReviewInfo = requestFlowOperation.GetResult();

        var launchFlowOperation = reviewManager.LaunchReviewFlow(playReviewInfo);
        yield return launchFlowOperation;

        Debug.Log("Launch Error: " + launchFlowOperation.Error);

        if (launchFlowOperation.Error != ReviewErrorCode.NoError)
        {
            yield break;
        }

        Debug.Log("Review flow completed.");

        PlayerPrefs.SetInt(REVIEW_SHOWN_KEY, 1);
    }
#endif

#if UNITY_IOS
    private void OpenIOSReview()
    {
        string url = $"itms-apps://itunes.apple.com/app/id{IOS_APP_ID}?action=write-review";
        Application.OpenURL(url);

        Debug.Log("Opened iOS Review Page");

        PlayerPrefs.SetInt(REVIEW_SHOWN_KEY, 1);
    }
#endif
}