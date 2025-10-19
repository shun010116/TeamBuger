using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 메인 카메라에 부착되어 카메라의 이동, 줌, 재정렬(+추적)을 관리합니다.
/// [수정됨] Spacebar 홀드 시 플레이어 추적 기능 추가.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("카메라 설정")]
    [Tooltip("카메라 이동 속도 (줌 레벨에 비례함. 0.5 ~ 1.5 권장)")]
    public float speedMultiplier = 1.0f;
    [Tooltip("마우스 휠 줌 속도")]
    public float zoomSpeed = 1.0f;
    [Tooltip("최소 줌 (가장 멀리)")]
    public float maxZoomOrthographicSize = 15.0f;
    [Tooltip("최대 줌 (가장 가까이)")]
    public float minZoomOrthographicSize = 3.0f;

    // 내부 변수
    private PlayerControls playerControls;
    private Camera mainCamera;
    private Transform playerTarget; // 추적 및 재정렬 대상
    private bool isFollowingPlayer = false; // [추가] 추적 모드 상태 플래그

    void Awake()
    {
        if (Instance == null) { Instance = this; } else { Destroy(gameObject); return; }
        mainCamera = GetComponent<Camera>();
        playerControls = new PlayerControls();
    }

    private void OnEnable()
    {
        playerControls.Player.Enable();
        // [수정] Recenter 액션의 'started'(눌렀을 때)와 'canceled'(뗐을 때) 구독
        playerControls.Player.Recenter.started += StartFollowing;
        playerControls.Player.Recenter.canceled += StopFollowing;
        // playerControls.Player.Recenter.performed -= OnRecenter; // performed는 이제 사용 안 함
    }

    private void OnDisable()
    {
        playerControls.Player.Disable();
        // [수정] 구독 해제
        playerControls.Player.Recenter.started -= StartFollowing;
        playerControls.Player.Recenter.canceled -= StopFollowing;
    }

    /// <summary>
    /// [수정됨] Update -> LateUpdate로 변경 (카메라 추적은 LateUpdate가 더 부드러움)
    /// </summary>
    void LateUpdate() // Update 대신 LateUpdate 사용
    {
        // 1. 추적 모드일 경우: 플레이어 위치로 카메라 이동
        if (isFollowingPlayer && playerTarget != null)
        {
            // CenterOnTarget 함수를 재사용하여 위치 업데이트
            CenterOnTarget(playerTarget);
        }
        // 2. 추적 모드가 아닐 경우: WASD로 자유 이동
        else if (!isFollowingPlayer)
        {
            HandleCameraMovement(); // WASD 이동 처리
        }

        // 3. 줌 처리는 항상 가능
        HandleZoom();
    }

    /// <summary>
    /// (PlayerSpawner가 호출) 지정된 Transform을 기준으로 카메라를 맞추고, 추적 대상으로 저장합니다.
    /// </summary>
    public void CenterOnTarget(Transform target)
    {
        if (target == null) return; // 대상이 없으면 아무것도 안 함

        // 추적/재정렬 대상을 저장하거나 업데이트
        this.playerTarget = target;

        // Z축 값은 그대로 두면서 XY 위치만 맞춤 (기존 로직 동일)
        Vector3 targetPosition = target.position;
        transform.position = new Vector3(
            targetPosition.x,
            targetPosition.y,
            transform.position.z
        );
    }

    /// <summary>
    /// [수정됨] Spacebar를 '누르기 시작'했을 때 호출됩니다.
    /// </summary>
    private void StartFollowing(InputAction.CallbackContext context)
    {
        if (playerTarget != null)
        {
            isFollowingPlayer = true; // 추적 모드 활성화
            CenterOnTarget(playerTarget); // 즉시 한 번 중앙 정렬 (탭 효과)
        }
        else
        {
            Debug.LogWarning("추적할 플레이어 타겟이 없습니다!");
        }
    }

    /// <summary>
    /// [수정됨] Spacebar를 '뗐을 때' 호출됩니다.
    /// </summary>
    private void StopFollowing(InputAction.CallbackContext context)
    {
        isFollowingPlayer = false; // 추적 모드 비활성화
        // 카메라는 현재 위치에 그대로 멈춤
    }

    /// <summary>
    /// WASD 입력을 받아 카메라를 이동 (추적 중이 아닐 때만 호출됨)
    /// </summary>
    private void HandleCameraMovement()
    {
        Vector2 moveInput = playerControls.Player.Move.ReadValue<Vector2>();
        Vector3 moveDirection = new Vector3(moveInput.x, moveInput.y, 0);

        float adjustedSpeed = mainCamera.orthographicSize * speedMultiplier;

        transform.Translate(moveDirection.normalized * adjustedSpeed * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// 마우스 휠 줌 처리 (변경 없음)
    /// </summary>
    private void HandleZoom()
    {
        float zoomInput = playerControls.Player.Zoom.ReadValue<Vector2>().y;
        if (zoomInput == 0) return;
        float newSize = mainCamera.orthographicSize - (Mathf.Sign(zoomInput) * zoomSpeed);
        mainCamera.orthographicSize = Mathf.Clamp(newSize, minZoomOrthographicSize, maxZoomOrthographicSize);
    }

    // [삭제됨] OnRecenter 함수 -> StartFollowing으로 통합됨
}