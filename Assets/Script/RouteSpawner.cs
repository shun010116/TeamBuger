using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MazeGenerator가 미로 생성을 완료하면,
/// 모든 '길(Path)' 타일 위에 '루트 프리팹'을 스폰하고 관리합니다.
/// </summary>
public class RouteSpawner : MonoBehaviour
{
    [Tooltip("길 타일 위에 스폰할 루트(+) 프리팹")]
    public GameObject routePrefab;
    
    [Tooltip("생성된 루트 오브젝트들을 담을 부모 (정리용)")]
    public Transform routeContainer;

    // 생성된 모든 루트 컨트롤러를 그리드 좌표로 빠르게 찾기 위한 딕셔너리
    public Dictionary<Vector2Int, RouteController> allRoutes = new Dictionary<Vector2Int, RouteController>();

    /// <summary>
    /// MazeGenerator가 호출합니다. 맵 데이터를 받아 루트를 스폰합니다.
    /// </summary>
    public void SpawnRoutes(MazeGenerator.CellType[,] map, float tileSize)
    {
        if (routePrefab == null)
        {
            Debug.LogError("Route Prefab이 RouteSpawner에 할당되지 않았습니다!");
            return;
        }

        // 1. 기존 루트가 있다면 삭제
        ClearRoutes();

        int width = map.GetLength(0);
        int height = map.GetLength(1);

        // 2. 맵을 순회하며 '길' 타일을 찾음
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // 이 타일이 '길(Path)'이 아니면 건너뜀
                if (map[x, y] != MazeGenerator.CellType.Path)
                {
                    continue;
                }

                // 3. '길' 타일 위에 루트 프리팹 스폰
                Vector3 position = new Vector3(x * tileSize, y * tileSize, 0);
                GameObject routeObj = Instantiate(routePrefab, position, Quaternion.identity, routeContainer);
                RouteController routeController = routeObj.GetComponent<RouteController>();

                if (routeController != null)
                {
                    // 4. 루트 컨트롤러 초기화 및 딕셔너리에 저장
                    routeController.Initialize(x, y);
                    allRoutes.Add(new Vector2Int(x, y), routeController);
                }
            }
        }
    }

    /// <summary>
    /// 딕셔너리를 비우고 모든 루트 오브젝트를 파괴합니다.
    /// </summary>
    public void ClearRoutes()
    {
        allRoutes.Clear();
        for (int i = routeContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(routeContainer.GetChild(i).gameObject);
        }
    }
}