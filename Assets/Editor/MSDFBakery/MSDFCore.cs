using System.Collections.Generic;
using System.Linq;
using NUnit.Framework.Internal.Commands;
using UnityEngine;

public static class MSDFCore
{
    /// <summary>
    ///  MSDF 텍스쳐를 베이크 합니다.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="threshold"></param>
    /// <param name="maxDistance"></param>
    /// <param name="tileSize"></param>
    /// <param name="stichEps"> 양자화할 값 </param>
    /// <returns></returns>
    public static Texture2D BakeMSDF(
        Texture2D source,
        float threshold = 0.5f,
        float maxDistance = 16f,
        int tileSize = 16,
        float stitchEps = 0.01f)
    {
        Debug.Log("BakeMSDF called");
        
        int width = source.width;
        int height = source.height;

        // Step1 픽셀별 알파를 읽습니다.
        float[] alpha = GetAlphaArray(source);
        
        // Step2 알파를 값을 바탕으로 매칭스퀘어를 활용해 보간해 연속 선분을 추출합니다.
        List<Segment> segments = MarchingSquaresSegments(alpha, width, height, threshold, stitchEps);
        
        // Step3 선분에 그릴 순서 부여해 전체 윤곽선 추출합니다.
        List<List<Vector2>> contours = StitchContours(segments, stitchEps);

        // Step4 선분에 색상을 부여합니다.
        List<ColoredSegment> coloredSegments = BuildColoredSegments(contours);
        
        // Step5 텍스쳐의 타일을 설정하고 해당 타일에 포함되는 선분을 리스트에 추가해 픽셀의 선분탐색 시간을 줄여줍니다.
        var grid = BuildTileGrid(
            width,
            height,
            tileSize,
            coloredSegments,
            maxDistance
        );
        // 채널별로 가장 가까운 거리를 구해 픽셀마다 거리값 등록합니다.
        ComputeChannelDistances(
            width,
            height,
            coloredSegments,
            grid,
            maxDistance,
            out var rField,
            out var gField,
            out var bField
        );
        
        // Step 6 임계값을 기준으로 부호를 부여합니다.
        ApplySignToDistanceFields(
            width,
            height,
            rField,
            gField,
            bField,
            alpha,
            threshold
        );
#if UNITY_EDITOR
        MSDFColoredDrawer.SetSegments(coloredSegments);
#endif

        
        // Step 7 생성된 픽셀 컬러에 거리 채널을 담아 픽셀 다시 그리기
        return EncodeMSDFTexture(
            width,
            height,
            rField,
            gField,
            bField,
            maxDistance
        );
    }
    
    #region Step 1 Reading Alpha
    
    /// <summary>
    /// 텍스쳐의 각 픽셀의 알파값을 읽어 float[]으로 반환하는 함수
    /// </summary>
    /// <param name="texture"></param>
    /// <returns></returns>
    public static float[] GetAlphaArray(Texture2D texture)
    {
        //텍스처의 모든 픽셀을 바이트(0~255) 기반 구조체로 읽기
        Color32[] px = texture.GetPixels32(); //GetPixels32() faster than GetPixels()
        var a = new float[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            a[i] = px[i].a / 255f;
        }
        return a;
    }

    #endregion
    
    #region Step2 Get Contour with Marching Square
    /// <summary>
    /// 0: bottom (bl→br)
    /// 1: right (br→tr)
    /// 2: top (tl→tr)
    /// 3: left (bl→tl)
    /// </summary>
    private static readonly int[][] MS_TABLE = new int[16][]
    {
        null,        new[]{3,0},  new[]{0,1},  new[]{3,1},
        new[]{1,2},  null,        new[]{0,2},  new[]{3,2},
        new[]{2,3},  new[]{2,0},  null,        new[]{1,2},
        new[]{1,3},  new[]{0,1},  new[]{0,3},  null
    };
   
    // 선분
    public struct Segment       // 선분 정보를 담을 구조체를 선언해줍니다.
    {
        public Vector2 a, b;
        public Segment(Vector2 a, Vector2 b)
        {
            this.a = a; this.b = b;
        }
    }
    
