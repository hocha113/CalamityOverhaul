using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Fog
{
    /// <summary>
    /// 局部清雾公开 API：圆/矩形区域 + TTL + 边缘羽化，多请求取最小因子。<br/>
    /// 消费者模式=短 TTL + 按帧续订，消费者消失请求自动过期——无注销接口即无泄漏。<br/>
    /// 因子作用在模拟目标值上，压雾/回雾自动继承驱散快、回聚慢的时间不对称。<br/>
    /// 本轮无消费者（场景优先），留给将来的屋内避难所、驱雾灯、演出留白
    /// </summary>
    public static class KiyumeFogSuppression
    {
        private struct Request
        {
            internal bool IsRect;
            internal Vector2 Center;
            internal float Radius;
            internal Rectangle Rect;
            internal float Feather;
            internal uint ExpireTick;
        }

        private static readonly List<Request> requests = new(8);
        private static uint tickNow;
        //防御上限：雾未激活期时钟不走，失控消费者的请求不许无界积压
        private const int MaxRequests = 256;

        /// <summary>
        /// 圆形清雾。<paramref name="worldCenterPx"/> 圆心（世界px），<paramref name="radiusPx"/> 全清半径，
        /// <paramref name="ttlTicks"/> 存活期（消费者按帧续订），<paramref name="featherPx"/> 边缘羽化带宽
        /// </summary>
        public static void RequestCircle(Vector2 worldCenterPx, float radiusPx, int ttlTicks = 12, float featherPx = 200f) {
            if (requests.Count >= MaxRequests) {
                requests.RemoveAt(0);
            }
            requests.Add(new Request {
                IsRect = false,
                Center = worldCenterPx,
                Radius = MathHelper.Max(radiusPx, 0f),
                Feather = MathHelper.Max(featherPx, 1f),
                ExpireTick = tickNow + (uint)Math.Max(ttlTicks, 1)
            });
        }

        /// <summary>矩形清雾（屋内/避难所用）。<paramref name="worldRectPx"/> 世界px矩形</summary>
        public static void RequestRect(Rectangle worldRectPx, int ttlTicks = 12, float featherPx = 200f) {
            if (requests.Count >= MaxRequests) {
                requests.RemoveAt(0);
            }
            requests.Add(new Request {
                IsRect = true,
                Rect = worldRectPx,
                Feather = MathHelper.Max(featherPx, 1f),
                ExpireTick = tickNow + (uint)Math.Max(ttlTicks, 1)
            });
        }

        /// <summary>清空全部请求（世界卸载/演出收尾）</summary>
        public static void Clear() => requests.Clear();

        internal static bool AnyActive => requests.Count > 0;

        internal static int ActiveCount => requests.Count;

        //每 tick 推进时钟并原位清过期（倒序 RemoveAt，零分配）
        internal static void Update() {
            tickNow++;
            for (int i = requests.Count - 1; i >= 0; i--) {
                if (requests[i].ExpireTick <= tickNow) {
                    requests.RemoveAt(i);
                }
            }
        }

        /// <summary>抑制因子：1=无抑制，0=全清；多请求取最小，边缘 smoothstep 羽化</summary>
        internal static float Evaluate(Vector2 worldPx) {
            float factor = 1f;
            for (int i = 0; i < requests.Count; i++) {
                Request req = requests[i];
                //到全清区边界的外距
                float d;
                if (req.IsRect) {
                    float dx = MathHelper.Max(MathHelper.Max(req.Rect.Left - worldPx.X, worldPx.X - req.Rect.Right), 0f);
                    float dy = MathHelper.Max(MathHelper.Max(req.Rect.Top - worldPx.Y, worldPx.Y - req.Rect.Bottom), 0f);
                    d = MathF.Sqrt(dx * dx + dy * dy);
                }
                else {
                    d = MathHelper.Max(Vector2.Distance(worldPx, req.Center) - req.Radius, 0f);
                }
                float t = MathHelper.Clamp(d / req.Feather, 0f, 1f);
                factor = MathHelper.Min(factor, t * t * (3f - 2f * t));
            }
            return factor;
        }
    }
}
