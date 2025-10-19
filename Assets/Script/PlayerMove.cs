using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// [수정됨] 스크립트가 스프라이트(View)를 직접 제어하고,
/// Animator는 'UpDown' 또는 'Rotate' 애니메이션만 재생합니다.
/// </summary>
[RequireComponent(typeof(Animator))] // Animator는 여전히 필요
public class PlayerMove : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 5.0f;

    [Header("방향별 스프라이트/프리팹")] // [다시 추가됨]
    public GameObject viewUp;
    public GameObject viewDown;
    public GameObject viewLeft;
    public GameObject viewRight;

    // Animator 참조 (애니메이션 재생용)
    private Animator animator;

    private Coroutine movementCoroutine;

    // [추가] Animator 파라미터 이름 해시 (성능 최적화)
    private readonly int hashIsMoving = Animator.StringToHash("IsMoving");
    private readonly int hashIsHorizontal = Animator.StringToHash("IsHorizontal");

    void Start()
    {
        animator = GetComponent<Animator>();

        // 시작 시 기본 모습 (예: 아래) 설정 및 애니메이션 정지
        UpdateVisuals(Vector3.down, false);
    }

    /// <summary>
    /// RouteManager가 호출 (변경 없음)
    /// </summary>
    public void StartMovement(List<Vector3> worldPath, IDestination destination)
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }
        movementCoroutine = StartCoroutine(MoveAlongPath(worldPath, destination));
    }

    private IEnumerator MoveAlongPath(List<Vector3> path, IDestination destination)
    {
        RouteManager.Instance.SetPlayerMoving(true);

        // [수정] 이동 시작 시 애니메이션 재생
        // UpdateVisuals(direction, true); // 첫 방향은 루프 안에서 설정됨

        if (path.Count <= 1)
        {
            RouteManager.Instance.SetPlayerMoving(false);
            UpdateVisuals(GetLastDirection(), false); // 마지막 방향 유지하며 정지
            yield break;
        }

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 targetPoint = path[i];
            Vector3 direction = Vector3.zero;

            while (Vector3.Distance(transform.position, targetPoint) > 0.01f)
            {
                direction = (targetPoint - transform.position).normalized;

                // [수정] 스프라이트와 애니메이션 모두 업데이트
                UpdateVisuals(direction, true);

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPoint,
                    moveSpeed * Time.deltaTime
                );
                yield return null;
            }
            transform.position = targetPoint;
        }

        // 이동 완료
        RouteManager.Instance.SetPlayerMoving(false);
        movementCoroutine = null;

        // [수정] 마지막 방향의 스프라이트를 보여주며 애니메이션 정지
        UpdateVisuals(GetLastDirection(), false);

        // 이동 완료 후 이벤트 트리거 (변경 없음)
        RouteManager.Instance.OnPlayerMovementFinished(destination);
    }

    /// <summary>
    /// [핵심 수정] 이동 방향과 상태에 따라 스프라이트(View)를 켜고 끄고,
    /// Animator 파라미터를 설정하여 적절한 애니메이션을 재생합니다.
    /// </summary>
    private void UpdateVisuals(Vector3 direction, bool isMoving)
    {
        // 1. 모든 View 비활성화 (기존 로직)
        if (viewUp) viewUp.SetActive(false);
        if (viewDown) viewDown.SetActive(false);
        if (viewLeft) viewLeft.SetActive(false);
        if (viewRight) viewRight.SetActive(false);

        // 2. 주 방향(수평/수직) 결정
        bool isHorizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);

        // 3. 방향에 맞는 View 활성화
        if (isHorizontal) // 좌우 이동
        {
            if (direction.x > 0 && viewRight) viewRight.SetActive(true);
            else if (viewLeft) viewLeft.SetActive(true);
        }
        else // 상하 이동
        {
            if (direction.y > 0 && viewUp) viewUp.SetActive(true);
            else if (viewDown) viewDown.SetActive(true);
        }

        // 4. Animator 파라미터 설정
        animator.SetBool(hashIsMoving, isMoving);
        animator.SetBool(hashIsHorizontal, isHorizontal);
    }

    /// <summary>
    /// [헬퍼] 현재 활성화된 View를 기반으로 마지막 방향 벡터를 추정
    /// </summary>
    private Vector3 GetLastDirection()
    {
        if (viewUp && viewUp.activeSelf) return Vector3.up;
        if (viewDown && viewDown.activeSelf) return Vector3.down;
        if (viewLeft && viewLeft.activeSelf) return Vector3.left;
        if (viewRight && viewRight.activeSelf) return Vector3.right;
        return Vector3.down; // 기본값
    }

    // [삭제됨] SetAnimatorDirection 함수 -> UpdateVisuals로 통합됨
}