    /// <summary>
    /// MS_Table을 통해 윤곽선을 추출해내는 함수
    /// </summary>
    /// <param name="alpha"></param>
    /// <param name="w"></param>
    /// <param name="h"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    private static List<Segment> MarchingSquaresSegments(float[] alpha, int w, int h, float threshold ,float stitchEps)
    {
        List<Segment> segs = new List<Segment>(w * h);  // 반환할 선분리스트 입니다.
        
        float A(int x, int y) => alpha[y * w + x];      // x, y 좌표로 1차원 리스트에 접근하는 함수입니다.
		    
        for (int y = 0; y < h - 1; ++y)
        {
            for (int x = 0; x < w - 1; ++x)
            {
                // 각 셀 격자점 알파를 추출합니다.
                float blA = A(x, y);
                float brA = A(x + 1, y);
                float tlA = A(x, y + 1);
                float trA = A(x + 1, y + 1);

                //격자 점이 임계값을 넘었는지 확인합니다.
                bool bl = blA >= threshold;
                bool br = brA >= threshold;
                bool tl = tlA >= threshold;
                bool tr = trA >= threshold;

                // 넘은 점만 마스킹합니다.
                int mask = 0;
                if (bl) mask |= 1;
                if (br) mask |= 2;
                if (tr) mask |= 4;
                if (tl) mask |= 8;

                // 경계선 없는 경우에는 처리하지 않습니다.
                if (mask == 0 || mask == 15) continue;

                // 임계값을 넘는 경계점을 잇기 위해 보간 룰을 가져옵니다
                int[] pairs = MS_TABLE[mask];
						    
			    // MS를 보면 5, 10의 경우 두개의 선분이 그어짐을 알 수 있고, 가운데가 비어있는 경우와 차 있는 경우로 나뉩니다.
                if (mask == 5 || mask == 10) 
                {
		                // 셀의 중간 알파 값을 구해 중간이 비었는지 차 있는지 판별합니다.
                    float center = (blA + brA + trA + tlA) * 0.25f;
                    bool centerInside = center >= threshold;
                    								    
				    // 5,10 각각 break contour와 join contour두가지 경우가 있을 수 있습니다.
                    if (mask == 5)
                    {
                        pairs = centerInside ? new[] { 3, 2, 0, 1 } : new[] { 3, 0, 2, 1 }; 
                    }
                    else //10
                    {
                        pairs = centerInside ? new[] { 1, 2, 0, 3 } : new[] { 1, 0, 2, 3 };
                    }
                }
						    
			    // 기준 셀의 위치 입니다.
                Vector2 cell = new Vector2(x, y);

                // 보간을 통해 셀의 각 선분에서 임계값이 되는 정점를 가져오는 함수입니다.
                Vector2 EdgePoint(int e)
                {
                    switch (e)
                    {
                        case 0:
                            return cell + Interpolate(new Vector2(0, 0), new Vector2(1, 0), blA, brA, threshold);
                        case 1:
                            return cell + Interpolate(new Vector2(1, 0), new Vector2(1, 1), brA, trA, threshold);
                        case 2:
                            return cell + Interpolate(new Vector2(1, 1), new Vector2(0, 1), trA, tlA, threshold);
                        case 3:
                            return cell + Interpolate(new Vector2(0, 0), new Vector2(0, 1), blA, tlA, threshold);
                        default: return cell;
                    }
                }
						    
			    //5, 10번의 경우도 처리해야 하기 때문에 반복문을 돌려줍니다.
                for (int i = 0; i < pairs.Length; i += 2)
                {
                    // 정점을 보간해 줍니다.
                    Vector2 a = EdgePoint(pairs[i]);      
                    Vector2 b = EdgePoint(pairs[i + 1]);
								    
					// 생성된 실수 정점이 충분히 가까운 경우에는 epsilon를 통해 Snap처리해서 정점을 위치시킵니다.
                    a = Snap(a, stitchEps);
                    b = Snap(b, stitchEps);
								    
					// 정점을 잇는 선분을 리스트에 추가해 주는데 양 방향을 둘다 등록해 줍니다.
                    segs.Add(new Segment(a, b)); 
                }
            }
        }
        
        Debug.Log($"seg Count : {segs.Count}");
        return segs;
    }

