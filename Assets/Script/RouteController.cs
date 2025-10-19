using UnityEngine;


public enum PathDirection
{
    None, North, South, East, West
}




public class RouteController : MonoBehaviour
{
    [Header("파란색 루트 (기본)")]
    public GameObject routeNorth_Blue;
    public GameObject routeSouth_Blue;
    public GameObject routeEast_Blue;
    public GameObject routeWest_Blue;

    [Header("빨간색 루트 (스테미나 고갈)")]
    public GameObject routeNorth_Red;
    public GameObject routeSouth_Red;
    public GameObject routeWest_Red;
    public GameObject routeEast_Red;

    public Vector2Int gridPos;

    private Renderer[] blueRenderers = new Renderer[4]; // N, S, E, W
    private Renderer[] redRenderers = new Renderer[4];


    void Awake() // Start 대신 Awake에서 캐싱
    {
        // GetComponent는 한번만!
        blueRenderers[0] = routeNorth_Blue?.GetComponent<Renderer>();
        blueRenderers[1] = routeSouth_Blue?.GetComponent<Renderer>();
        blueRenderers[2] = routeEast_Blue?.GetComponent<Renderer>();
        blueRenderers[3] = routeWest_Blue?.GetComponent<Renderer>();
        redRenderers[0] = routeNorth_Red?.GetComponent<Renderer>();
        redRenderers[1] = routeSouth_Red?.GetComponent<Renderer>();
        redRenderers[2] = routeEast_Red?.GetComponent<Renderer>();
        redRenderers[3] = routeWest_Red?.GetComponent<Renderer>();
    }

    public void Initialize(int x, int y)
    {
        gridPos = new Vector2Int(x, y);
        ResetRoute();
    }



    public void ResetRoute()
    {
        // [수정] 0번 SortingOrder로 리셋
        ActivatePartInternal(PathDirection.North, false, false, 0);
        ActivatePartInternal(PathDirection.South, false, false, 0);
        ActivatePartInternal(PathDirection.East, false, false, 0);
        ActivatePartInternal(PathDirection.West, false, false, 0);
    }





    public void ActivatePart(PathDirection dir, bool state, bool isStaminaDepleted, int sortingOrder)
    {
        if (state)
        {
            ActivatePartInternal(dir, !isStaminaDepleted, isStaminaDepleted, sortingOrder);
        }
        else
        {
            ActivatePartInternal(dir, false, false, 0); // 비활성화 시 0
        }
    }

    private void ActivatePartInternal(PathDirection dir, bool blueState, bool redState, int sortingOrder)
    {
        int index = GetIndexFromDirection(dir);
        if (index == -1) return; // 잘못된 방향

        // 파란색 처리
        if (blueRenderers[index] != null)
        {
            blueRenderers[index].gameObject.SetActive(blueState);
            if (blueState) blueRenderers[index].sortingOrder = sortingOrder;
        }
        // 빨간색 처리
        if (redRenderers[index] != null)
        {
            redRenderers[index].gameObject.SetActive(redState);
            if (redState) redRenderers[index].sortingOrder = sortingOrder;
        }
    }

    // [추가] 방향을 배열 인덱스로 변환하는 헬퍼
    private int GetIndexFromDirection(PathDirection dir)
    {
        switch (dir)
        {
            case PathDirection.North: return 0;
            case PathDirection.South: return 1;
            case PathDirection.East:  return 2;
            case PathDirection.West:  return 3;
            default: return -1;
        }
    }
}