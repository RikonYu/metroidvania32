using UnityEngine;

public class CamParent : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 deadZoneSize = new Vector2(4f, 2f);
    [SerializeField] private Vector2 followOffset = new Vector2(0f, 1f);
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private bool drawGizmos = true;

    private Room currentRoom;
    private Vector3 smoothVelocity;

    private Camera TargetCamera
    {
        get
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponentInChildren<Camera>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            return targetCamera;
        }
    }

    private void LateUpdate()
    {
        if (target == null || currentRoom == null)
        {
            return;
        }

        Vector3 desired = GetDeadZoneAdjustedPosition();
        desired = ClampToCurrentRoom(desired);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref smoothVelocity, smoothTime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetCurrentRoom(Room room)
    {
        currentRoom = room;
    }

    public void HardCutToTarget()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desired = target.position + (Vector3)followOffset;
        transform.position = ClampToCurrentRoom(desired);
        smoothVelocity = Vector3.zero;
    }

    public void HardCutToPosition(Vector3 position)
    {
        transform.position = ClampToCurrentRoom(position);
        smoothVelocity = Vector3.zero;
    }

    private Vector3 GetDeadZoneAdjustedPosition()
    {
        Vector3 current = transform.position;
        Vector3 desiredTarget = target.position + (Vector3)followOffset;
        Vector2 halfDeadZone = deadZoneSize * 0.5f;

        Vector3 desired = current;
        desired.z = current.z;

        float deltaX = desiredTarget.x - current.x;
        if (Mathf.Abs(deltaX) > halfDeadZone.x)
        {
            desired.x = desiredTarget.x - Mathf.Sign(deltaX) * halfDeadZone.x;
        }

        float deltaY = desiredTarget.y - current.y;
        if (Mathf.Abs(deltaY) > halfDeadZone.y)
        {
            desired.y = desiredTarget.y - Mathf.Sign(deltaY) * halfDeadZone.y;
        }

        return desired;
    }

    private Vector3 ClampToCurrentRoom(Vector3 position)
    {
        if (currentRoom == null || TargetCamera == null)
        {
            return position;
        }

        Rect rect = currentRoom.WorldRect;
        float halfHeight = TargetCamera.orthographicSize;
        float halfWidth = halfHeight * TargetCamera.aspect;

        position.x = Mathf.Clamp(position.x, rect.xMin + halfWidth, rect.xMax - halfWidth);
        position.y = Mathf.Clamp(position.y, rect.yMin + halfHeight, rect.yMax - halfHeight);
        return position;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(deadZoneSize.x, deadZoneSize.y, 0f));

        if (currentRoom != null && TargetCamera != null)
        {
            Rect rect = currentRoom.WorldRect;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(rect.center, new Vector3(rect.width, rect.height, 0f));
        }
    }
}
