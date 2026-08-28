using UnityEngine;

public class PlantingZone : MonoBehaviour
{
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private GameObject promptUI;

    private bool isPlanted = false;
    private bool playerInRange = false;
    private Collider triggerCollider;

    private void Start()
    {
        triggerCollider = GetComponent<Collider>();

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        if (treePrefab == null)
        {
            Debug.LogWarning($"PlantingZone '{gameObject.name}' has no Tree Prefab assigned!", gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;

            if (!isPlanted && promptUI != null)
            {
                promptUI.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }
        }
    }

    public void TryPlant()
    {
        if (isPlanted || !playerInRange)
        {
            return;
        }

        isPlanted = true;

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        if (treePrefab != null)
        {
            Instantiate(treePrefab, transform.position, Quaternion.identity);
        }

        GameManager.Instance.NotifyTreePlanted(this);
    }
}
