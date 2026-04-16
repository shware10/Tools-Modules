using UnityEngine;

public class MeshCone : MeshTemp
{
    [Header("콘 메쉬 설정")]
    
    [Tooltip("콘의 꼭짓점 위치입니다.")]    
    [SerializeField] private Vector3 apex = Vector3.zero;
    
    [Tooltip("콘의 원 중앙 위치입니다.")]
    [SerializeField] Vector3 baseCenter = new Vector3(0,5,0);
    
    [Tooltip("콘의 원 반지름입니다.")]
    [SerializeField] float radius = 5f;
    
    [Range(3,64)]
    [Tooltip("콘을 구성할 삼각형의 갯수입니다.")]
    [SerializeField] int segments = 32;
    
    protected override void OnValidate()
    {
        base.OnValidate();
        MeshBuildCore.BuildCone(mesh, apex, baseCenter, radius, segments);
    }
    
    protected override void Init()
    {
        base.Init();
        MeshBuildCore.BuildCone(mesh, apex, baseCenter, radius, segments);
    }
}
