using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Particles.Core
{
    internal class PeSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ)
                CWRParticleHandler.Update();
        }

        public static void CWRDrawForegroundParticles(Terraria.On_Main.orig_DrawInfernoRings orig, Main self) {
            CWRParticleHandler.DrawAllParticles(Main.spriteBatch);
            orig(self);
        }

        internal static List<ManagedRenderTarget> ManagedTargets = new();

        public delegate void RenderTargetUpdateDelegate();

        /// <summary>
        /// 使用此事件在绘制游戏中的任何其他内容之前更新/绘制目标上的内容
        /// </summary>
        public static event RenderTargetUpdateDelegate RenderTargetUpdateLoopEvent;

        /// <summary>
        /// 在自动释放目标之前，目标上次访问后应经过多少帧
        /// </summary>
        public const int TimeBeforeAutoDispose = 600;

        internal static void ResetTargetSizes(Vector2 screenSize) {
            foreach (ManagedRenderTarget target in ManagedTargets) {
                // 不要尝试重新创建已经初始化或不应该重新创建的目标
                if (target is null || target.IsDisposed || target.WaitingForFirstInitialization)
                    continue;

                Main.QueueMainThreadAction(() => {
                    target.Recreate((int)screenSize.X, (int)screenSize.Y);
                });
            }
        }

        internal static void DisposeOfTargets() {
            if (ManagedTargets is null)
                return;

            Main.QueueMainThreadAction(() => {
                foreach (ManagedRenderTarget target in ManagedTargets)
                    target?.Dispose();
                ManagedTargets.Clear();
            });
        }

        public override void OnModLoad() {
            ManagedTargets = new();
            Main.OnPreDraw += HandleTargetUpdateLoop;
            Main.OnResolutionChanged += ResetTargetSizes;
        }

        public override void OnModUnload() {
            DisposeOfTargets();
            Main.OnPreDraw -= HandleTargetUpdateLoop;
            Main.OnResolutionChanged -= ResetTargetSizes;

            if (RenderTargetUpdateLoopEvent != null) {
                foreach (var subscription in RenderTargetUpdateLoopEvent.GetInvocationList())
                    RenderTargetUpdateLoopEvent -= (RenderTargetUpdateDelegate)subscription;
            }
        }

        private void HandleTargetUpdateLoop(GameTime obj) {
            // 自动处理在一段时间内未使用的目标，以阻止它们占用GPU内存
            if (ManagedTargets != null) {
                foreach (ManagedRenderTarget target in ManagedTargets) {
                    if (target == null || target.IsDisposed || !target.ShouldAutoDispose)
                        continue;

                    if (target.TimeSinceLastAccessed >= TimeBeforeAutoDispose)
                        target.Dispose();
                    else
                        target.TimeSinceLastAccessed++;
                }
            }
            RenderTargetUpdateLoopEvent?.Invoke();
        }
    }
}
