using System.Collections;
using UnityEngine;

/// <summary>
/// 생성 시 GameStartManager와 이름이 일치하는 Plane을 자동 탐색합니다.
/// 레벨 2부터는 Rigidbody에 위쪽 Impulse를 주어 주기적으로 점프합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GameOverOnPlaneCollision : MonoBehaviour
{
    [Header("Auto Find")]
    [SerializeField] private string planeObjectName = "Plane";

    [Header("Level 2 Jump")]
    [SerializeField, Min(0.1f)] private float jumpInterval = 3f;
    [SerializeField, Min(0f)] private float jumpForce = 6f;
    [SerializeField] private ForceMode jumpForceMode = ForceMode.Impulse;

    private GameStartManager gameStartManager;
    private Transform planeObject;
    private Rigidbody rigidbodyComponent;
    private Coroutine jumpRoutine;

    private void Awake()
    {
        rigidbodyComponent = GetComponent<Rigidbody>();
        gameStartManager = FindFirstObjectByType<GameStartManager>();

        GameObject plane = GameObject.Find(planeObjectName);
        if (plane != null) planeObject = plane.transform;
        else Debug.LogWarning("GameOverOnPlaneCollision: '" + planeObjectName + "' 오브젝트를 찾지 못했습니다.", this);
    }

    public void SetJumpingEnabled(bool enabled)
    {
        if (enabled)
        {
            if (jumpRoutine == null) jumpRoutine = StartCoroutine(JumpRoutine());
            return;
        }

        if (jumpRoutine != null)
        {
            StopCoroutine(jumpRoutine);
            jumpRoutine = null;
        }
    }

    private IEnumerator JumpRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(jumpInterval);
            rigidbodyComponent.AddForce(Vector3.up * jumpForce, jumpForceMode);
        }
    }

    private void OnCollisionEnter(Collision collision) => TryEndGame(collision.transform);
    private void OnTriggerEnter(Collider other) => TryEndGame(other.transform);

    private void TryEndGame(Transform other)
    {
        if (gameStartManager == null || planeObject == null) return;
        if (other == planeObject || other.IsChildOf(planeObject)) gameStartManager.EndGame();
    }

    private void OnDestroy() => SetJumpingEnabled(false);
}