    // eps를 통해 양자화 ex) 1.234 / 0.01 = 123.4 Round → 123 * 0.01 = 1.23
    static Vector2 Snap(Vector2 v, float eps)
    {
        return new Vector2(
            Mathf.Round(v.x / eps) * eps,
            Mathf.Round(v.y / eps) * eps
        );
    }

    /// <summary>
    /// 두 꼭짓점의 알파값 보간 비율을 바탕으로 꼭짓점의 위치를 보간해 줍니다.
    /// </summary>
    /// <param name="p0"></param>
    /// <param name="p1"></param>
    /// <param name="a0"></param>
    /// <param name="a1"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    private static Vector2 Interpolate(Vector2 p0, Vector2 p1, float a0, float a1, float threshold)
    {
        float t = ((Mathf.Abs(a1 - a0)) < 1e-6f) ? 0.5f : (threshold - a0) / (a1 - a0);
        t = Mathf.Clamp01(t);
        return Vector2.Lerp(p0, p1, t);
    }
    
    #endregion
    
    #region Step3 StitchContours
    /// <summary>
    /// MS를 통해 생성한 선분을 이어줍니다.
    /// </summary>
    /// <param name="segments"></param>
    /// <param name="eps"></param>
    /// <returns></returns>
    private static List<List<Vector2>> StitchContours(List<Segment> segments, float eps)
    {
        long Key(Vector2 v)
        {
            // 양자화
            int qx = Mathf.RoundToInt(v.x / eps);
            int qy = Mathf.RoundToInt(v.y / eps);
            return ((long)qx << 32) ^ (uint)qy;  // 하나의 롱 값으로 키 만드는 테크닉입니다.
        }
        
        // 각 정점과 연결된 선분들을 저장
        Dictionary<long, List<int>> pointToSegments
                = new Dictionary<long, List<int>>();

        for (int i = 0; i < segments.Count; ++i)
        {
            long ka = Key(segments[i].a); // 시작점
            long kb = Key(segments[i].b); // 끝점
            
            // 아직 등록되지 않은 선분의 각 정점별로 리스트를 생성해 이어지는 선분들을 추가해줍니다.
            if (!pointToSegments.TryGetValue(ka, out var la))
            {
                la = new List<int>();
                pointToSegments.Add(ka, la);
            }
            if (!pointToSegments.TryGetValue(kb, out var lb))
            {
                lb = new List<int>();
                pointToSegments.Add(kb, lb);
            }
				    
			// 선분의 시작점이나 끝점이 등록된 정점과 매칭된다면 해당 정점의 리스트에 현재 선분의 인덱스를 저장해 줍니다.
            la.Add(i); 
            lb.Add(i); 
        }
        
        // 그릴 윤곽선을 선언해 줍니다. 리스트 안에 리스트인 이유는 여러 덩어리의 이미지 일 수 있기 때문입니다.
        List<List<Vector2>> contours = new List<List<Vector2>>();
        
        // 이미 그린 선분이면 중복 체크를 통해 처리해줍니다.
        bool[] used = new bool[segments.Count];
        
        for (int i = 0; i < segments.Count; ++i)
        {
            //이미 그린 선분이면 뛰어 넘어 줍니다.
            if (used[i]) continue; 
            
            used[i] = true;
            Segment s = segments[i];
            Vector2 start = s.a;
            // 선분의 끝이 현재 위치가 됩니다.
            Vector2 cur = s.b; 
            
            // 윤곽선을 이룰 선분 집합인 폴리라인 입니다.
            List<Vector2> poly = new List<Vector2> { start, cur };

            while (true) // 정점이 이어지는 한 계속 루프를 돌아 줍니다.
            {
                // 현재 정점 키 생성
                long k = Key(cur);
                // 정점에 연결된 선분이 없으면 탐색 종료
                
                // 현재 정점에서 다음 정점으로 이을 수 없다면 닫힌 형태의 이미지를 구성할 수 없습니다.
                if (!pointToSegments.TryGetValue(k, out var candidates)) break; 
                
                
                // 다음 선분 인덱스를 저장할 변수입니다.
                int nextIndex = -1;
                // 선분 방향을 뒤집을지 말지를 확인할 변수입니다.
                bool reverse = false;
                // 가장 완만히 꺾이는 각도를 구하기 위한 변수입니다.
                float bestAngle = float.MaxValue;
						    
				// 직전 선분의 방향입니다.
                Vector2 prevDir = (cur - poly[poly.Count - 2]).normalized;
						    
				// 현재 정점과 이어진 선분들을 확인합니다.
                foreach (int si in candidates)
                {
                    if (used[si]) continue;
								    
					// 후보 선분을 선택합니다.
                    Segment cand = segments[si];
								    
					// 현재 정점이 후보 선분의 시작 정점과 이어지는지 끝 정점과 이어지는지 확인합니다.
                    bool matchA = Key(cand.a) == k;
                    bool matchB = Key(cand.b) == k;
                    // 둘다 이어지지 않는다면 넘어갑니다.
                    if (!matchA && !matchB) continue; 
                    
					// 만약 현재 정점이 a와 이어진다면 다음 정점은 b가 됩니다.
                    Vector2 next = matchA ? cand.b : cand.a;
                    // 현재 정점과 다음 정점의 방향 벡터를 구합니다.
                    Vector2 dir = (next - cur).normalized;
					// 직전 선분의 방향벡터와 현재의 방향 벡터 사이의 각도를 구합니다.
                    float angle = Vector2.Angle(prevDir, dir);
								    
					// 최적의 각도를 갱신합니다.		
                    if (angle < bestAngle)
                    {
		                // 최적 각도라면 다음 선분 인덱스로 현재 탐색중인 선분의 인덱스로 설정합니다.
                        bestAngle = angle;
                        nextIndex = si;
                        // a -> b 로 방향으로 흐른다면 정방향이고 반대라면 역방향입니다.
                        reverse = matchA ? false : true;
                    }
                }
                if (nextIndex < 0) // 다음 선분 인덱스를 찾지 못했다면 윤곽선을 그릴 수 없습니다.
                {
                    Debug.Log("Contour break: no next segment found");
                    break;
                }
                
                used[nextIndex] = true;
                // 다음 선분 선택하고 사용처리 해줍니다.
                Segment ns = segments[nextIndex];

                // 선분 벡터 내적을 통해 각도를 알아햐 하니 방향을 통일해줘야 합니다.
                // 뒤집힘 여부를 알고 있으니 그에 따라 정점을 선택해 줍니다.
                Vector2 nextP = reverse ? ns.a : ns.b;
                
                // 폴리라인에 다음 정점을 추가해줍니다.
                poly.Add(nextP);
                // 현재 정점을 다음 정점으로 설정합니다.
                cur = nextP;
                
                // 현재 정점이 시작 정점이되었으면 
                if ((cur - start).sqrMagnitude <= eps * eps) 
                {
	                  // 마지막 정점을 시작 정점으로 해줍니다.
                    poly[poly.Count - 1] = start;
                    // 윤곽선이 닫혔으므로 탐색을 종료합니다.
                    break;
                }
            }
            // 윤곽선을 구성하는 정점 갯수가 3개보다 작으면 선밖에 형성하지 못하니 3개 이상일때만 닫힌 윤곽선으로 판단합니다.
            if (poly.Count >= 3) 
            {
                contours.Add(poly);
            }
        }
        
        // 구현된 최종 윤곽선 리턴을 리턴합니다.
        return contours;
    }
    
