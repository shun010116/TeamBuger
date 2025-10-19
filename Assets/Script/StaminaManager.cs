using UnityEngine;
using UnityEngine.UI; // UI(Image)를 사용하기 위해 필요

/// <summary>
/// 플레이어의 스테미나(최대치, 현재치)를 관리하고 UI에 반영합니다.
/// [수정됨] Slider 구조 내의 2개 Fill 이미지를 제어합니다.
/// </summary>
public class StaminaManager : MonoBehaviour
{
    public static StaminaManager Instance { get; private set; }

    [Header("스테미나 설정")]
    public float maxStamina = 100f;
    public float staminaCostPerTile = 1f;

    [Header("UI 연결")]
    // [수정] Slider 컴포넌트 대신 Slider 내부의 Fill 이미지 2개를 연결
    [Tooltip("정상 스테미나를 표시할 UI Image (예: Slider/Fill Area/Fill_Normal)")]
    public Image staminaBar_Normal; // 'Fill_Normal' (파란색) 오브젝트 연결
    [Tooltip("부족한 스테미나(빚)를 표시할 UI Image (예: Slider/Fill Area/Fill_Debt)")]
    public Image staminaBar_Debt; // 'Fill_Debt' (빨간색) 오브젝트 연결

    public float CurrentStamina { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            CurrentStamina = maxStamina;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateUI(); // UI 초기화
    }

    /// <summary>
    /// 현재 스테미나가 0 이하인지 확인합니다.
    /// </summary>
    public bool IsStaminaDepleted()
    {
        return CurrentStamina <= 0;
    }

    /// <summary>
    /// 현재 그려진 루트의 길이를 받아 스테미나를 갱신합니다.
    /// </summary>
    public void UpdateStamina(int pathLength)
    {
        // CurrentStamina는 0 이하로 계속 내려갈 수 있음 (예: -20)
        CurrentStamina = maxStamina - (pathLength * staminaCostPerTile);
        UpdateUI();
    }

    /// <summary>
    /// 스테미나를 최대치로 리셋합니다.
    /// </summary>
    public void ResetStamina()
    {
        CurrentStamina = maxStamina;
        UpdateUI();
    }

    /// <summary>
    /// 2개의 Fill 이미지 값을 현재 스테미나 비율로 업데이트합니다.
    /// </summary>
    private void UpdateUI()
    {
        if (staminaBar_Normal == null || staminaBar_Debt == null) return;

        // 1. 정상 스테미나 (파란색 바)
        // CurrentStamina가 0~100 사이일 때만 채워짐
        float normalFill = Mathf.Clamp01(CurrentStamina / maxStamina);
        staminaBar_Normal.fillAmount = normalFill;

        // 2. 부족한 스테미나 (빨간색 바)
        // CurrentStamina가 0 미만일 때 (예: -20) 그 절대값을 채움
        float debtAmount = Mathf.Abs(Mathf.Min(0, CurrentStamina));
        float debtFill = Mathf.Clamp01(debtAmount / maxStamina);
        staminaBar_Debt.fillAmount = debtFill;
    }

    
    public bool IsRouteAllBlue()
    {
        // CurrentStamina가 0 '이상'이어야 함
        return CurrentStamina >= 0;
    }

    /// <summary>
    /// (RestStopEvent용) 설정된 값만큼 스테미나를 회복 (최대치 초과 X)
    /// </summary>
    public void RestoreStamina(float amount)
    {
        // 빚(예: -20)이 있어도 회복됨
        CurrentStamina = Mathf.Min(CurrentStamina + amount, maxStamina);
        UpdateUI();
    }
}