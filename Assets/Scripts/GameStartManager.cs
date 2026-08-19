using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>시작 연출 후의 레벨 진행, HUD 및 게임 오버를 관리합니다.</summary>
public class GameStartManager : MonoBehaviour
{
    [Header("Start References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject prefabToSpawn;
    [SerializeField] private MouseDeltaObjectMover mouseDeltaObjectMover;

    [Header("Start Timing")]
    [SerializeField, Min(0.01f)] private float fovChangeDuration = 1f;
    [SerializeField] private float targetFieldOfView = 41.3f;
    [SerializeField] private Ease fovEase = Ease.InOutSine;
    [SerializeField, Min(0f)] private float readyDelayAfterFov = 2f;
    [SerializeField, Min(0f)] private float spawnDelayAfterReady = 3f;

    [Header("Level Progression")]
    [SerializeField, Min(0.1f)] private float levelDuration = 10f;
    [Tooltip("레벨 3에서 Bool을 변경할 Animator")]
    [SerializeField] private Animator level3Animator;
    [Tooltip("Animator에 추가할 Bool 파라미터 이름")]
    [SerializeField] private string level3AnimatorBool = "IsLevel3";
    [Tooltip("레벨 3 블렌드 중 화면 중앙에 유지할 손 본 아래 무기 Transform")]
    [SerializeField] private Transform level3WeaponTarget;
    [Tooltip("Animator 전환 시간과 같은 값으로 설정하세요.")]
    [SerializeField, Min(0.01f)] private float level3CameraCenterDuration = 0.5f;
    [SerializeField, Min(0f)] private float level4OrbitDegreesPerSecond = 20f;
    [SerializeField, Min(0f)] private float level5SpeedIncreasePercent = 10f;

    [Header("Ready UI")]
    [SerializeField] private string readyMessage = "READY";
    [SerializeField] private Color readyColor = Color.white;

    private Canvas canvas;
    private RectTransform canvasRect;
    private Text timerText;
    private Text levelUpText;
    private Text gameOverText;
    private RectTransform readyPanel;
    private RectTransform levelUpPanel;
    private CanvasGroup readyGroup;
    private CanvasGroup levelUpGroup;
    private CanvasGroup gameOverGroup;
    private Sequence readySequence;
    private Sequence levelUpSequence;
    private Coroutine levelRoutine;
    private Coroutine level3CameraCenterRoutine;
    private float gameStartTime;
    private float initialAnimatorSpeed = 1f;
    private float currentOrbitDegreesPerSecond;
    private bool isGameStarted;
    private bool isGameOver;
    private int currentLevel = 1;
    private GameOverOnPlaneCollision collisionObject;

    private void Awake()
    {
        CreateUI();
        currentOrbitDegreesPerSecond = level4OrbitDegreesPerSecond;
        if (level3Animator != null) initialAnimatorSpeed = level3Animator.speed;
        if (mouseDeltaObjectMover != null) mouseDeltaObjectMover.enabled = false;
        if (level3Animator != null && !string.IsNullOrWhiteSpace(level3AnimatorBool))
            level3Animator.SetBool(level3AnimatorBool, false);
    }

    private void Start() => StartCoroutine(PlayStartSequence());

    private void Update()
    {
        if (isGameStarted && !isGameOver)
        {
            timerText.text = "TIME  " + FormatTime(Time.time - gameStartTime);
            if (currentLevel >= 4) OrbitCamera();
        }

        if (isGameOver && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator PlayStartSequence()
    {
        if (targetCamera == null)
        {
            Debug.LogError("GameStartManager: Target Camera를 연결해 주세요.", this);
            yield break;
        }

        yield return DOTween.To(() => targetCamera.fieldOfView, value => targetCamera.fieldOfView = value,
                targetFieldOfView, fovChangeDuration)
            .SetEase(fovEase).SetLink(gameObject).WaitForCompletion();
        yield return new WaitForSeconds(readyDelayAfterFov);

        ShowReady();
        yield return new WaitForSeconds(spawnDelayAfterReady);
        HideReady();
        SpawnPrefab();
        BeginGameplay();
    }

    private void BeginGameplay()
    {
        if (isGameOver) return;

        isGameStarted = true;
        gameStartTime = Time.time;
        timerText.gameObject.SetActive(true);
        if (mouseDeltaObjectMover != null) mouseDeltaObjectMover.enabled = true;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        collisionObject = FindFirstObjectByType<GameOverOnPlaneCollision>();
        if (collisionObject != null) collisionObject.SetJumpingEnabled(false);
        levelRoutine = StartCoroutine(PlayLevels());
    }

    private IEnumerator PlayLevels()
    {
        while (!isGameOver)
        {
            yield return new WaitForSeconds(levelDuration);
            if (isGameOver) yield break;
            currentLevel++;
            ApplyLevel(currentLevel);
            yield return ShowLevelUp(currentLevel);
        }
    }

    private void ApplyLevel(int level)
    {
        if (level >= 2 && collisionObject == null)
        {
            collisionObject = FindFirstObjectByType<GameOverOnPlaneCollision>();
        }

        if (collisionObject != null) collisionObject.SetJumpingEnabled(level == 2);

        if (level >= 3)
        {
            if (level3Animator != null && !string.IsNullOrWhiteSpace(level3AnimatorBool))
                level3Animator.SetBool(level3AnimatorBool, true);

            if (level == 3)
            {
                level3CameraCenterRoutine = StartCoroutine(CenterWeaponDuringLevel3Blend());
            }
        }

        if (level >= 5)
        {
            float speedMultiplier = Mathf.Pow(1f + level5SpeedIncreasePercent / 100f, level - 4);
            if (level3Animator != null) level3Animator.speed = initialAnimatorSpeed * speedMultiplier;
            currentOrbitDegreesPerSecond = level4OrbitDegreesPerSecond * speedMultiplier;
        }
    }

    private IEnumerator ShowLevelUp(int level)
    {
        levelUpSequence?.Kill();
        levelUpText.text = "LEVEL " + level;
        levelUpPanel.gameObject.SetActive(true);
        levelUpGroup.alpha = 1f;

        float canvasWidth = canvasRect.rect.width;
        float outsideX = canvasWidth * 0.5f + levelUpPanel.rect.width;
        levelUpPanel.anchoredPosition = new Vector2(-outsideX, 0f);
        levelUpSequence = DOTween.Sequence().SetLink(gameObject);
        levelUpSequence.Append(levelUpPanel.DOAnchorPosX(canvasWidth * -0.1f, 0.35f).SetEase(Ease.OutCubic));
        levelUpSequence.Append(levelUpPanel.DOAnchorPosX(canvasWidth * 0.1f, 1.25f).SetEase(Ease.InOutSine));
        levelUpSequence.Append(levelUpPanel.DOAnchorPosX(outsideX, 0.45f).SetEase(Ease.InCubic));
        levelUpSequence.Join(levelUpGroup.DOFade(0f, 0.35f).SetDelay(1.7f));
        yield return levelUpSequence.WaitForCompletion();
        levelUpPanel.gameObject.SetActive(false);
    }

    private void OrbitCamera()
    {
        if (targetCamera != null && spawnPoint != null)
            targetCamera.transform.RotateAround(spawnPoint.position, Vector3.up, currentOrbitDegreesPerSecond * Time.deltaTime);
    }

    private IEnumerator CenterWeaponDuringLevel3Blend()
    {
        if (targetCamera == null || level3WeaponTarget == null) yield break;

        float elapsed = 0f;
        while (elapsed < level3CameraCenterDuration && !isGameOver)
        {
            yield return new WaitForEndOfFrame();
            elapsed += Time.deltaTime;
            MoveCameraTowardWeaponCenter(1f - Mathf.Exp(-14f * Time.deltaTime));
        }

        if (!isGameOver) MoveCameraTowardWeaponCenter(1f);
    }

    private void MoveCameraTowardWeaponCenter(float movementFactor)
    {
        Vector3 weaponScreenPosition = targetCamera.WorldToScreenPoint(level3WeaponTarget.position);
        if (weaponScreenPosition.z <= 0f) return;

        Rect pixelRect = targetCamera.pixelRect;
        Vector3 screenCenterAtWeaponDepth = targetCamera.ScreenToWorldPoint(new Vector3(
            pixelRect.center.x, pixelRect.center.y, weaponScreenPosition.z));
        Vector3 requiredCameraPosition = targetCamera.transform.position +
                                         (level3WeaponTarget.position - screenCenterAtWeaponDepth);
        targetCamera.transform.position = Vector3.Lerp(
            targetCamera.transform.position, requiredCameraPosition, movementFactor);
    }

    public void EndGame()
    {
        if (isGameOver) return;
        isGameOver = true;

        float finalTime = isGameStarted ? Time.time - gameStartTime : 0f;
        timerText.text = "TIME  " + FormatTime(finalTime);
        readySequence?.Kill();
        levelUpSequence?.Kill();
        if (levelRoutine != null) StopCoroutine(levelRoutine);
        if (level3CameraCenterRoutine != null) StopCoroutine(level3CameraCenterRoutine);
        if (mouseDeltaObjectMover != null) mouseDeltaObjectMover.enabled = false;
        if (collisionObject != null) collisionObject.SetJumpingEnabled(false);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        gameOverText.text = "GAME OVER\n\nTIME  " + FormatTime(finalTime) + "\n\n<size=28>PRESS R TO RESTART</size>";
        gameOverGroup.gameObject.SetActive(true);
        gameOverGroup.alpha = 0f;
        gameOverGroup.DOFade(1f, 0.6f).SetEase(Ease.OutQuad).SetLink(gameObject);
    }

    private void SpawnPrefab()
    {
        if (prefabToSpawn == null || spawnPoint == null) return;
        GameObject spawned = Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
        collisionObject = spawned.GetComponentInChildren<GameOverOnPlaneCollision>();
    }

    private void CreateUI()
    {
        GameObject canvasObject = new GameObject("GameUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasRect = canvas.GetComponent<RectTransform>();
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        timerText = CreateText("TimerText", canvas.transform, 42, Color.white, TextAnchor.UpperLeft);
        RectTransform timerRect = timerText.rectTransform;
        timerRect.anchorMin = timerRect.anchorMax = new Vector2(0, 1);
        timerRect.pivot = new Vector2(0, 1);
        timerRect.anchoredPosition = new Vector2(45, -40);
        timerRect.sizeDelta = new Vector2(500, 70);
        timerText.gameObject.SetActive(false);

        GameObject ready = CreatePanel("ReadyPanel", canvas.transform, new Vector2(680, 190), new Color(0.03f, 0.05f, 0.12f, 0.88f));
        readyPanel = ready.GetComponent<RectTransform>();
        readyGroup = ready.AddComponent<CanvasGroup>();
        Text readyText = CreateText("ReadyText", ready.transform, 72, readyColor, TextAnchor.MiddleCenter);
        readyText.text = readyMessage;
        AddTextEffects(readyText, new Color(0.1f, 0.85f, 1f, 0.9f));
        Stretch(readyText.rectTransform);
        ready.SetActive(false);

        GameObject levelUp = CreatePanel("LevelUpPanel", canvas.transform, new Vector2(620, 160), new Color(0.12f, 0.05f, 0.2f, 0.9f));
        levelUpPanel = levelUp.GetComponent<RectTransform>();
        levelUpGroup = levelUp.AddComponent<CanvasGroup>();
        levelUpText = CreateText("LevelUpText", levelUp.transform, 70, new Color(1f, 0.84f, 0.2f), TextAnchor.MiddleCenter);
        AddTextEffects(levelUpText, new Color(0.65f, 0.15f, 1f, 0.9f));
        Stretch(levelUpText.rectTransform);
        levelUp.SetActive(false);

        GameObject overlay = new GameObject("GameOverPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        overlay.transform.SetParent(canvas.transform, false);
        overlay.GetComponent<Image>().color = new Color(0, 0, 0, 0.72f);
        gameOverGroup = overlay.GetComponent<CanvasGroup>();
        Stretch(overlay.GetComponent<RectTransform>());
        gameOverText = CreateText("GameOverText", overlay.transform, 82, Color.white, TextAnchor.MiddleCenter);
        gameOverText.fontStyle = FontStyle.Bold;
        AddTextEffects(gameOverText, new Color(0.85f, 0.12f, 0.12f, 0.95f));
        Stretch(gameOverText.rectTransform);
        overlay.SetActive(false);
    }

    private void ShowReady()
    {
        readyPanel.gameObject.SetActive(true);
        readyGroup.alpha = 0;
        readyPanel.localScale = Vector3.one * 0.65f;
        readyPanel.anchoredPosition = new Vector2(0, -45);
        readySequence = DOTween.Sequence().SetLink(gameObject);
        readySequence.Append(readyGroup.DOFade(1, 0.27f));
        readySequence.Join(readyPanel.DOScale(1, 0.45f).SetEase(Ease.OutBack));
        readySequence.Join(readyPanel.DOAnchorPosY(0, 0.45f).SetEase(Ease.OutCubic));
    }

    private void HideReady()
    {
        readySequence?.Kill();
        readyPanel.gameObject.SetActive(false);
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static Text CreateText(string name, Transform parent, int size, Color color, TextAnchor alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        Text text = obj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        return text;
    }

    private static void AddTextEffects(Text text, Color outlineColor)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(3, -3);
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.65f);
        shadow.effectDistance = new Vector2(8, -8);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        int centiseconds = Mathf.FloorToInt((time * 100f) % 100f);
        return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, centiseconds);
    }

    private void OnDestroy()
    {
        readySequence?.Kill();
        levelUpSequence?.Kill();
        if (level3CameraCenterRoutine != null) StopCoroutine(level3CameraCenterRoutine);
    }
}
