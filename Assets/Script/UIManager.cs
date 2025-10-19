using UnityEngine;
using UnityEngine.UI; // Button을 사용하기 위해 필요

/// <summary>
/// 'GO' 버튼의 활성화/비활성화 상태를 관리합니다.
/// [수정됨] OnClick() 이벤트는 인스펙터에서 직접 연결합니다.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI 요소")]
    [Tooltip("상태를 제어할 GO 버튼")]
    public Button goButton;

    void Awake()
    {
        if (Instance == null) 
        { 
            Instance = this; 
        } 
        else 
        { 
            Destroy(gameObject); 
        }
    }

    // [삭제됨] Start() 함수 -> OnClick 연결은 인스펙터에서 할 것임
    // [삭제됨] OnGoButtonPressed() 함수 -> OnClick이 RouteManager를 바로 호출할 것임

    void Update()
    {
        // UIManager의 유일한 역할: 매 프레임 'GO' 버튼의 활성화/비활성화 조건 검사
        UpdateGoButtonState();
    }

    /// <summary>
    /// (GO 버튼 활성화 로직)
    /// </summary>
    public void UpdateGoButtonState()
    {
        if (goButton == null || RouteManager.Instance == null || StaminaManager.Instance == null)
        {
            if (goButton) goButton.interactable = false;
            return;
        }

        // 조건 1: 루트가 목적지에 닿았는가?
        bool isFinished = RouteManager.Instance.IsRouteFinishedAtDestination();
        // 조건 2: 루트가 파란색인가?
        bool isAllBlue = StaminaManager.Instance.IsRouteAllBlue();
        // 조건 3: 플레이어가 이동 중이 아닌가?
        bool isNotMoving = !RouteManager.Instance.IsPlayerMoving();
        
        // 세 조건을 모두 만족해야 버튼이 활성화됨
        goButton.interactable = isFinished && isAllBlue && isNotMoving;
    }
}