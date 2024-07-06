using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Particles.Core
{
    /// <summary>
    /// 一个粒子的基类，它主要来自于灾厄本体
    /// </summary>
    internal class CWRParticle
    {
        /// <summary>
        /// 由一般粒子处理程序注册的粒子类型的ID,这是在粒子处理器loadsl时自动设置的
        /// </summary>
        public int Type;
        /// <summary>
        /// 这个粒子已经存在的帧数,一般情况下,不需要手动更新它
        /// </summary>
        public int Time;
        /// <summary>
        /// 如果你需要在达到粒子上限的情况下渲染粒子，将此设置为<see langword="true"/>
        /// </summary>
        public virtual bool Important => false;
        /// <summary>
        /// 如果你想让你的粒子在达到其最大寿命时自动移除,将此设置为<see langword="true"/>
        /// </summary>
        public virtual bool SetLifetime => false;
        /// <summary>
        /// 一个粒子可以存活的最大时间,单位为tick,一般如果想让其有效需要先将<see cref="SetLifetime"/>设置为<see langword="true"/>
        /// </summary>
        public int Lifetime = 0;

        public float LifetimeCompletion => Lifetime != 0 ? Time / (float)Lifetime : 0;

        /// <summary>
        /// 一个粒子在世界中的位置，这不是在粒子集的上下文中使用的，因为所有的粒子都是根据它们相对于集合原点的位置来计算的
        /// </summary>
        public Vector2 Position;
        public Vector2 Velocity;

        public Vector2 Origin;
        public Color Color;
        public float Rotation;
        public float Scale;

        public virtual int FrameVariants => 1;
        public int Variant = 0;
        public virtual string Texture => "";
        /// <summary>
        /// 将此设置为true以禁用默认的粒子绘制，因此调用Particle.CustomDraw()
        /// </summary>
        public virtual bool UseCustomDraw => false;
        /// <summary>
        /// 如果想自己处理粒子绘制,请使用此方法。只在UseCustomDraw设置为true时调用
        /// </summary>
        public virtual void CustomDraw(SpriteBatch spriteBatch) { }

        public virtual void CustomDraw(SpriteBatch spriteBatch, Vector2 basePosition) { }
        /// <summary>
        /// 每次更新粒子处理程序时调用。粒子的速度会自动添加到它的位置，它的时间也会自动增加
        /// </summary>
        public virtual void AI() { }

        /// <summary>
        /// 将此设置为true以使你的粒子使用additive blending而不是alpha blend。
        /// </summary>
        public virtual bool UseAdditiveBlend => false;

        /// <summary>
        /// 将此设置为true以使您的粒子与半透明像素一起工作。如果UseAdditiveBlend被设置为true，则被覆盖
        /// </summary>
        public virtual bool UseHalfTransparency => false;

        /// <summary>
        /// 从处理程序中移除粒子
        /// </summary>
        public void Kill() => CWRParticleHandler.RemoveParticle(this);
    }
}
