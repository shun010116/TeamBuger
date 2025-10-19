// [중요] 유니티 API(MonoBehaviour, Header 등)를 사용하기 위해 필수입니다.
using UnityEngine;
// List, Dictionary 등을 사용하기 위해 필수입니다.
using System.Collections.Generic; 

/// <summary>
/// 레벨별로 사용할 '길'과 '벽' 프리팹 세트를 저장합니다.
/// (MazeGenerator 클래스 밖에 있어도 됩니다)
/// </summary>
[System.Serializable]
public class LevelPrefabSet
{
    public string levelName;
    public GameObject pathPrefab;
    public GameObject wallPrefab;
}


/// <summary>
/// 사용자의 설정에 맞춰 연결성이 보장되는 미로를 생성하고,
/// RouteSpawner와 PlayerSpawner를 호출합니다.
/// </summary>
public class MazeGenerator : MonoBehaviour // "MonoBehaviour"를 찾기 위해 using UnityEngine; 필요
{
    // --- [수정] ---
    // RouteSpawner에서도 이 타입을 알아야 하므로 'public'으로 변경합니다.
    /// <summary>
    /// 맵 타일의 상태 (벽 또는 길)
    /// </summary>
    public enum CellType
    {
        Wall,
        Path
    }
    // --- [수정 끝] ---

    [Header("맵 설정")] // "Header"를 찾기 위해 using UnityEngine; 필요
    [Tooltip("미로의 가로 크기 (홀수를 권장)")] // "Tooltip"도 동일
    public int width = 21;
    [Tooltip("미로의 세로 크기 (홀수를 권장)")]
    public int height = 21;

    [Header("밀도 및 너비 설정")]
    [Tooltip("미로 내부의 벽이 빽빽한 정도 (0.0 = 길만 있음, 1.0 = 벽으로 가득 참)")]
    [Range(0f, 1f)]
    public float wallDensity = 0.6f;
    [Tooltip("최대 길 너비. 1로 설정 시 좁은 통로를 만듭니다.")]
    public int maxPathWidth = 1;

    [Header("레벨별 에셋 설정")]
    [Tooltip("현재 생성할 미로의 레벨 (이 인덱스에 해당하는 프리팹을 사용)")]
    public int currentLevel = 0;
    [Tooltip("각 레벨에서 사용할 프리팹 세트 목록")]
    public List<LevelPrefabSet> levelPrefabs;

    [Header("에셋 설정 (공통)")]
    [Tooltip("타일 1개의 크기(가로/세로 동일하다고 가정)")]
    public float tileSize = 1.0f;

    [Header("오브젝트 관리")]
    [Tooltip("생성된 맵 타일들을 담을 부모 Transform")]
    public Transform mazeContainer;
    
    [Header("게임 매니저")]
    public PlayerSpawner playerSpawner;
    public RouteSpawner routeSpawner;
    public RestStopSpawner restStopSpawner;

    // 2D 맵 데이터를 저장할 배열
    private CellType[,] map;

    void Start()
    {
        if (mazeContainer == null)
        {
            mazeContainer = transform;
        }
        GenerateMaze();
    }

    /// <summary>
    /// 미로 생성의 전체 과정을 시작합니다.
    /// [수정됨] RouteSpawner와 PlayerSpawner 호출 순서 변경
    /// </summary>
    public void GenerateMaze()
    {
        // 1. 미로 데이터 생성
        ClearMaze();
        InitializeMap();
        CreateSkeleton();
        FillMaze();
        
        if (restStopSpawner != null)
        {
            Vector2Int playerStartPos = new Vector2Int(width / 2, height / 2);
            restStopSpawner.ModifyMapForRestStops(map, playerStartPos);
        }

        // 2. 미로 프리팹 생성
        InstantiateMaze();

        // 3. [추가] 루트 스포너 호출 (플레이어 스폰보다 먼저)
        if (routeSpawner != null)
        {
            // map 데이터와 tileSize를 넘겨줍니다.
            routeSpawner.SpawnRoutes(map, tileSize);
        }
        else
        {
             Debug.LogWarning("MazeGenerator에 RouteSpawner가 할당되지 않았습니다.");
        }

        // 4. [수정] 플레이어 스폰 (루트 생성 *이후*에 호출)
        if (playerSpawner != null) 
        {
            int centerX = width / 2;
            int centerY = height / 2;
            
            Vector3 centerWorldPosition = new Vector3(
                centerX * tileSize, 
                centerY * tileSize, 
                0 
            );
            
            playerSpawner.SpawnPlayer(centerWorldPosition); 
        }
        else
        {
            Debug.LogWarning("MazeGenerator에 PlayerSpawner가 할당되지 않아 플레이어를 스폰할 수 없습니다."); 
        }
    }
    
    // --- [이하 모든 헬퍼 함수는 이전과 동일] ---
    
