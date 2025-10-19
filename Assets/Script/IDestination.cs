using UnityEngine;

/// <summary>
/// 'GO' 버튼의 목적지가 될 수 있는 모든 오브젝트(휴게소 등)가
/// 구현해야 하는 인터페이스입니다.
/// </summary>
public interface IDestination
{
    /// <summary>
    /// 루트가 이 목적지에 닿았을 때, 계속 이어서 그리는 것을 허용할지 여부
    /// (Prereq 3. - false면 루트 그리기가 멈춤)
    /// </summary>
    bool AllowRoutePassThrough { get; }

    /// <summary>
    /// 이 목적지 영역의 RectInt (콜라이더 영역)
    /// </summary>
    RectInt GetArea();
}