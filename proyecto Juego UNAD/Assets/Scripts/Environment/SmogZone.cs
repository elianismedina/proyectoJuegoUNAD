using UnityEngine;
using System.Collections;

public class SmogZone : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    private float currentHealth;
    private ParticleSystem particleSystem;
    private Collider triggerCollider;
    private Vector3 originalScale;
    private Material originalMaterial;
    private bool isCleared = false;

    private void Start()
    {
        currentHealth = maxHealth;
        particleSystem = GetComponentInChildren<ParticleSystem>();
        triggerCollider = GetComponent<Collider>();
        originalScale = transform.localScale;

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            originalMaterial = renderer.material;
        }
    }

    public void TakeDamage(float damage)
    {
        if (isCleared) return;

        currentHealth -= damage;
        UpdateVisual();

        if (currentHealth <= 0)
        {
            ClearZone();
        }
    }

    private void UpdateVisual()
    {
        float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);

        // Shrink the cloud visually
        transform.localScale = originalScale * Mathf.Lerp(0.3f, 1f, healthPercent);

        // Fade particle emission
        if (particleSystem != null)
        {
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = Mathf.Lerp(0, 30, healthPercent);
        }

        // Fade material alpha
        if (originalMaterial != null && originalMaterial.HasProperty("_Color"))
        {
            Color color = originalMaterial.color;
            color.a = Mathf.Lerp(0.2f, 0.8f, healthPercent);
            originalMaterial.color = color;
        }
    }

    private void ClearZone()
    {
        isCleared = true;

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        GameManager.Instance.NotifySmogCleared(this);

        StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);

            if (originalMaterial != null && originalMaterial.HasProperty("_Color"))
            {
                Color color = originalMaterial.color;
                color.a = alpha;
                originalMaterial.color = color;
            }

            if (particleSystem != null && particleSystem.isPlaying)
            {
                particleSystem.Stop();
            }

            yield return null;
        }

        gameObject.SetActive(false);
    }
}