    void InitializeMap()
    {
        map = new CellType[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                map[x, y] = CellType.Wall;
            }
        }
    }

    void CreateSkeleton()
    {
        Vector2Int center = new Vector2Int(width / 2, height / 2);
        CarveCell(center); 

        Vector2Int exitN = new Vector2Int(Random.Range(1, width - 1), height - 1);
        Vector2Int exitS = new Vector2Int(Random.Range(1, width - 1), 0);
        Vector2Int exitE = new Vector2Int(width - 1, Random.Range(1, height - 1));
        Vector2Int exitW = new Vector2Int(0, Random.Range(1, height - 1));

        CarveCell(exitN); 
        CarveCell(exitS);
        CarveCell(exitE);
        CarveCell(exitW);

        CarvePath(center, exitN);
        CarvePath(center, exitS);
        CarvePath(center, exitE);
        CarvePath(center, exitW);
    }
    
    void CarveCell(Vector2Int cell)
    {
        if (IsCellValid(cell.x, cell.y))
        {
            map[cell.x, cell.y] = CellType.Path;
        }
    }

    void CarvePath(Vector2Int start, Vector2Int end)
    {
        Vector2Int current = start;
        int maxIterations = width * height; 

        for (int i = 0; i < maxIterations; i++)
        {
            if (current == end) break; 

            int dx = end.x - current.x;
            int dy = end.y - current.y;

            Vector2Int nextMove = current;

            if (Random.value < 0.7f)
            {
                if (Mathf.Abs(dx) > Mathf.Abs(dy)) nextMove.x += (int)Mathf.Sign(dx);
                else nextMove.y += (int)Mathf.Sign(dy);
            }
            else
            {
                int randDir = Random.Range(0, 4);
                if (randDir == 0) nextMove.x++;
                else if (randDir == 1) nextMove.x--;
                else if (randDir == 2) nextMove.y++;
                else nextMove.y--;
            }

            if (nextMove != end)
            {
                nextMove.x = Mathf.Clamp(nextMove.x, 1, width - 2);
                nextMove.y = Mathf.Clamp(nextMove.y, 1, height - 2);
            }

            CarveCell(nextMove); 
            current = nextMove;
        }
    }

    void FillMaze()
    {
        float pathRatio = 1.0f - wallDensity;
        int targetPathCount = (int)((width * height) * pathRatio);

        List<Vector2Int> frontiers = new List<Vector2Int>();
        HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (map[x, y] == CellType.Path)
                {
                    pathCells.Add(new Vector2Int(x, y));
                }
            }
        }

        foreach (Vector2Int pathCell in pathCells)
        {
            AddFrontiers(pathCell, frontiers, pathCells);
        }
        
        while (frontiers.Count > 0 && pathCells.Count < targetPathCount)
        {
            int randIndex = Random.Range(0, frontiers.Count);
            Vector2Int cellToCarve = frontiers[randIndex];
            frontiers.RemoveAt(randIndex); 

            if (IsCreatingWideBlock(cellToCarve, maxPathWidth))
            {
                continue; 
            }

            CarveCell(cellToCarve); 
            pathCells.Add(cellToCarve);

            AddFrontiers(cellToCarve, frontiers, pathCells);
        }
    }

    void AddFrontiers(Vector2Int cell, List<Vector2Int> frontiers, HashSet<Vector2Int> pathCells)
    {
        Vector2Int[] neighbors = {
            new Vector2Int(cell.x + 1, cell.y), new Vector2Int(cell.x - 1, cell.y),
            new Vector2Int(cell.x, cell.y + 1), new Vector2Int(cell.x, cell.y - 1)
        };

        foreach (Vector2Int n in neighbors)
        {
            if (IsCellValid(n.x, n.y) && map[n.x, n.y] == CellType.Wall && 
                !pathCells.Contains(n) && !frontiers.Contains(n))
            {
                frontiers.Add(n); 
            }
        }
    }

    bool IsCellValid(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    bool IsCellPath(int x, int y)
    {
        if (!IsCellValid(x, y))
        {
            return false; 
        }
        return map[x, y] == CellType.Path;
    }

    bool IsCreatingWideBlock(Vector2Int cell, int N)
    {
        for (int dx = -N; dx <= 0; dx++)
        {
            for (int dy = -N; dy <= 0; dy++)
            {
                if (CheckBlock(cell.x + dx, cell.y + dy, N + 1, cell))
                {
                    return true;
                }
            }
        }
        return false;
    }

    bool CheckBlock(int startX, int startY, int size, Vector2Int cellToCarve)
    {
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2Int currentCell = new Vector2Int(startX + x, startY + y);
                
                if (currentCell == cellToCarve)
                {
                    continue; 
                }

                if (!IsCellPath(currentCell.x, currentCell.y))
                {
                    return false;
                }
            }
        }
        return true;
    }

    void InstantiateMaze()
    {
        if (levelPrefabs == null || levelPrefabs.Count == 0)
        {
            Debug.LogError("Level Prefabs 목록이 비어있습니다!");
            return;
        }

        currentLevel = Mathf.Clamp(currentLevel, 0, levelPrefabs.Count - 1);
        LevelPrefabSet currentSet = levelPrefabs[currentLevel];

        if (currentSet.pathPrefab == null || currentSet.wallPrefab == null)
        {
            Debug.LogError($"Level {currentLevel} ('{currentSet.levelName}')에 프리팹이 제대로 설정되지 않았습니다!");
            return;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject prefabToInstantiate = (map[x, y] == CellType.Path) 
                    ? currentSet.pathPrefab 
                    : currentSet.wallPrefab;
                
                Vector3 position = new Vector3(x * tileSize, y * tileSize, 0);
                
                Instantiate(prefabToInstantiate, position, Quaternion.identity, mazeContainer);
            }
        }
    }

    public void RegenerateMaze()
    {
        GenerateMaze();
    }

    public void ClearMaze()
    {
        for (int i = mazeContainer.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(mazeContainer.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(mazeContainer.GetChild(i).gameObject);
            }
        }
    }
}