    #endregion
    
    #region Step4 Edge Coloring
    
    // 선분에 칠할 RGB 값입니다.
    public enum EdgeColor {R, G, B}

    // 그냥 선분에서 색상, aabb, normal을 추가한 새로운 데이터 구조입니다.
    public struct ColoredSegment
    {
        public Segment seg;
        public EdgeColor color;
        public Rect aabb;      // 바운딩 박스를 설정해 줍니다. 바운딩 박스는 점-선분 거리 계산 전 
    }
    
    private static List<ColoredSegment> BuildColoredSegments(List<List<Vector2>> contours)
    {
        // 채색 선분 리스트를 생성합니다.
        List<ColoredSegment> result = new List<ColoredSegment>();
		    
		// 전체 윤곽선을 구성하는 닫힌 윤곽선들을 순회합니다.
        foreach (var poly in contours)
        {
		    // shoelace formula의 변형식을 활용해 ccw인지 cw인지를 확인하고 ccw로 통일해줍니다.
            if (ClockWise(poly) > 0) poly.Reverse();
            
            // 마지막 정점은 시작 정점과 같으니 제외해 줍니다.
            int count = poly.Count - 1;
			// 선분에 칠할 색상을 정해 줍니다. R -> G -> B 순서로 진행합니다.
            EdgeColor color = EdgeColor.R;

            for (int i = 0; i < count; ++i)
            {
		        // 윤곽선을 순회하는 정점리스트를 순회합니다.
                Vector2 a = poly[i];
                Vector2 b = poly[i + 1];
                
				// ColoredSegment를 구성해줍니다.
                result.Add(new ColoredSegment
                {
                    seg = new Segment(a, b),
                    color = color,
                    // 선분을 감싸는 최소 사각형을 구합니다.
                    aabb = ComputeAABB(new Segment(a, b)),
                });
						    
                // 색상을 바꿔줍니다.
                color = NextColor(color);
            }
        }
        return result;
    } 

