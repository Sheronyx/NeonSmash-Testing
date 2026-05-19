using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MultiplayerHUD : MonoBehaviour
{
    [Header("Scores")]
    [SerializeField] TextMeshProUGUI localScoreText;
    [SerializeField] TextMeshProUGUI opponentScoreText;

    [Header("Tug-of-War Bar")]
    [SerializeField] RectTransform tugFill;
    [SerializeField] float barHalfWidth = 200f;
    [SerializeField] int winThreshold = 30;

    [Header("Game Over")]
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] TextMeshProUGUI winnerNameText;

    [Header("Buttons")]
    [SerializeField] List<Button> backToMenuButtons = new();
    [SerializeField] List<Button> tryAgainButtons   = new();

    void Start()
    {
        if (!MultiplayerManager.IsMultiplayerGame)
        {
            gameObject.SetActive(false);
            return;
        }

        MultiplayerGameSession.OnScoresUpdated += HandleScores;
        MultiplayerGameSession.OnGameOver      += HandleGameOver;

        foreach (var btn in backToMenuButtons) btn.onClick.AddListener(GoToMenu);
        foreach (var btn in tryAgainButtons)   btn.onClick.AddListener(TryAgain);

        if (gameOverPanel) gameOverPanel.SetActive(false);

        UpdateUI(0, 0);
    }

    void OnDestroy()
    {
        MultiplayerGameSession.OnScoresUpdated -= HandleScores;
        MultiplayerGameSession.OnGameOver      -= HandleGameOver;
    }

    void GoToMenu()
    {
        MultiplayerManager.IsMultiplayerGame = false;
        MultiplayerManager.Instance?.Disconnect();
        SceneFader.Instance.LoadScene("MainMenuScene");
    }

    void TryAgain()
    {
        MultiplayerManager.IsMultiplayerGame = false;
        MultiplayerManager.Instance?.Disconnect();
        SceneFader.Instance.LoadScene("MainMenuScene");
    }

    void HandleScores(int local, int opponent)
    {
        UpdateUI(local, opponent);
    }

    void HandleGameOver(bool won, string winnerName)
    {
        if (winnerNameText) winnerNameText.text = $"{winnerName} wins";
        if (gameOverPanel)  gameOverPanel.SetActive(true);
    }

    void UpdateUI(int local, int opponent)
    {
        if (localScoreText)    localScoreText.text    = local.ToString();
        if (opponentScoreText) opponentScoreText.text = opponent.ToString();

        if (tugFill == null) return;

        float diff    = Mathf.Clamp(local - opponent, -winThreshold, winThreshold);
        float t       = diff / (float)winThreshold;
        float centerX = t * barHalfWidth;

        var pos = tugFill.anchoredPosition;
        pos.x = centerX;
        tugFill.anchoredPosition = pos;
    }
}
