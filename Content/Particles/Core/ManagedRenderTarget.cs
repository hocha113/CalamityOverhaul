using CalamityMod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Particles.Core
{
    internal class ManagedRenderTarget : IDisposable
    {
        private RenderTarget2D target;

        public readonly RenderTargetCreationCondition CreationCondition;

        public readonly bool ShouldResetUponScreenResize;

        public readonly bool ShouldAutoDispose;

        public bool WaitingForFirstInitialization {
            get;
            private set;
        } = true;

        public bool IsUninitialized => target is null || target.IsDisposed;

        public bool IsDisposed {
            get;
            private set;
        }

        /// <summary>
        /// 实际包含在包装器中的<see cref="RenderTarget2D"/>实例
        /// </summary>
        public RenderTarget2D Target {
            get {
                if (IsUninitialized) {
                    target = CreationCondition(Main.screenWidth, Main.screenHeight);
                    WaitingForFirstInitialization = false;
                    IsDisposed = false;
                }

                TimeSinceLastAccessed = 0;
                return target;
            }
            private set => target = value;
        }

        public int Width => Target.Width;

        public int Height => Target.Height;

        public Vector2 Size => new(Width, Height);

        /// <summary>
        /// 获取此目标以来的帧数。用于出于性能考虑释放未使用的目标
        /// </summary>
        public int TimeSinceLastAccessed;

        public delegate RenderTarget2D RenderTargetCreationCondition(int screenWidth, int screenHeight);

        /// <summary>
        /// 由于某些原因，给这个构造函数添加更多的参数会导致低端pc在FNA中崩溃。我不知道为什么....所以不要使用它们
        /// </summary>
        /// <param name="screenWidth"></param>
        /// <param name="screenHeight"></param>
        /// <returns></returns>
        public static RenderTarget2D CreateScreenSizedTarget(int screenWidth, int screenHeight) => new(Main.instance.GraphicsDevice, screenWidth, screenHeight);

        public ManagedRenderTarget(bool shouldResetUponScreenResize, RenderTargetCreationCondition creationCondition, bool shouldAutoDispose = true) {
            ShouldResetUponScreenResize = shouldResetUponScreenResize;
            CreationCondition = creationCondition;
            ShouldAutoDispose = shouldAutoDispose;
            PeSystem.ManagedTargets.Add(this);
        }

        /// <summary>
        /// 将当前渲染目标设置为提供的目标
        /// </summary>
        /// <param name="target">要切换到的渲染目标</param>
        /// <param name="flushColor">用于清除屏幕的颜色。默认为透明</param>
        public void SwapTo(Color? flushColor = null) => Target.SwapTo(flushColor);

        /// <summary>
        /// 由<see cref="PeSystem"/>自动调用，不要手动调用
        /// </summary>
        public void Dispose() {
            if (IsDisposed)
                return;

            IsDisposed = true;
            target?.Dispose();
            GC.SuppressFinalize(this);
        }

        //// <summary>
        /// 由<see cref="PeSystem"/>自动调用，不要手动调用
        /// </summary>
        public void Recreate(int screenWidth, int screenHeight) {
            Dispose();
            IsDisposed = false;

            target = CreationCondition(screenWidth, screenHeight);
            TimeSinceLastAccessed = 0;
        }

        public static implicit operator RenderTarget2D(ManagedRenderTarget target) => target.Target;
    }
}
