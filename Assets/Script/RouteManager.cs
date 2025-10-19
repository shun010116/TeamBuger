using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic; // List와 Stack 사용
using System.Linq; // Stack을 순회하기 위해 Linq 사용

/// <summary>
/// 그려진 '선분' 1개의 정보를 저장하는 클래스 (Stack에 저장될 데이터)
/// [수정됨] 렌더링 순서(sortingOrder)를 포함합니다.
/// </summary>
public class RouteSegment
{
    public RouteController fromTile;
    public RouteController toTile;
    public PathDirection exitDir; // fromTile에서 나가는 방향
    public PathDirection entryDir; // toTile로 들어오는 방향
    public bool wasStaminaDepleted; // 이 선분이 그려질 때의 스테미나 상태 (색상)
    public int sortingOrder; // 이 선분의 렌더링 순서

    public RouteSegment(RouteController from, RouteController to, PathDirection exit, PathDirection entry, bool depleted, int order)
    {
        this.fromTile = from;
        this.toTile = to;
        this.exitDir = exit;
        this.entryDir = entry;
        this.wasStaminaDepleted = depleted;
        this.sortingOrder = order; 
    }

    /// <summary>
    /// 이 선분이 특정 타일의 특정 방향 파츠를 '사용'하는지 확인
    /// </summary>
    public bool UsesPart(RouteController tile, PathDirection dir)
    {
        if (tile == fromTile && dir == exitDir) return true;
        if (tile == toTile && dir == entryDir) return true;
        return false;
    }
}


/// <summary>
/// [최종 버전]
/// 마우스 입력으로 루트 그리기를 제어합니다. (스택 기반)
/// 플레이어 이동을 트리거하고, GO 버튼 상태를 관리하며, 목적지 이벤트를 연계합니다.
/// </summary>
public class RouteManager : MonoBehaviour
{
    public static RouteManager Instance { get; private set; }

    [Header("필수 참조")]
    [Tooltip("모든 루트 타일 정보를 가진 RouteSpawner")]
    public RouteSpawner routeSpawner;
    [Tooltip("플레이어 오브젝트 참조를 위한 PlayerSpawner")]
    public PlayerSpawner playerSpawner;
    [Tooltip("맵 데이터(map)와 타일 크기(tileSize)를 가져올 MazeGenerator")]
    public MazeGenerator mazeGenerator;
    [Tooltip("스크린 좌표를 월드 좌표로 변환할 메인 카메라")]
    public Camera mainCamera;
    [Tooltip("목적지(휴게소) 목록을 가져올 RestStopSpawner")]
    public RestStopSpawner restStopSpawner; 
    
    // PlayerMove 스크립트 참조
    private PlayerMove playerMove;

    [Header("연속 지우기 설정")]
    [Tooltip("우클릭을 꾹 누르기 시작한 후, 연속 지우기가 발동되기까지의 딜레이 (초)")]
    public float undoHoldDelay = 0.5f;
    [Tooltip("연속 지우기 발동 시, 지워지는 속도 (초당 횟수)")]
    public float undoRepeatRate = 0.1f; 

    [Header("렌더링 설정")]
    [Tooltip("루트가 맵(0)보다 위에 그려지도록 하기 위한 기본 Sorting Order 값")]
    public int baseSortingOrder = 1; // (플레이어(100)보다 훨씬 낮아야 함)

    // Input Actions
    private PlayerControls playerControls;

    // 그리기 상태
    private bool isDrawing = false;
    
    // 이동/이벤트 중 그리기 방지
    private bool isPlayerMoving = false; // PlayerMove가 제어
    private bool allowDrawing = true; // 이벤트가 제어

    // 루트 기록 (스택)
    private Stack<RouteSegment> routeHistory = new Stack<RouteSegment>();
    private RouteController lastRouteTile = null; // 새 그리기를 시작할 '마지막 타일'

    // GO 버튼 조건용
    private IDestination lastTouchedDestination = null;

    // 연속 지우기 상태 변수
    private bool isHoldingUndo = false;
    private float nextUndoTime = 0f;

    void Awake()
    {
        if (Instance == null) 
        { 
            Instance = this; 
            playerControls = new PlayerControls();
        } 
        else 
        { 
            Destroy(gameObject); 
        }
    }
    
