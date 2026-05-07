using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshTemp : MonoBehaviour
{
    protected MeshFilter meshFilter;
    protected Mesh mesh;
    
    protected virtual void Awake()
    {
        Init();
    }
    protected virtual void OnValidate()
    {
        if (!Application.isPlaying) Init();
    }
    
    protected virtual void Init()
    {
        if(meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        
        // 임시 메쉬를 생성
        if(mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Temporary Mesh";
            meshFilter.sharedMesh = mesh;
        }
    }        
}