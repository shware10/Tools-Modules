using UnityEngine;

public class MeshBand : MeshTemp
{
    [Header("밴드 메쉬 설정")]
    
    [Tooltip("하단 원의 중심 좌표입니다.")]
    [SerializeField] private Vector3 bottomCenter =  Vector3.zero;
    
    [Tooltip("상단 원의 중심 좌표입니다.")]
    [SerializeField] private Vector3 topCenter = new Vector3(0, 5, 0);
    
    [Tooltip("하단 원의 반지름입니다.")]
    float bottomRadius = 3f;
    
    [Tooltip("상단 원의 반지름입니다.")]
    float topRadius = 3f;
    
    [Range(3, 64)]
    [Tooltip("메쉬를 구성할 사각형(쿼드)의 갯수입니다.")]
    int segments = 32;
    
    protected override void OnValidate()
    {
        base.OnValidate();
        MeshBuildCore.BuildBand(mesh, bottomCenter, topCenter, bottomRadius, topRadius, segments);
    }
    
    protected override void Init()
    {
        base.Init();
        MeshBuildCore.BuildBand(mesh, bottomCenter, topCenter, bottomRadius, topRadius, segments);
    }
}