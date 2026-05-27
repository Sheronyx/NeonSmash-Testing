using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendRowView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] Image           statusDot;
    [SerializeField] Button          challengeButton;
    [SerializeField] Button          removeButton;

    [SerializeField] Color onlineColor  = new Color(0.2f, 1f, 0.4f);
    [SerializeField] Color offlineColor = new Color(0.5f, 0.5f, 0.5f);

    public void Bind(string memberId, string displayName, bool isOnline,
                     Action<string> onChallenge, Action<string> onRemove)
    {
        nameText.text = displayName;

        if (statusDot) statusDot.color = isOnline ? onlineColor : offlineColor;

        if (challengeButton)
        {
            challengeButton.gameObject.SetActive(true);
            challengeButton.onClick.RemoveAllListeners();
            challengeButton.onClick.AddListener(() => onChallenge(memberId));
        }

        if (removeButton)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(() => onRemove(memberId));
        }
    }
}
