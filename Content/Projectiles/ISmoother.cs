using Microsoft.Xna.Framework;
using System;

namespace CalamityOverhaul.Content.Projectiles
{
    public interface ISmoother
    {
        void ReCalculate(int maxTime);

        /// <summary>
        /// 一般返回一个0-1之间的插值
        /// </summary>
        /// <param name="timer"></param>
        /// <param name="maxTime"></param>
        /// <returns></returns>
        float Smoother(int timer, int maxTime);
        float Smoother(float factor);
    }

    public class NoSmoother : ISmoother
    {
        public void ReCalculate(int maxTime) { }

        public float Smoother(int timer, int maxTime) {
            return (float)timer / maxTime;
        }

        /// <summary>
        /// 你觉得这样很有意思吗？
        /// </summary>
        /// <param name="factor"></param>
        /// <returns></returns>
        public float Smoother(float factor) {
            return factor;
        }
    }

    public class BezierEaseSmoother : ISmoother
    {
        public int halfTime;

        public void ReCalculate(int maxTime) {
            halfTime = maxTime / 2;
        }

        public float Smoother(int timer, int maxTime) {
            return CWRUtils.BezierEase((float)timer / maxTime);
        }

        public float Smoother(float factor) {
            return CWRUtils.BezierEase(factor);
        }
    }

    public class HeavySmoother : ISmoother
    {
        public void ReCalculate(int maxTime) { }

        public float Smoother(int timer, int maxTime) {
            float factor = (float)timer / maxTime;
            return CWRUtils.HeavyEase(factor);
        }

        public float Smoother(float factor) {
            return CWRUtils.HeavyEase(factor);
        }
    }

    public class X2Smoother : ISmoother
    {
        public void ReCalculate(int maxTime) { }

        public float Smoother(int timer, int maxTime) {
            float factor = (float)timer / maxTime;
            return factor * factor;
        }

        public float Smoother(float factor) {
            return factor * factor;
        }
    }

    public class SqrtSmoother : ISmoother
    {
        public void ReCalculate(int maxTime) { }

        public float Smoother(int timer, int maxTime) {
            float factor = (float)timer / maxTime;
            return MathF.Sqrt(factor);
        }

        public float Smoother(float factor) {
            return MathF.Sqrt(factor);
        }
    }

    /// <summary>
    /// 从0到1再回到0
    /// </summary>
    public class SinSmoother : ISmoother
    {
        public void ReCalculate(int maxTime) { }

        public float Smoother(int timer, int maxTime) {
            float factor = (float)timer / maxTime;
            return MathF.Sin(factor * MathHelper.Pi);
        }

        public float Smoother(float factor) {
            return MathF.Sin(factor * MathHelper.Pi);
        }
    }

    /// <summary>
    /// 从0快速接近1，之后快速返回0<br></br>
    /// 在0.5的时候到达1
    /// </summary>
    public class ReverseX2Smoother : ISmoother
    {
        public void ReCalculate(int maxTime) { }

        public float Smoother(int timer, int maxTime) {
            float factor = (float)timer / maxTime;
            if (factor < 0.5f)
                return 4 * factor * factor;

            factor--;
            return 4 * factor * factor;
        }

        public float Smoother(float factor) {
            if (factor < 0.5f)
                return 4 * factor * factor;

            factor--;
            return 4 * factor * factor;
        }
    }
}
