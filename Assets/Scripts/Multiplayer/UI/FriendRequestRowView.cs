using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendRequestRowView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] Button          acceptButton;
    [SerializeField] Button          declineButton;

    public void Bind(string memberId, string displayName,
                     Action<string> onAccept, Action<string> onDecline)
    {
        nameText.text = displayName;

        if (acceptButton)
        {
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(() => onAccept(memberId));
        }

        if (declineButton)
        {
            declineButton.onClick.RemoveAllListeners();
            declineButton.onClick.AddListener(() => onDecline(memberId));
        }
    }
}