    // shoelace formula의 변형입니다.
    // 전개 해보면 shoelace formula * -1임을 알 수 있습니다.
    // a < 0 = ccw / a > 0 = cw 부호를 통해 방향 판단만 활용합니다.
    private static float ClockWise(List<Vector2> p)
    {
        float a = 0;
        for (int i = 0; i < p.Count - 1; i++)
            a += (p[i+1].x - p[i].x) * (p[i+1].y + p[i].y);
        return a;
    }

    private static EdgeColor NextColor(EdgeColor c)
    {
        return (EdgeColor)(((int)c + 1) % 3);
    }

    private static Rect ComputeAABB(Segment s)
    {
        float minX = Mathf.Min(s.a.x, s.b.x);
        float minY = Mathf.Min(s.a.y, s.b.y);
        float maxX = Mathf.Max(s.a.x, s.b.x);
        float maxY = Mathf.Max(s.a.y, s.b.y);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }
    #endregion 
    
    #region Step5 Per-Pixel Channel Distance Computation

    // 픽셀 -> 근처 선분 후보를 빠르게 찾도록 돕는 클래스입니다.
    private class TileGrid
    {
        public readonly int tileSize;
        public readonly int tilesX;
        public readonly int tilesY;
        public readonly List<int>[] tiles;

        public TileGrid(int w, int h, int tileSize)
        {
    		// 여기서는 3x3 타일로 텍스쳐를 나눌 것 입니다.
            this.tileSize = tileSize;
            
            // 남는 부분도 ceil을 통해 타일로 포함시킵니다.
            tilesX = (w + tileSize - 1) / tileSize;
            tilesY = (h + tileSize - 1) / tileSize;
				    
			// 타일의 갯수만큼 선언해줍니다.
            tiles = new List<int>[tilesX * tilesY];
            for (int i = 0; i < tiles.Length; ++i)
            {
                // 타일마다 가질 선분 인덱스 리스트입니다.
                tiles[i] = new List<int>(32);
            }
        }
        
        // x,y 값으로 1차원 타일 배열에 접근하는 유틸입니다.
        public int GetTileIndexByTile(int tx, int ty) => ty * tilesX + tx;
        
        // 픽셀의 x,y 값으로 자기가 속한 타일에 접근하는 유틸입니다.
        public void PixelToTile(int px, int py, out int tx, out int ty)
        {
            tx = px / tileSize;
            ty = py / tileSize;
        }
    }

