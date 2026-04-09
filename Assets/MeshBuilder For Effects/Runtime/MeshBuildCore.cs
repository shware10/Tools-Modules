using UnityEngine;

public static class MeshBuildCore
{
    
    /// <summary>
    /// Cone 형태의 메쉬를 만드는 메서드
    /// </summary>
    /// <param name="mesh">오브젝트의 메쉬</param>
    /// <param name="apex">콘의 꼭짓점</param>
    /// <param name="baseCenter">원의 중심</param>
    /// <param name="radius">원의 지름</param>
    /// <param name="segments">면을 나누는 삼각형 갯수</param>
    public static void BuildCone
    (
        Mesh mesh,
        Vector3 apex,
        Vector3 baseCenter,
        float radius,
        int segments
    )
    {
        mesh.Clear();
               
        // 콘으로 향하는 방향 축
        Vector3 axis = (apex - baseCenter).normalized;
        
        // axis랑 직교하는 벡터 찾기 위해 기준 벡터 2개(콘 방향 축, 아무 벡터) 외적
        Vector3 right = Vector3.Cross(axis, Vector3.up);
        
        // up 벡터가 평행하면 forward로 다시시도        
        if(right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(axis, Vector3.forward);
        
        // 단위벡터로 만들기
        right.Normalize();
        
        // right와 axis 두 벡터에 모두 수직인 벡터를 생성
        // 직교 좌표계 완성
        Vector3 forward = Vector3.Cross(right, axis);
        
        // 원 정점 갯수(삼각형 갯수+1) + 꼭짓점
		int vertexCount = 2 + segments;
		// 정점을 담을 배열				 
		Vector3[] vertices = new Vector3[vertexCount];
		// uv 배열
		Vector2[] uv = new Vector2[vertices.Length];
		// 정점 세개의 인덱스로 삼각형을 구성할 배열
		int[] triangles = new int[segments * 3];	
		
		int apexIndex = 0;
		int ringIndex = 1;
		
		vertices[apexIndex] = apex; // 첫 정점은 꼭짓점
		
		uv[apexIndex] = new Vector2(0.5f, 0f); // 꼭짓점을 중앙 바닥으로
		
		// 정점 생성
        for(int i = 0; i <= segments; ++i)
        {
            float t = (float)i / segments;
            float rad = t * Mathf.PI * 2f;
            
            Vector3 dir = right * Mathf.Cos(rad) + forward * Mathf.Sin(rad); 
            
            Vector3 point = baseCenter + dir * radius;
			
            vertices[ringIndex + i] = point;
            // 정점을 uv에 맵핑
            uv[ringIndex + i] = new Vector2(t, 1f);
        }
        
		int trIdx = 0;
		
		// 옆면을 삼각형으로 생성하기
		for(int i = 0; i < segments; ++i)
		{
		    int current = ringIndex + i;
		    int next = ringIndex + i + 1;
		    
		    triangles[trIdx++] = apexIndex;
		    triangles[trIdx++] = current;
		    triangles[trIdx++] = next;
		}
		
		mesh.vertices = vertices;
		mesh.uv = uv;
		mesh.triangles = triangles;
		
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
    }
    
    /// <summary>
    /// Band 형태의 메쉬를 만드는 메서드
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="bottomCenter">하단 원의 중심좌표</param>
    /// <param name="topCenter">상단 원의 중심좌표</param>
    /// <param name="bottomRadius">하단 원의 반지름</param>
    /// <param name="topRadius">상단 원의 반지름</param>
    /// <param name="segments">면을 나누는 사각형(쿼드)의 갯수</param>
    public static void BuildBand
    (
		Mesh mesh,
		Vector3 bottomCenter, 
		Vector3 topCenter,
	    float bottomRadius,
	    float topRadius,
	    int segments
	)
    {
	    mesh.Clear();
	    
	    Vector3 axis = (topCenter - bottomCenter).normalized;
	    // 두 원의 위치 차이가 없으면 up벡터를 y축으로
	    if(axis.sqrMagnitude < 0.0001f) axis = Vector3.up;
	     
	    Vector3 right = Vector3.Cross(axis, Vector3.up);
	    // axis가 up벡터와 평행하면 forward 벡터로 다시 외적
	    if(right.sqrMagnitude < 0.0001f) right = Vector3.Cross(axis, Vector3.forward);
	    
	    right.Normalize();
	    
	    Vector3 forward = Vector3.Cross(right, axis).normalized;
	    
	    // 닫는 정점을 하나 추가 하고 위/아래를 고려한 2배
	    Vector3[] vertices = new Vector3[(segments + 1) * 2];
	    Vector2[] uv = new Vector2[vertices.Length];	    
	    // 정점 찍기
	    for(int i = 0; i <= segments; ++i)
	    {
		    float t = (float)i/segments;
		    float rad = t * Mathf.PI * 2f;
		    
		    Vector3 dir = Mathf.Cos(rad) * right + Mathf.Sin(rad) * forward;
		    
		    Vector3 bottom = bottomCenter + dir * bottomRadius;
		    Vector3 top = topCenter + dir * topRadius;
		    
		    int idx = i * 2;
		    vertices[idx] = bottom;
		    vertices[idx + 1] = top;
		    
		    uv[idx] = new Vector2(t, 0f);
		    uv[idx + 1] = new Vector2(t, 1f);
	    }
	    
	    // 쿼드는 2개의 삼각형 * 삼각형을 이루는 정점 3  
	    int[] triangles = new int[segments * 6];
	    
	    int trIdx = 0;
	    for(int i = 0; i < segments; ++i)
	    {
		    int b0 = i * 2;
		    int t0 = i * 2 + 1;
		    int b1 = (i + 1) * 2;
		    int t1 = (i + 1) * 2 + 1;
		    
		    // 반시계 방향(CCW)으로 그리기 
		    // triangle 1
		    triangles[trIdx++] = b0;
		    triangles[trIdx++] = b1;
		    triangles[trIdx++] = t0;
		    
		    // triangle 2
		    triangles[trIdx++] = t0;
		    triangles[trIdx++] = b1;
		    triangles[trIdx++] = t1;
	    }
	    
	    mesh.vertices = vertices;
	    mesh.uv = uv;
	    mesh.triangles = triangles;
	    
	    mesh.RecalculateNormals();
	    mesh.RecalculateBounds();
    }
    
    /// <summary>
    /// Spiral 형태의 메쉬를 만드는 메서드
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="bottomCenter">하단 원의 중심 좌표</param>
    /// <param name="topCenter">상단 원의 중심 좌표</param>
    /// <param name="bottomRadius">하단 원의 반지름</param>
    /// <param name="topRadius">상단 원의 반지름</param>
    /// <param name="width">Spiral의 메쉬 두께</param>
    /// <param name="turns">Spiral을 구성하는 회전 수</param>
    /// <param name="segmentsPerTurn">회전 당 구성할 사각형(쿼드)의 갯수</param>
    /// <param name="isVertical">메쉬 두께 수직 방향 여부 false = Horizontal</param>
    public static void BuildSpiral
    (
		Mesh mesh,
		Vector3 bottomCenter,
		Vector3 topCenter,
		float bottomRadius,
		float topRadius,
		float width,
		int turns,
		int segmentsPerTurn,
		bool isVertical
    )
    {
		mesh.Clear();
		
		int segments = turns * segmentsPerTurn;
		
		Vector3[] vertices = new Vector3[(segments + 1) * 2];
		Vector2[] uv = new Vector2[vertices.Length];
		int[] triangles = new int[segments * 6];
		
		// 전체 높이축 변화량
		Vector3 axisDelta = topCenter - bottomCenter;
		// 높이축 방향 벡터
		Vector3 axisDir = axisDelta.normalized;
		
		Vector3 right = Vector3.Cross(axisDir, Vector3.up);
		if(right.sqrMagnitude < 0.0001f) right = Vector3.Cross(axisDir, Vector3.forward);
		
		right.Normalize();
		// 직교 좌표계 구성
		Vector3 forward = Vector3.Cross(right, axisDir);
		
		// 정점 그리기
		for(int i = 0; i <= segments; ++i)
		{
			// 전체 스파이럴 진행률
			float percentage = (float)i / segments;
			// 하나의 원을 그리기 위한 라디안 퍼센티지
			float t = percentage * turns * Mathf.PI * 2;
			// 현재 반지름은 top/bottom 두 반지름을 퍼센트만큼 보간한 값
			float radius = Mathf.Lerp(bottomRadius, topRadius, percentage);
			// 현재 높이는 전체 높이를 퍼센트만큼 보간한 값
			Vector3 height = Vector3.Lerp(bottomCenter, topCenter, percentage);
			
			float cos = Mathf.Cos(t);	// x
			float sin = Mathf.Sin(t);	// z
			
			// 원 방향 성분
			Vector3 dir = right * cos + forward * sin;
			
			// 정점의 위치
			Vector3 point = height + dir * radius;
			
			// 넓이 벡터
			Vector3 widthDir;
			
			if(isVertical)	// 수직으로 spiral의 넓이 주기	
			{ widthDir = axisDir; } 	
			else			// 수평으로 spiral의 넓이 주기		
			{
				// 현재 정점의 변화량 = 정점의 위치의 각 방향성분을 미분한 값의 합
				Vector3 tangent = (right * -sin + forward * cos) * radius
				                  + axisDelta / segments;
				// 현재 진행 방향 벡터
				tangent.Normalize();
				widthDir = Vector3.Cross(axisDir, tangent).normalized;
			}
			
			int idx = i * 2;
			float halfwidth = width / 2f;
			// 안쪽(아래쪽) 정점
			vertices[idx] = point - widthDir * halfwidth;
			// 바깥쪽(윗쪽) 정점
			vertices[idx + 1] = point + widthDir * halfwidth;
			
			uv[idx] = new Vector2(0, percentage);
			uv[idx + 1] = new Vector2(1, percentage);		
		}
		
		// 삼각형을 이루는 정점 구성
		int trIdx = 0;
		for(int i = 0; i < segments; ++i)
		{
			int b0 = i * 2;
			int t0 = i * 2 + 1;
			int b1 = (i + 1) * 2;
			int t1 = (i + 1) * 2 + 1;
			
			triangles[trIdx++] = b0;
			triangles[trIdx++] = b1;
			triangles[trIdx++] = t0;
			
			triangles[trIdx++] = t0;
			triangles[trIdx++] = b1;
			triangles[trIdx++] = t1;
		}
		
		mesh.vertices = vertices;
		mesh.uv = uv;
		mesh.triangles = triangles;
		
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();
    }
}