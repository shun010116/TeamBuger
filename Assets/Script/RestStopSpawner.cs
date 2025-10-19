using UnityEngine;
using System.Collections.Generic;
using System.Linq; 

/// <summary>
/// [수정됨] 누락되었던 헬퍼 함수들 추가
/// </summary>
public class RestStopSpawner : MonoBehaviour
{
    [Header("필수 참조")]
    public MazeGenerator mazeGenerator;
    public Transform restStopContainer;
    
    // [Header("스폰 프리팹")]
    // [Tooltip("휴게소 영역의 중심에 스폰할 프리팹 (NPC, 아이템 상자 등)")]
    // public GameObject restStopContentPrefab; 
    [Tooltip("휴게소 영역의 네 꼭짓점을 표시할 스프라이트 프리팹")]
    public GameObject cornerSpritePrefab; 
    [Tooltip("휴게소 이벤트 로직을 담을 프리팹 (RestStopEvent.cs 포함)")]
    public GameObject restStopEventPrefab; 

    [Header("휴게소 크기 설정")]
    [Range(1, 5)] public int minWidth = 2;
    [Range(1, 5)] public int maxWidth = 3;
    [Range(1, 5)] public int minHeight = 2;
    [Range(1, 5)] public int maxHeight = 3;

    [Header("막다른 길 우선 배치 (Rule 3.5)")]
    public int deadEndSearchRadius = 3;

    [Header("단계 1: 근거리 휴게소 (Rule 1)")]
    public int nearRestStopCount = 2;
    public float minSpawnDistance = 10f;
    public float maxSpawnDistance = 30f;
    public float minRestStopSpacing = 8f;

    [Header("단계 2: 원거리 휴게소 (Rule 2)")]
    public int farRestStopCount = 3;
    public float farRestStopSpacing = 15f;


    private MazeGenerator.CellType[,] map;
    private List<Vector2Int> allPathTiles = new List<Vector2Int>();
    private List<RestStopEvent> spawnedRestStops = new List<RestStopEvent>();
    public List<RestStopEvent> GetSpawnedRestStops() => spawnedRestStops;


    public void ModifyMapForRestStops(MazeGenerator.CellType[,] mapData, Vector2Int playerStartPos)
    {
        this.map = mapData;
        spawnedRestStops.Clear();
        allPathTiles.Clear();

        for (int x = 0; x < map.GetLength(0); x++)
        {
            for (int y = 0; y < map.GetLength(1); y++)
            {
                if (map[x, y] == MazeGenerator.CellType.Path)
                {
                    allPathTiles.Add(new Vector2Int(x, y));
                }
            }
        }

        FindAndPlaceRestStops(
            playerStartPos, nearRestStopCount, minSpawnDistance, 
            maxSpawnDistance, minRestStopSpacing
        );

        FindAndPlaceRestStops(
            playerStartPos, farRestStopCount, maxSpawnDistance, 
            9999f, farRestStopSpacing
        );
    }

    private void FindAndPlaceRestStops(Vector2Int center, int count, float minRange, float maxRange, float spacing)
    {
        List<Vector2Int> candidates = allPathTiles.Where(pos => 
        {
            float dist = Vector2Int.Distance(center, pos);
            return dist >= minRange && dist <= maxRange;
        })
        .OrderBy(pos => Random.value)
        .ToList();

        int placedCount = 0;
        foreach (Vector2Int candidatePos in candidates)
        {
            if (placedCount >= count) break;

            bool spacedEnough = spawnedRestStops.All(restStop => 
            {
                float dist = Vector2.Distance(candidatePos, restStop.GetArea().center);
                return dist >= spacing;
            });

            if (spacedEnough)
            {
                // --- [오류 수정 지점] ---
                // 이 함수가 누락되었습니다.
                Vector2Int finalPos = FindDeadEndPriority(candidatePos);
                // --- [수정 끝] ---
                
                PlaceSingleRestStop(finalPos);
                placedCount++;
            }
        }
    }

