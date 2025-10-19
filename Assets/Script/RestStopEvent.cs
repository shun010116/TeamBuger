using UnityEngine;
using UnityEngine.UI; // Image를 사용하기 위해 추가
using System.Collections;

/// <summary>
/// [수정됨] 페이드 인/아웃 로직을 직접 포함합니다.
/// </summary>
public class RestStopEvent : MonoBehaviour, IDestination
{
    [Header("이벤트 설정")]
    public bool allowRoutePassThrough = false;
    public float staminaToRestore = 10f;
    public bool isVisited = false;

    [Header("UI 참조")]
    [Tooltip("화면 페이드 효과에 사용할 Fade 스크립트 (Fade.cs)")]
    public Fade fadeController;

    // IDestination 구현
    public bool AllowRoutePassThrough => allowRoutePassThrough;
    public RectInt GetArea() => area;

    private RectInt area;
    private bool isEventRunning = false;
    private Image fadeImage;

    private void Awake()
    {
        if (fadeController != null)
        {
            fadeImage = fadeController.targetImage;
        }
    }

    // RestStopSpawner가 호출
    public void Initialize(RectInt area)
    {
        this.area = area;
        // 시작 시 페이드 이미지 알파값 확인 (필수는 아님)
        if (fadeImage != null && fadeImage.color.a != 0)
        {
             Color color = fadeImage.color;
             color.a = 0;
             fadeImage.color = color;
        }
    }

    /// <summary>
    /// 플레이어가 트리거에 '머무는 동안' + '이동이 멈췄을 때' 이벤트 시작
    /// (OnTriggerEnterだと移動完了前に発動してしまう可能性があるため)
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        // 플레이어 태그 확인, 이벤트 중복 방지, 플레이어 이동 중 아님 확인
        if (other.CompareTag("Player") && !isEventRunning &&
            RouteManager.Instance != null && !RouteManager.Instance.IsPlayerMoving())
        {
            StartCoroutine(EventSequence(other.transform));
        }
    }

    /// <summary>
    /// PlayerMove가 이동 완료 후 수동으로 호출할 함수
    /// </summary>
    public void StartEventSequenceManual(Transform playerTransform)
    {
        if (!isEventRunning)
        {
            StartCoroutine(EventSequence(playerTransform));
        }
    }

    /// <summary>
    /// 휴게소 이벤트 로직 (페이드 포함)
    /// </summary>
    private IEnumerator EventSequence(Transform playerTransform)
    {
        if (fadeController == null)
        {
            Debug.LogError("FadeController가 RestStopEvent에 할당되지 않았습니다!");
            yield break; // 페이드 없이는 진행 불가
        }
        isEventRunning = true;

        // 1. 페이드 아웃
        yield return StartCoroutine(fadeController.FadeUI(1f)); // 알파 1 (불투명)

        // --- 페이드 아웃 완료 후 실행될 내용 ---
        // 2. 그려놨던 길 지우기
        if (RouteManager.Instance != null)
        {
            RouteManager.Instance.Internal_ResetAllRoutes();
        }

        // 3. 스테미나 회복
        if (StaminaManager.Instance != null)
        {
            StaminaManager.Instance.RestoreStamina(staminaToRestore);
        }

        // 4. 플레이어 위치 스냅 (휴게소 입구/중앙)
        Vector3 snapPosition = transform.position; // 휴게소 중앙
        playerTransform.position = snapPosition;
        // --- 여기까지 ---

        // 5. 페이드 인
        yield return StartCoroutine(fadeController.FadeUI(0f));

        isEventRunning = false;
    }
}