    // 각 타일에 속할 선분들을 찾아 리스트에 넣어줍니다.
    private static TileGrid BuildTileGrid(
        int width,
        int height,
        int tileSize,
        List<ColoredSegment> segments,
        float maxDistance)
    {
        TileGrid grid = new TileGrid(width, height, tileSize);

        for (int i = 0; i < segments.Count; ++i)
        {
            // 선분의 바운딩 박스로 부터 유효거리(maxDistance)보다 안쪽에 있어야만 해당 선분과의 거리를 측정할 픽셀입니다.
            Rect aabb = segments[i].aabb;
            aabb.xMin -= maxDistance;
            aabb.yMin -= maxDistance;
            aabb.xMax += maxDistance;
            aabb.yMax += maxDistance;
				    
			// 선분을 포함할 수 있는 타일들의 범위를 지정합니다.
            int minTx = Mathf.Clamp(Mathf.FloorToInt(aabb.xMin / tileSize), 0, grid.tilesX - 1);
            int maxTx = Mathf.Clamp(Mathf.FloorToInt(aabb.xMax / tileSize), 0, grid.tilesX - 1);
            int minTy = Mathf.Clamp(Mathf.FloorToInt(aabb.yMin / tileSize), 0, grid.tilesY - 1);
            int maxTy = Mathf.Clamp(Mathf.FloorToInt(aabb.yMax / tileSize), 0, grid.tilesY - 1);
				    
			// 범위 내의 타일들에 선분을 등록해 줍니다.
            for (int ty = minTy; ty <= maxTy; ++ty)
            {
		        for (int tx = minTx; tx <= maxTx; ++tx)
		        {
		            int tileIndex = grid.GetTileIndexByTile(tx, ty);
		            grid.tiles[tileIndex].Add(i);
		        }
            }
        }
        
        return grid;
    }

    // 각 픽셀에 대해서 R,G,B 채널 선분들 중 가장 가까운 거리를 계산해 저장합니다.
    private static void ComputeChannelDistances(
        int width,
        int height,
        List<ColoredSegment> segments,
        TileGrid grid,
        float maxDistance,
        out float[] rField,
        out float[] gField,
        out float[] bField)
    {
	    // 모든 픽셀은 r,g,b 필드 값을 가집니다.
        int count = width * height;
        rField = new float[count];
        gField = new float[count];
        bField = new float[count];
		    
		// 각 픽셀의 채널별로 최소 dist를 찾기 위해 maxDistance로 초기화 해줍니다.
        for (int i = 0; i < count; i++)
            rField[i] = gField[i] = bField[i] = maxDistance;
		    
		// 루트 계산 비용을 줄이기 위해 거리 판단은 square로 해줍니다.
        float maxDistSq = maxDistance * maxDistance;
        
		// 모든 픽셀을 순회합니다.	
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                // 거리 판단은 픽셀의 중심으로 해줍니다.
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                // 채널별 최소 거리를 갱신할 변수입니다.
                float bestR = maxDistance;
                float bestG = maxDistance;
                float bestB = maxDistance;
						    
				// 현재 픽셀이 속한 타일 위치를 가져옵니다.
                grid.PixelToTile(x, y, out int tx, out int ty);

                // 현재 픽셀이 속한 타일을 줌심으로 3x3 타일을 탐색해 줍니다. 
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nty = ty + dy;
                    if ((uint)nty >= (uint)grid.tilesY) continue;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int ntx = tx + dx;
                        if ((uint)ntx >= (uint)grid.tilesX) continue;

                        int tileIndex = grid.GetTileIndexByTile(ntx, nty);
                        List<int> tileList = grid.tiles[tileIndex];

                        for (int li = 0; li < tileList.Count; li++)
                        {
                            int si = tileList[li];
                            var cs = segments[si];

                            // 미리 구해둔 sqr를 통해 AABB 사전 컷을 해줍니다.
                            if (DistancePointToAABBSqr(p, cs.aabb) > maxDistSq)
                                continue;

                            // 선분의 정사영점과 픽셀의 중심 거리를 측정합니다.
                            float d = DistancePointToSegmentSqr(p, cs.seg);
                            if (d > maxDistSq) continue;
												    
							// maxDistance 안에 값만 루트연산을 해줍니다.
                            float dist = Mathf.Sqrt(d);

                            // 현재 선분의 색상에 맞게 채널별 최소거리 갱신해줍니다.
                            switch (cs.color)
                            {
                                case EdgeColor.R: if (dist < bestR) bestR = dist; break;
                                case EdgeColor.G: if (dist < bestG) bestG = dist; break;
                                case EdgeColor.B: if (dist < bestB) bestB = dist; break;
                            }
                        }
                    }
                }

                rField[idx] = bestR;
                gField[idx] = bestG;
                bField[idx] = bestB;
            }
        }
    }

    // 픽셀의 중심점 p 와 선분의 aabb 까지의 거리를 리턴합니다.
    private static float DistancePointToAABBSqr(Vector2 p, Rect aabb)
    {
        float dx = 0f;
        if (p.x < aabb.xMin) dx = aabb.xMin - p.x;
        else if (p.x > aabb.xMax) dx = p.x - aabb.xMax;

        float dy = 0f;
        if (p.y < aabb.yMin) dy = aabb.yMin - p.y;
        else if (p.y > aabb.yMax) dy = p.y - aabb.yMax;

        return dx * dx + dy * dy;
    }


    private static float DistancePointToSegmentSqr(Vector2 p, Segment seg)
    {
        Vector2 a = seg.a;
        Vector2 b = seg.b;

        Vector2 ab = b - a;
        Vector2 ap = p - a;
		    

        float abLenSq = Vector2.Dot(ab, ab);
        //선분의 길이가 너무 작으면 정사영점 찾지말고 a까지의 거리로 처리합니다.
        if (abLenSq < 1e-8f)
            return (p - a).sqrMagnitude;
		    
		// 선분 a-b에서 정사영점의 상대 위치를 찾습니다.
        float t = Vector2.Dot(ap, ab) / abLenSq;
        t = Mathf.Clamp01(t);
		    
		// 정사영점의 위치를 구하고 픽셀 중심과의 거리를 리턴합니다.
        Vector2 c = a + t * ab;
        return (p - c).sqrMagnitude;
    }

