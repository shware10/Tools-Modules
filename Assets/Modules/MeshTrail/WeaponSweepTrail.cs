using System.Collections.Generic;
using UnityEngine;

public class WeaponSweepTrail : MonoBehaviour
{
    [Header("Weapon Points")]
    public Transform startPoint;
    public Transform endPoint;
    
    [Header("Attack State")]
    public bool isAttacking;
    
    [Header("Sweep Settings")]
    public float sweepRadius = 0.05f;
    public LayerMask hitMask;
    
    [Header("Trail Settings")]
    public float trailWidth = 0.03f;
    public int maxTrailPoints = 32;
    
    struct Edge
    {
        public Vector3 startPoint;
        public Vector3 endPoint;
    }
    
    private Edge prevEdge;
    private Edge curEdge;
    private bool hasPrev;
    
    private List<Edge> trailEdges = new();
    private HashSet<Collider> hitSet = new();
    
    private Mesh trailMesh;
    
    void Awake()
    {
        // 메쉬를 담을 컨테이너
        trailMesh = new Mesh();
        // 동적 메쉬 마크
        trailMesh.MarkDynamic();
        // 메쉬 필터와 연결
        GetComponent<MeshFilter>().mesh = trailMesh;
    }
    
    void LateUpdate()
    {

        
        if(isAttacking)
        {
            SetEdge(); // curEdge 설정
            if ((curEdge.startPoint - prevEdge.startPoint).sqrMagnitude < 0.0004f)return; // 애니메이션 미세한 떨림 제거
            Sweep();
            RecordTrail();
            BuildTrailMesh();
            prevEdge = curEdge;
        }
    }
    
    /// <summary>
    /// cur Edge를 설정하는 함수
    /// </summary>
    void SetEdge()
    {
        // 충돌 판정을 위한 world 기준 포지션 저장
        curEdge.startPoint = startPoint.position;
        curEdge.endPoint = endPoint.position;
        
        if(!hasPrev) // 첫 선분 처리
        {
            prevEdge = curEdge;
            hasPrev = true;
        }
    }
    
    /// <summary>
    /// 검이 지나간 자리를 CapsuleCast를 통해 충돌 판정을 해주는 함수
    /// </summary>
    void Sweep()
    {
        Vector3 move = curEdge.startPoint - prevEdge.startPoint;
        
        float distance = move.magnitude;
        
        if(distance < 0.0001f) return;
        
        Vector3 dir = move / distance;
        
        //무기의 검끝과 검시작 부분을 높이로 하는 캡슐을 생성해 충돌을 판정한다.
        
        if(Physics.CapsuleCast(
        prevEdge.startPoint,        // 검 시작
        prevEdge.endPoint,          // 검 끝
        sweepRadius,                // 캡슐의 반지름
        dir,                // 움직임 방향
        out RaycastHit hit,         // 충돌체 
        distance,                   // prev -> cur 간 움직이는 거리
        hitMask,
        QueryTriggerInteraction.Ignore)
        )
        {
            if(hitSet.Add(hit.collider))
            {
                Debug.Log($"충돌체 : {hit.collider.name}");
            }
        }
    }
    
    void RecordTrail()
    {
        if (trailEdges.Count > 0) // 부드럽게 보간
        {
            curEdge.startPoint = Vector3.Lerp(prevEdge.startPoint, curEdge.startPoint, 0.5f);
            curEdge.endPoint   = Vector3.Lerp(prevEdge.endPoint,   curEdge.endPoint,   0.5f);
        }

        trailEdges.Add(curEdge);

        if (trailEdges.Count > maxTrailPoints)
            trailEdges.RemoveAt(0);
    }
    
    void BuildTrailMesh()
    {
        int edgeCount = trailEdges.Count;
        
        // 선분 카운터가 1개 이하면 tri 형성 불가
        if(edgeCount < 2)
        {
            trailMesh.Clear();
            return;
        }
        
        int vCount = edgeCount * 2;                     // start/end 두개의 정점
        Vector3[] vertices = new Vector3[vCount];       // 정점 등록
        Vector2[] uvs = new Vector2[vCount];            // uv 리스트
        int[] triangles = new int[(edgeCount-1) * 6];   // 삼각형을 그리는 정점 리스트 
        
        for(int i = 0; i < edgeCount; ++i)
        {
            Edge curEdge = trailEdges[i];
            
            Vector3 bladeEdge = curEdge.endPoint - curEdge.startPoint;
            Vector3 bladeDir = bladeEdge.normalized;
            
            float t = (float)i / (edgeCount-1);                             // 프레임이 지나갈수록 1 -> 0

            Vector3 endLocal = transform.InverseTransformPoint(curEdge.endPoint);
            Vector3 startLocal = transform.InverseTransformPoint(curEdge.startPoint);
            
            int v = i*2;
            // Edge 마다 두께 부여
            vertices[v]     = endLocal; // 칼끝
            vertices[v + 1] = Vector3.Lerp(endLocal,startLocal, t);
            
            uvs[v]      = new Vector2(1-t, 1);
            uvs[v+1]    = new Vector2(1-t, 0);
            
            // tris 생성을 위한 리스트 등록
            if(i < edgeCount-1)
            {
                int ti = i * 6;
                triangles[ti + 0] = v;
                triangles[ti + 1] = v + 2;
                triangles[ti + 2] = v + 1;
                triangles[ti + 3] = v + 1;
                triangles[ti + 4] = v + 2;
                triangles[ti + 5] = v + 3;
            }
        }
        
        trailMesh.Clear();
        trailMesh.vertices  = vertices;
        trailMesh.triangles = triangles;
        trailMesh.uv        = uvs;
        trailMesh.RecalculateBounds();
    }
    
    public void ResetAttack()
    {
        hasPrev = false;
        trailEdges.Clear();
        hitSet.Clear();
        trailMesh.Clear();
    }
}
