using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// 육각형 이미지 생성 유틸리티
    /// UI Image에서 사용할 육각형 스프라이트 동적 생성
    /// </summary>
    public class HexagonMeshGenerator : MonoBehaviour
    {
        /// <summary>
        /// 육각형 텍스처 생성
        /// </summary>
        public static Texture2D GenerateHexagonTexture(int size, Color fillColor, Color borderColor, int borderWidth = 2)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            // 배경을 투명하게
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - borderWidth;
            
            // 육각형 꼭지점 계산 (pointy-top)
            Vector2[] vertices = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.PI / 6f + i * Mathf.PI / 3f;
                vertices[i] = center + new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );
            }
            
            // 픽셀별로 육각형 내부인지 확인
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    float distFromCenter = Vector2.Distance(point, center);
                    
                    if (IsPointInHexagon(point, vertices))
                    {
                        // 테두리 영역 확인
                        bool isBorder = false;
                        for (int i = 0; i < 6; i++)
                        {
                            Vector2 v1 = vertices[i];
                            Vector2 v2 = vertices[(i + 1) % 6];
                            float dist = DistanceToLine(point, v1, v2);
                            if (dist < borderWidth)
                            {
                                isBorder = true;
                                break;
                            }
                        }
                        
                        pixels[y * size + x] = isBorder ? borderColor : fillColor;
                    }
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return texture;
        }
        
        /// <summary>
        /// 점이 육각형 내부에 있는지 확인
        /// </summary>
        private static bool IsPointInHexagon(Vector2 point, Vector2[] vertices)
        {
            int intersections = 0;
            
            for (int i = 0; i < 6; i++)
            {
                Vector2 v1 = vertices[i];
                Vector2 v2 = vertices[(i + 1) % 6];
                
                if ((v1.y > point.y) != (v2.y > point.y))
                {
                    float x = (v2.x - v1.x) * (point.y - v1.y) / (v2.y - v1.y) + v1.x;
                    if (point.x < x)
                        intersections++;
                }
            }
            
            return intersections % 2 == 1;
        }
        
        /// <summary>
        /// 점과 선분 사이의 거리
        /// </summary>
        private static float DistanceToLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            Vector2 line = lineEnd - lineStart;
            float length = line.magnitude;
            line.Normalize();
            
            Vector2 toPoint = point - lineStart;
            float projection = Vector2.Dot(toPoint, line);
            projection = Mathf.Clamp(projection, 0, length);
            
            Vector2 closestPoint = lineStart + line * projection;
            return Vector2.Distance(point, closestPoint);
        }
        
        /// <summary>
        /// 육각형 스프라이트 생성
        /// </summary>
        public static Sprite GenerateHexagonSprite(int size, Color fillColor, Color borderColor, int borderWidth = 2)
        {
            Texture2D texture = GenerateHexagonTexture(size, fillColor, borderColor, borderWidth);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
    
    /// <summary>
    /// 파티클 이펙트 생성 유틸리티
    /// </summary>
    public static class ParticleEffectFactory
    {
        /// <summary>
        /// 매칭 이펙트 생성
        /// </summary>
        public static ParticleSystem CreateMatchEffect(Transform parent)
        {
            GameObject particleObj = new GameObject("MatchEffect");
            particleObj.transform.SetParent(parent);
            particleObj.transform.localPosition = Vector3.zero;
            
            ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.5f;
            main.startSpeed = 3f;
            main.startSize = 0.3f;
            main.maxParticles = 20;
            main.playOnAwake = false;
            
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 10, 20)
            });
            
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;
            
            return ps;
        }
        
        /// <summary>
        /// 빅뱅 이펙트 생성
        /// </summary>
        public static ParticleSystem CreateBigBangEffect(Transform parent)
        {
            GameObject particleObj = new GameObject("BigBangEffect");
            particleObj.transform.SetParent(parent);
            particleObj.transform.localPosition = Vector3.zero;
            
            ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.startLifetime = 1.5f;
            main.startSpeed = 10f;
            main.startSize = 0.5f;
            main.maxParticles = 200;
            main.playOnAwake = false;
            
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 100, 200)
            });
            
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.yellow, 0f),
                    new GradientColorKey(Color.red, 0.5f),
                    new GradientColorKey(Color.magenta, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;
            
            return ps;
        }
    }
}
