using UnityEngine;

public enum HitBoxType
{
    Line,
    Box,
    Sphere,
}

[CreateAssetMenu(fileName = "AttackData", menuName = "Scriptable Objects/AttackData")]
public class AttackDataScriptableObject : ScriptableObject
{
    [SerializeField] private string key;
    [SerializeField] private Vector3 offset = Vector3.zero;
    [SerializeField] private float distance = 0.0f;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private HitBoxType hitBoxType = HitBoxType.Box;
    [SerializeField, Min(1)] private int maxTargets = 1;
    [SerializeField, Min(1)] private int hitCount = 1;
    [SerializeField, Min(0.01f)] private float hitInterval = 0.1f;

    // 구체
    [SerializeField] private float radius;

    // 박스
    [SerializeField] private Vector3 boxSize = Vector3.one;

    // 라인
    [SerializeField] private float length = 1.0f;



    public string Key => key;
    public Vector3 Offset => offset;
    public float Distance => distance;
    public float Length => length;
    public HitBoxType HitBoxType => hitBoxType;
    public float Radius => radius;
    public Vector3 BoxSize => boxSize;
    public LayerMask LayerMask => layerMask;
    public int MaxTargets => maxTargets;
    public int HitCount => hitCount;
    public float HitInterval => hitInterval;

}
