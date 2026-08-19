using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마우스 이동량(delta)에 비례해 대상 오브젝트를 이동시킵니다.
/// </summary>
public class MouseDeltaObjectMover : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("비워 두면 이 스크립트가 붙은 오브젝트를 움직입니다.")]
    [SerializeField] private Transform targetObject;
    [Tooltip("화면 비율 제한에 사용할 카메라입니다.")]
    [SerializeField] private Camera movementCamera;

    [Header("Mouse Movement")]
    [Tooltip("화면 픽셀 단위 마우스 delta에 곱할 이동 배율입니다. 기본값 1은 마우스와 같은 비율입니다.")]
    [SerializeField] private Vector2 deltaMultiplier = Vector2.one;
    [SerializeField] private bool invertY = false;

    [Header("Screen Position Limits (Viewport: 0 = left/bottom, 1 = right/top)")]
    [SerializeField] private bool clampToViewport = true;
    [Range(0f, 1f)] [SerializeField] private float minViewportX = 0.1f;
    [Range(0f, 1f)] [SerializeField] private float maxViewportX = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float minViewportY = 0.1f;
    [Range(0f, 1f)] [SerializeField] private float maxViewportY = 0.9f;

    private void Awake()
    {
        if (targetObject == null)
        {
            targetObject = transform;
        }

        if (movementCamera == null)
        {
            movementCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (movementCamera == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float yDirection = invertY ? -1f : 1f;

        // 애니메이터 갱신 후의 위치를 화면 좌표로 바꾼 뒤, 마우스 delta를 화면 기준으로 적용합니다.
        Vector3 currentWorldPosition = targetObject.parent == null
            ? targetObject.position
            : targetObject.parent.TransformPoint(targetObject.localPosition);

        Vector3 screenPosition = movementCamera.WorldToScreenPoint(currentWorldPosition);
        screenPosition.x += mouseDelta.x * deltaMultiplier.x;
        screenPosition.y += mouseDelta.y * deltaMultiplier.y * yDirection;
        Vector3 targetWorldPosition = movementCamera.ScreenToWorldPoint(screenPosition);

        if (clampToViewport && movementCamera != null)
        {
            Vector3 viewportPosition = movementCamera.WorldToViewportPoint(targetWorldPosition);
            viewportPosition.x = Mathf.Clamp(viewportPosition.x, minViewportX, maxViewportX);
            viewportPosition.y = Mathf.Clamp(viewportPosition.y, minViewportY, maxViewportY);
            targetWorldPosition = movementCamera.ViewportToWorldPoint(viewportPosition);
        }

        if (targetObject.parent == null)
        {
            targetObject.position = targetWorldPosition;
        }
        else
        {
            targetObject.localPosition = targetObject.parent.InverseTransformPoint(targetWorldPosition);
        }
    }

    private void OnValidate()
    {
        if (minViewportX > maxViewportX)
        {
            maxViewportX = minViewportX;
        }

        if (minViewportY > maxViewportY)
        {
            maxViewportY = minViewportY;
        }
    }
}
