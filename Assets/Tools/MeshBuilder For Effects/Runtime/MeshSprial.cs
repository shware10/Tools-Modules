using UnityEngine;
public class MeshSpiral : MeshTemp
{
    [Header("스파이럴 메쉬 설정")]
    
    [Tooltip("하단 원의 중심 좌표입니다.")]
    [SerializeField] private Vector3 bottomCenter = Vector3.zero;
    
    [Tooltip("상단 원의 중심 좌표입니다.")]
    [SerializeField] private Vector3 topCenter = new Vector3(0, 5, 0);
    
    [Tooltip("하단 원의 반지름입니다.")]
    [SerializeField] private float bottomRadius = 2f;
    
    [Tooltip("상단 원의 반지름입니다.")]
    [SerializeField] private float topRadius = 2f;
    
    [Tooltip("메쉬의 두께입니다.")]
    [SerializeField] private float width = 0.5f;
    
    [Tooltip("스파이럴을 구성할 회전 수 입니다.")]
    [SerializeField] private int turns = 3;
    
    [Range(8, 64)]
    [Tooltip("회전 당 구성할 사각형(쿼드)의 갯수입니다.")]
    [SerializeField] private int segmentsPerTurn = 32;
    
    [Tooltip("메쉬 두께의 수직 여부입니다. false = Horizontal")]
    [SerializeField] private bool isVertical = true;
    
    protected override void OnValidate()
    {
        base.OnValidate();
        MeshBuildCore.BuildSpiral(mesh, bottomCenter, topCenter, bottomRadius, topRadius, width, turns, segmentsPerTurn, isVertical);
    }
    protected override void Init()
    {
        base.Init();
        MeshBuildCore.BuildSpiral(mesh, bottomCenter, topCenter, bottomRadius, topRadius, width, turns, segmentsPerTurn, isVertical);
    }
}