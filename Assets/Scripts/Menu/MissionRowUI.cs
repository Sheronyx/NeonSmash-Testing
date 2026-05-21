using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Image           progressBarFill;
    [SerializeField] private GameObject      completedBadge;
    [SerializeField] private GameObject      progressGroup;

    public void Bind(MissionData mission)
    {
        if (titleText != null) titleText.text = mission.Title;

        bool done = mission.IsCompleted;
        if (completedBadge != null) completedBadge.SetActive(done);
        if (progressGroup  != null) progressGroup.SetActive(!done);

        if (done) return;

        float fill = mission.Target > 0 ? Mathf.Clamp01((float)mission.Progress / mission.Target) : 0f;
        if (progressBarFill != null) progressBarFill.fillAmount = fill;

        if (progressText != null)
        {
            progressText.text = mission.Type switch
            {
                MissionType.PlayGames          => $"{mission.Progress} / {mission.Target}",
                MissionType.ScoreInOneGame     => $"{mission.Progress} / {mission.Target} pts",
                MissionType.TriggerSpecialMode => mission.Progress >= 1 ? "Done" : "0 / 1",
                _                              => $"{mission.Progress} / {mission.Target}"
            };
        }
    }
}