    private void PlaceSingleRestStop(Vector2Int origin)
    {
        int width = Random.Range(minWidth, maxWidth + 1);
        int height = Random.Range(minHeight, maxHeight + 1);

        // 그리드 좌표 기준 영역 계산 (좌하단 기준)
        int startX = Mathf.Max(0, origin.x - width / 2);
        int startY = Mathf.Max(0, origin.y - height / 2);
        // 영역이 맵 경계를 넘지 않도록 보정 (오른쪽/위쪽)
        int endX = Mathf.Min(startX + width, map.GetLength(0));
        int endY = Mathf.Min(startY + height, map.GetLength(1));
        // 실제 적용될 너비/높이 재계산
        int actualWidth = endX - startX;
        int actualHeight = endY - startY;

        // 최종 그리드 영역
        RectInt area = new RectInt(startX, startY, actualWidth, actualHeight);

        // --- 벽 제거 ---
        // 'area' 범위 내 모든 타일을 Path로 변경 (맵 경계 체크 포함)
        for (int x = area.xMin; x < area.xMax; x++)
        {
            for (int y = area.yMin; y < area.yMax; y++)
            {
                // IsCellValid는 여기서 불필요 (area 계산 시 이미 반영됨)
                map[x, y] = MazeGenerator.CellType.Path;
            }
        }

        float tileSize = mazeGenerator.tileSize;

        // --- 이벤트 오브젝트 및 콜라이더 ---
        // 영역의 '중심' 월드 좌표 계산 (타일 중심 기준)
        Vector3 areaCenterWorld = new Vector3(
            (area.xMin + actualWidth / 2f - 0.5f) * tileSize,
            (area.yMin + actualHeight / 2f - 0.5f) * tileSize,
            0
        );

        // 콜라이더 크기 (실제 적용된 너비/높이 사용)
        Vector2 colliderSize = new Vector2(actualWidth * tileSize, actualHeight * tileSize);

        // 이벤트 핸들러 스폰
        if (restStopEventPrefab == null) { /* ... 에러 처리 ... */ return; }
        GameObject eventObj = Instantiate(restStopEventPrefab, areaCenterWorld, Quaternion.identity, restStopContainer);
        RestStopEvent restStopEvent = eventObj.GetComponent<RestStopEvent>();
        if (restStopEvent == null) { /* ... 에러 처리 ... */ return; } // Null check 추가!

        // 콜라이더 가져와서 크기 설정
        BoxCollider2D collider = eventObj.GetComponent<BoxCollider2D>();
        if (collider == null) { /* ... 에러 처리 ... */ return; }
        collider.size = colliderSize;

        // 이벤트 초기화 및 등록
        restStopEvent.Initialize(area);
        spawnedRestStops.Add(restStopEvent);

        // --- 내용물 스폰 ---
        // if (restStopContentPrefab != null)
        // {
        //     Instantiate(restStopContentPrefab, areaCenterWorld, Quaternion.identity, eventObj.transform);
        // }

        // --- 코너 스프라이트 ---
        if (cornerSpritePrefab != null)
        {
            // 타일 경계 기준 모서리 좌표 계산
            float leftEdge   = area.xMin * tileSize - (tileSize / 2f);
            float rightEdge  = (area.xMax * tileSize) - (tileSize / 2f); // xMax는 포함 안 됨
            float bottomEdge = area.yMin * tileSize - (tileSize / 2f);
            float topEdge    = (area.yMax * tileSize) - (tileSize / 2f); // yMax는 포함 안 됨

            // 네 모서리에 스폰
            Instantiate(cornerSpritePrefab, new Vector3(leftEdge, bottomEdge, 0), Quaternion.identity, eventObj.transform);
            Instantiate(cornerSpritePrefab, new Vector3(rightEdge, bottomEdge, 0), Quaternion.identity, eventObj.transform);
            Instantiate(cornerSpritePrefab, new Vector3(leftEdge, topEdge, 0), Quaternion.identity, eventObj.transform);
            Instantiate(cornerSpritePrefab, new Vector3(rightEdge, topEdge, 0), Quaternion.identity, eventObj.transform);
        }
    }




    // --- [누락된 헬퍼 함수들 추가] ---

    /// <summary>
    /// (Rule 3.5) 후보지 주변에 막다른 길이 있는지 탐색하고, 있다면 그 위치를 반환
    /// </summary>
    



    private Vector2Int FindDeadEndPriority(Vector2Int candidatePos)
    {
        int minX = Mathf.Max(1, candidatePos.x - deadEndSearchRadius);
        int maxX = Mathf.Min(map.GetLength(0) - 2, candidatePos.x + deadEndSearchRadius);
        int minY = Mathf.Max(1, candidatePos.y - deadEndSearchRadius);
        int maxY = Mathf.Min(map.GetLength(1) - 2, candidatePos.y + deadEndSearchRadius);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2Int checkPos = new Vector2Int(x, y);
                if (map[x, y] == MazeGenerator.CellType.Path && IsDeadEnd(checkPos))
                {
                    return checkPos; // 우선 배치 위치 반환
                }
            }
        }
        return candidatePos; // 막다른 길 없음. 원래 후보지 반환
    }

    /// <summary>
    /// (Rule 3.5 헬퍼) 해당 좌표가 3면이 벽인 '막다른 길'인지 확인
    /// </summary>
    private bool IsDeadEnd(Vector2Int pos)
    {
        int wallCount = 0;
        if (IsWall(pos.x + 1, pos.y)) wallCount++;
        if (IsWall(pos.x - 1, pos.y)) wallCount++;
        if (IsWall(pos.x, pos.y + 1)) wallCount++;
        if (IsWall(pos.x, pos.y - 1)) wallCount++;
        
        return wallCount >= 3;
    }

    private bool IsCellValid(int x, int y)
    {
        return x >= 0 && x < map.GetLength(0) && y >= 0 && y < map.GetLength(1);
    }

    private bool IsWall(int x, int y)
    {
        if (!IsCellValid(x, y)) return true; // 맵 밖은 벽으로 간주
        return map[x, y] == MazeGenerator.CellType.Wall;
    }

    private Vector3 GetWorldPosFromGrid(Vector2 gridPos, float tileSize)
    {
        // [수정] (float)gridPos.x가 아닌 gridPos.x 사용
        return new Vector3(gridPos.x * tileSize, gridPos.y * tileSize, 0);
    }
    // --- [헬퍼 함수 추가 끝] ---
}