    // (Script Execution Order에 의해 MazeGenerator(및 PlayerSpawner)가 먼저 실행됨)
    void Start()
    {
        // PlayerMove 스크립트 참조 가져오기
        if (playerSpawner.SpawnedPlayer != null)
        {
            playerMove = playerSpawner.SpawnedPlayer.GetComponent<PlayerMove>();
        }
        else
        {
            // (Script Execution Order가 올바르다면 이 로그는 뜨지 않아야 함)
            Debug.LogError("RouteManager: Start()에서 SpawnedPlayer를 찾을 수 없습니다. 실행 순서를 확인하세요.");
        }
    }

    private void OnEnable()
    {
        playerControls.Player.Enable();
        playerControls.Player.Click.performed += StartDrawing;
        playerControls.Player.Click.canceled += StopDrawing;
        playerControls.Player.ResetRoute.performed += ResetAllRoutes;
        playerControls.Player.Undo.started += StartUndoHold;
        playerControls.Player.Undo.canceled += StopUndoHold;
        playerControls.Player.SubmitRoute.performed += OnSubmitRoute;
    }

    private void OnDisable()
    {
        playerControls.Player.Disable();
        playerControls.Player.Click.performed -= StartDrawing;
        playerControls.Player.Click.canceled -= StopDrawing;
        playerControls.Player.ResetRoute.performed -= ResetAllRoutes;
        playerControls.Player.Undo.started -= StartUndoHold;
        playerControls.Player.Undo.canceled -= StopUndoHold;
        playerControls.Player.SubmitRoute.performed -= OnSubmitRoute;
    }

