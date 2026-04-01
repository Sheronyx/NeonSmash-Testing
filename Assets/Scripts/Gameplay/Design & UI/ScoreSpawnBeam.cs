using System.Collections;
using UnityEngine;

public class ScoreSpawnBeam : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform scoreTarget;
    [SerializeField] private GameObject projectilePrefab;

    [Header("Beam Settings")]
    [SerializeField] private float projectileSpeed = 25f;

    public void SpawnBeamToScore(Vector3 startWorldPos)
    {
        StartCoroutine(Co_SpawnBeam(startWorldPos));
    }

    private IEnumerator Co_SpawnBeam(Vector3 start, Vector3? overrideTarget = null)
    {

        yield return null;
        Canvas.ForceUpdateCanvases();

        if (scoreTarget == null || projectilePrefab == null)
            yield break;

        Debug.Log("Score Target Screen Pos: " + scoreTarget.position);
        start.z = 0f;

        Vector3 target = GetWorldPositionOfUI(scoreTarget);
        target.z = 0f;

        GameObject projectile = Instantiate(projectilePrefab, start, Quaternion.identity);

        while (projectile != null && Vector3.Distance(projectile.transform.position, target) > 0.05f)
        {
            projectile.transform.position =
                Vector3.MoveTowards(projectile.transform.position, target, projectileSpeed * Time.deltaTime);

            yield return null;
        }

        if (projectile != null)
            Destroy(projectile);

        // 👉 Score Feedback
        ScoreManager.Instance?.PunchScore();
    }

    private Vector3 GetWorldPositionOfUI(Transform uiElement)
    {
        RectTransform rect = uiElement as RectTransform;

        // 👉 1. UI → Screen Space
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, rect.position);

        // 👉 2. Screen → World Space (Game)
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(screenPos.x, screenPos.y, Mathf.Abs(Camera.main.transform.position.z))
        );

        worldPos.z = 0f;
        return worldPos;
    }
}