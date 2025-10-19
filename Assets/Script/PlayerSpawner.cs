using UnityEngine;

public class PlayerSpawner : MonoBehaviour 
{
    [Header("스폰 설정")]
    public GameObject playerPrefab;
    
    [Header("오브젝트 관리")]
    public Transform playerContainer;

    // [수정] 스폰된 플레이어를 다른 스크립트가 참조할 수 있도록 변경
    public GameObject SpawnedPlayer { get; private set; }
    // private GameObject spawnedPlayer; // <- 이 줄은 삭제하거나 주석 처리

    public void SpawnPlayer(Vector3 spawnPosition)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player Prefab이 PlayerSpawner에 할당되지 않았습니다!"); 
            return;
        }

        if (SpawnedPlayer != null) // [수정]
        {
            Destroy(SpawnedPlayer); // [수정]
        }

        // [수정]
        SpawnedPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

        if (playerContainer != null)
        {
            SpawnedPlayer.transform.SetParent(playerContainer); // [수정]
        }

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.CenterOnTarget(SpawnedPlayer.transform); // [수정]
        }
        else
        {
            Debug.LogError("CameraManager.Instance를 찾을 수 없습니다!");
        }
    }
}