#endregion
    
    #region Step 6 Signed Distance
    
    // inside outside 부호 적용하기
    private static void ApplySignToDistanceFields(
        int width,
        int height,
        float[] rField,
        float[] gField,
        float[] bField,
        float[] alphaField,
        float threshold)
    {
        int pixelCount = width * height;

        for (int i = 0; i < pixelCount; ++i)
        {
            // 각 픽셀의 알파값을 확인합니다.
            float alpha = alphaField[i];
            // 임계값을 넘으면 (원하는 만큼 불투명하면) 안쪽으로 판정합니다.
            bool inside = alpha >= threshold;
            // 부호 처리해 줍니다.
            float sign = inside ? -1 : 1;
            rField[i] *= sign;
            gField[i] *= sign;
            bField[i] *= sign;
        }
    }
    
    #endregion
    
    #region Step 7 Encode Texture
    private static Texture2D EncodeMSDFTexture(
        int width,
        int height,
        float[] rField,
        float[] gField,
        float[] bField,
        float maxDistance)
    {
    
        // 입력 텍스쳐와 같은크기의 텍스쳐를 생성합니다.
        Texture2D tex = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false,
            true
        );
		
        // 픽셀을 r,g,b 필드를 저장하기 위한 컬러 픽셀로 만들어줍니다. 
        Color[] pixels = new Color[width * height];

        for (int i = 0; i < pixels.Length; ++i)
        {
            // r,g,b 필드에 저장하는데 0-1값으로 정규화 해줍니다.
            float rn = ((rField[i] / maxDistance * 0.5f) + 0.5f);
            float gn = ((gField[i] / maxDistance * 0.5f) + 0.5f);
            float bn = ((bField[i] / maxDistance * 0.5f) + 0.5f);
            pixels[i] = new Color(rn, gn, bn, 1f);
        }
    
        // 정규화 필드 값을 바탕으로 컬러 픽셀을 적용합니다.
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    #endregion
}