    private void Update()
    {
        // 이동 중이거나, 그리기가 금지된 상태(이벤트 중)면 Update 중지
        if (isPlayerMoving || !allowDrawing)
        {
            isDrawing = false; // 강제 중지
            return;
        }

        // 그리기 로직 (isDrawing이 true일 때)
        if (isDrawing)
        {
            Vector2Int currentGridPos = GetGridPosFromMouse();
            if (lastRouteTile != null && currentGridPos != lastRouteTile.gridPos && 
                routeSpawner.allRoutes.ContainsKey(currentGridPos))
            {
                Vector2Int diff = currentGridPos - lastRouteTile.gridPos;
                if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) == 1)
                {
                    DrawSegment(lastRouteTile, currentGridPos);
                }
            }
        }

        // 연속 지우기 로직
        if (isHoldingUndo && !isDrawing)
        {
            if (Time.time > nextUndoTime)
            {
                CallUndoOnce();
                nextUndoTime = Time.time + undoRepeatRate;
            }
        }
    }

    /// <summary>
    /// 그리기 시작/재개 (목적지에서 인접 타일로 재시작 포함)
    /// </summary>
    




    private void StartDrawing(InputAction.CallbackContext context)
    {
        // 공통 전제 조건
        if (!allowDrawing || isPlayerMoving) return;

        // 클릭 정보 가져오기
        Vector2Int clickedGridPos = GetGridPosFromMouse();
        if (!routeSpawner.allRoutes.ContainsKey(clickedGridPos)) return;
        RouteController clickedRouteTile = routeSpawner.allRoutes[clickedGridPos];

        // 플레이어 정보 가져오기
        if (playerSpawner.SpawnedPlayer == null) return;
        Vector2Int playerGridPos = GetGridPosFromWorld(playerSpawner.SpawnedPlayer.transform.position);
        RouteController playerCurrentRouteTile = routeSpawner.allRoutes.ContainsKey(playerGridPos) ? routeSpawner.allRoutes[playerGridPos] : null;

        // --- 조건 분기 ---

        // 3. 이어 그리기 (routeHistory에 이미 내용이 있음)
        if (routeHistory.Count > 0)
        {
            RouteController lastSegmentEndTile = routeHistory.Peek().toTile;
            // 마지막 타일을 클릭했는지 확인
            if (clickedGridPos == lastSegmentEndTile.gridPos)
            {
                // 목적지 잠금 확인
                IDestination dest = GetDestinationAt(clickedGridPos);
                bool isDepleted = StaminaManager.Instance.IsStaminaDepleted();
                if (dest != null && !dest.AllowRoutePassThrough && !isDepleted)
                { /* 이어 그리기 불가 */ }
                else
                {
                    isDrawing = true;
                    lastRouteTile = clickedRouteTile;
                }
            }
        }
        // 1 & 2. 새 루트 시작 (routeHistory가 비어 있음)
        else
        {
            // 플레이어가 현재 어떤 목적지 안에 있는지 확인
            IDestination currentDestination = GetDestinationAt(playerGridPos);
            bool playerIsInDestination = currentDestination != null;

            // 2. Player가 목적지 내부에 있는 경우
            if (playerIsInDestination)
            {
                RectInt destinationArea = currentDestination.GetArea();

                // 클릭한 타일이 목적지 '외부'에 있는지 확인
                if (!destinationArea.Contains(clickedGridPos))
                {
                    // 클릭한 타일이 목적지 영역과 '인접'한지 확인 (상하좌우 한 칸 차이)
                    bool isAdjacent = false;
                    // 가로 인접 체크 (클릭한 X가 영역 왼쪽/오른쪽 경계이고, Y가 영역 내부에 있음)
                    if ((clickedGridPos.x == destinationArea.xMin - 1 || clickedGridPos.x == destinationArea.xMax) &&
                        (clickedGridPos.y >= destinationArea.yMin && clickedGridPos.y < destinationArea.yMax))
                    {
                        isAdjacent = true;
                    }
                    // 세로 인접 체크 (클릭한 Y가 영역 아래/위쪽 경계이고, X가 영역 내부에 있음)
                    else if ((clickedGridPos.y == destinationArea.yMin - 1 || clickedGridPos.y == destinationArea.yMax) &&
                             (clickedGridPos.x >= destinationArea.xMin && clickedGridPos.x < destinationArea.xMax))
                    {
                        isAdjacent = true;
                    }

                    // 목적지 외부에 있고 + 인접한 Path 타일이면 시작 가능
                    if (isAdjacent)
                    {
                        isDrawing = true;
                        // 시작점은 클릭된 '인접 타일'
                        lastRouteTile = clickedRouteTile;
                        // (첫 선분은 마우스 드래그 시 DrawSegment에서 그려짐)
                    }
                }
                // (목적지 내부인데 목적지 내부 다른 곳 클릭 시 아무것도 안 함)
            }
            // 1. Player가 목적지 내부에 있지 않은 경우 (일반 Path 위)
            else
            {
                // 클릭한 타일이 플레이어의 현재 위치와 같은지 확인
                if (clickedGridPos == playerGridPos && playerCurrentRouteTile != null)
                {
                    // 플레이어 위치에서 시작
                    isDrawing = true;
                    lastRouteTile = playerCurrentRouteTile;
                }
                // (일반 Path인데 플레이어 위치 아닌 곳 클릭 시 아무것도 안 함)
            }
        }
    }

    




    private void StopDrawing(InputAction.CallbackContext context)
    {
        isDrawing = false;
    }

    /// <summary>
    /// 선분을 그리고 스택에 저장 (SortingOrder, 목적지 잠금 포함)
    /// </summary>
    private void DrawSegment(RouteController fromTile, Vector2Int toGridPos)
    {
        RouteController toTile = routeSpawner.allRoutes[toGridPos];
        lastTouchedDestination = null; // 일단 리셋

        // 1. 방향, 스테미나, 순서 계산
        Vector2Int dirVector = toGridPos - fromTile.gridPos;
        PathDirection exitDir = GetDirectionFromVector(dirVector);
        PathDirection entryDir = GetOppositeDirection(exitDir);
        bool isDepleted = StaminaManager.Instance.IsStaminaDepleted(); 
        int newSortingOrder = baseSortingOrder + routeHistory.Count;

        // 2. 활성화 (4개 인자 전달)
        fromTile.ActivatePart(exitDir, true, isDepleted, newSortingOrder);
        toTile.ActivatePart(entryDir, true, isDepleted, newSortingOrder);

        // 3. 새 선분 정보 저장 (6개 인자 전달)
        RouteSegment newSegment = new RouteSegment(fromTile, toTile, exitDir, entryDir, isDepleted, newSortingOrder);
        routeHistory.Push(newSegment);
        lastRouteTile = toTile;
        UpdateStaminaFromHistory();

        // 4. 목적지 도달 확인 (빨간 루트는 멈추지 않음)
        IDestination dest = GetDestinationAt(toGridPos);
        if (dest != null && !isDepleted) 
        {
            lastTouchedDestination = dest; // GO 버튼 활성화용
            if (!dest.AllowRoutePassThrough)
            {
                isDrawing = false; // 그리기 즉시 중지
            }
        }
    }
    
    // --- G키 / GO 버튼 로직 ---

    private void OnSubmitRoute(InputAction.CallbackContext context)
    {
        TriggerPlayerMovement();
    }

    /// <summary>
    /// (UIManager가 호출) GO 버튼 클릭 시 이동 시작
    /// </summary>
    public void TriggerPlayerMovement()
    {
        if (UIManager.Instance == null || !UIManager.Instance.goButton.interactable)
        {
            return; // GO 버튼이 활성화된 상태가 아니면 무시
        }
        
        if (playerMove == null)
        {
            // Start()에서 실패 시, GO 버튼 누를 때 다시 한번 찾기 시도
            if (playerSpawner.SpawnedPlayer == null) {
                Debug.LogError("PlayerMove를 찾을 수 없습니다. 플레이어가 스폰되지 않았습니다!");
                return;
            }
            playerMove = playerSpawner.SpawnedPlayer.GetComponent<PlayerMove>();
            if (playerMove == null)
            {
                Debug.LogError("PlayerMove 스크립트가 없습니다! Player 프리팹을 확인하세요.");
                return;
            }
        }
        

        // 1. 스택을 '순방향 List'로 변환
        List<Vector3> worldPath = new List<Vector3>();
        worldPath.Add(playerSpawner.SpawnedPlayer.transform.position); 
        
        float tileSize = mazeGenerator.tileSize;
        foreach (var segment in routeHistory.Reverse()) // Reverse()로 순방향 (A->B, B->C)
        {
            worldPath.Add(GetWorldPosFromGrid(segment.toTile.gridPos, tileSize));
        }

        // 2. PlayerMove에 경로와 '도착지 정보'를 전달
        playerMove.StartMovement(worldPath, lastTouchedDestination);
    }
    
    /// <summary>
    /// (PlayerMove가 이동 완료 후 호출)
    /// </summary>
    public void OnPlayerMovementFinished(IDestination destination)
    {
        // 1. 도착한 곳이 유효한 목적지(휴게소)가 맞는지 확인
        if (destination != null && destination is RestStopEvent)
        {
            // 2. 해당 휴게소의 이벤트를 수동으로 실행
            RestStopEvent restStopEvent = (RestStopEvent)destination;
            restStopEvent.StartEventSequenceManual(playerSpawner.SpawnedPlayer.transform);
        }
    }

    // --- 상태 함수들 (외부 호출용) ---

    public bool IsPlayerMoving() => isPlayerMoving;
    public void SetPlayerMoving(bool state)
    {
        isPlayerMoving = state;
    }
    
    // (GO 버튼 활성화 조건 1)
    public bool IsRouteFinishedAtDestination()
    {
        return lastTouchedDestination != null;
    }

    // --- (연속 지우기 / 리셋 로직) ---

    private void StartUndoHold(InputAction.CallbackContext context)
    {
        if (isDrawing || !allowDrawing || isPlayerMoving) return;
        isHoldingUndo = true;
        nextUndoTime = Time.time + undoHoldDelay;
        CallUndoOnce();
    }

    private void StopUndoHold(InputAction.CallbackContext context)
    {
        isHoldingUndo = false;
    }

    /// <summary>
    /// ResetAllRoutes가 (context) 인자 없이 호출될 수 있도록 오버로딩
    /// </summary>
    public void ResetAllRoutes(InputAction.CallbackContext context)
    {
        Internal_ResetAllRoutes();
    }
    
    /// <summary>
    /// (RestStopEvent가 호출할 함수) 실제 리셋 로직
    /// </summary>
    public void Internal_ResetAllRoutes()
    {
        foreach (RouteController route in routeSpawner.allRoutes.Values)
        {
            route.ResetRoute();
        }
        routeHistory.Clear(); 
        
        // 'lastRouteTile'을 플레이어 위치로 리셋
        if (playerSpawner.SpawnedPlayer != null)
        {
            Vector2Int playerGridPos = GetGridPosFromWorld(playerSpawner.SpawnedPlayer.transform.position);
            lastRouteTile = routeSpawner.allRoutes.ContainsKey(playerGridPos) ? routeSpawner.allRoutes[playerGridPos] : null;
        } else {
            lastRouteTile = null;
        }
        
        isDrawing = false;
        lastTouchedDestination = null; // 목적지 도달 상태 리셋
        UpdateStaminaFromHistory(); // 스테미나 갱신
    }

    /// <summary>
    /// 스택 Pop을 이용한 되돌리기 (SortingOrder 복원 포함)
    /// </summary>
    private void CallUndoOnce()
    {
        if (isDrawing || routeHistory.Count == 0 || !allowDrawing || isPlayerMoving) return;

        RouteSegment segmentToUndo = routeHistory.Pop();
        
        RouteController fromTile = segmentToUndo.fromTile;
        RouteController toTile = segmentToUndo.toTile;
        PathDirection exitDir = segmentToUndo.exitDir;
        PathDirection entryDir = segmentToUndo.entryDir;

        // 1. [임시] 두 파츠를 *일단 비활성화*
        fromTile.ActivatePart(exitDir, false, false, 0);
        toTile.ActivatePart(entryDir, false, false, 0);

        // 2. 남아있는 스택에서 이 파츠를 쓰는 '최신' 선분 검색
        RouteSegment segmentToRedraw_From = routeHistory.FirstOrDefault(seg => seg.UsesPart(fromTile, exitDir));
        RouteSegment segmentToRedraw_To = routeHistory.FirstOrDefault(seg => seg.UsesPart(toTile, entryDir));

        // 3. [복원] 만약 'from' 파츠를 쓰는 선분이 남아있다면, 
        //    그 선분의 '색상'과 '순서'로 다시 켬
        if (segmentToRedraw_From != null)
        {
            fromTile.ActivatePart(exitDir, true, segmentToRedraw_From.wasStaminaDepleted, segmentToRedraw_From.sortingOrder);
        }

        // 4. [복원] 'to' 파츠도 동일하게 처리
        if (segmentToRedraw_To != null)
        {
            toTile.ActivatePart(entryDir, true, segmentToRedraw_To.wasStaminaDepleted, segmentToRedraw_To.sortingOrder);
        }

        // 5. 'lastRouteTile' 갱신
        if (routeHistory.Count > 0)
        {
            lastRouteTile = routeHistory.Peek().toTile;
        }
        else if (playerSpawner.SpawnedPlayer != null)
        {
            Vector2Int playerGridPos = GetGridPosFromWorld(playerSpawner.SpawnedPlayer.transform.position);
            lastRouteTile = routeSpawner.allRoutes.ContainsKey(playerGridPos) ? routeSpawner.allRoutes[playerGridPos] : null;
        } else {
            lastRouteTile = null;
        }
        
        // 6. Undo로 인해 목적지에서 벗어났는지 확인
        if (lastRouteTile != null)
        {
            lastTouchedDestination = GetDestinationAt(lastRouteTile.gridPos);
        } else {
            lastTouchedDestination = null;
        }
        
        // 7. 스테미나 매니저 갱신
        UpdateStaminaFromHistory();
    }
    
    // --- 헬퍼 함수들 ---

    /// <summary>
    /// 스택의 '크기'를 기반으로 스테미나 갱신
    /// </summary>
    private void UpdateStaminaFromHistory()
    {
        if (StaminaManager.Instance != null)
        {
            StaminaManager.Instance.UpdateStamina(routeHistory.Count);
        }
    }
    
    /// <summary>
    /// 특정 그리드 좌표에 목적지가 있는지 확인
    /// </summary>
    private IDestination GetDestinationAt(Vector2Int gridPos)
    {
        if (restStopSpawner == null) return null;

        foreach (var restStop in restStopSpawner.GetSpawnedRestStops())
        {
            if (restStop.GetArea().Contains(gridPos))
            {
                return restStop;
            }
        }
        return null;
    }
    
    /// <summary>
    /// 플레이어가 현재 목적지에 있는지 확인 (Prereq 4)
    /// </summary>
    private bool IsAtDestination(Vector2Int playerGridPos)
    {
        return GetDestinationAt(playerGridPos) != null;
    }
    
    private Vector3 GetWorldPosFromGrid(Vector2 gridPos, float tileSize)
    {
        return new Vector3(gridPos.x * tileSize, gridPos.y * tileSize, 0);
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();
        mousePos.z = mainCamera.nearClipPlane + 10f; 
        return mainCamera.ScreenToWorldPoint(mousePos);
    }

    private Vector2Int GetGridPosFromMouse()
    {
        return GetGridPosFromWorld(GetMouseWorldPos());
    }

    private Vector2Int GetGridPosFromWorld(Vector3 worldPos)
    {
        float tileSize = (mazeGenerator != null) ? mazeGenerator.tileSize : 1.0f;
        int x = Mathf.RoundToInt(worldPos.x / tileSize);
        int y = Mathf.RoundToInt(worldPos.y / tileSize);
        return new Vector2Int(x, y);
    }

    private PathDirection GetDirectionFromVector(Vector2Int dir)
    {
        if (dir.x > 0) return PathDirection.East;
        if (dir.x < 0) return PathDirection.West;
        if (dir.y > 0) return PathDirection.North;
        if (dir.y < 0) return PathDirection.South;
        return PathDirection.None;
    }

    private PathDirection GetOppositeDirection(PathDirection dir)
    {
        switch (dir)
        {
            case PathDirection.North: return PathDirection.South;
            case PathDirection.South: return PathDirection.North;
            case PathDirection.East: return PathDirection.West;
            case PathDirection.West: return PathDirection.East;
            default: return PathDirection.None;
        }
    }
}