using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 baseOffset = new Vector3(0, 4, 12);
    [SerializeField] private float smoothTime = 0.3f;
    [SerializeField] private float heightLookOffset = 1.5f;
    [SerializeField] private bool followRotation = true;
    [SerializeField] private float rotationSmoothTime = 0.2f;

    private Vector3 positionVelocity = Vector3.zero;
    private float rotationVelocity;
    private float targetRotationY;
    private Rigidbody targetRb;

    private void Start()
    {
        if (target == null)
        {
            GameObject drone = GameObject.Find("Drone");
            if (drone != null)
            {
                target = drone.transform;
            }
        }

        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
            targetRotationY = target.eulerAngles.y;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
            targetRotationY = target.eulerAngles.y;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 currentOffset = baseOffset;

        if (followRotation && target != null)
        {
            targetRotationY = Mathf.SmoothDampAngle(targetRotationY, target.eulerAngles.y, ref rotationVelocity, rotationSmoothTime);
            Quaternion offsetRotation = Quaternion.Euler(0, targetRotationY, 0);
            currentOffset = offsetRotation * baseOffset;
        }

        Vector3 targetPosition = target.position + currentOffset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref positionVelocity, smoothTime);

        Vector3 lookTarget = target.position + Vector3.up * heightLookOffset;
        transform.LookAt(lookTarget);
    }
}
