using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 斯安威斯坦残影，纯客户端视觉，不进 Actor 同步
    /// <br/>激活态本身已经复制到每台机器，所以各客户端各自为全部激活玩家采样，一个包都不用发
    /// <br/>绘制交给 <see cref="SandevistanGhostRender"/>，层级与原来的
    /// ActorDrawLayer.BeforePlayers 完全一致
    /// </summary>
    internal sealed class SandevistanGhostVisual : ModSystem
    {
        /// <summary>单帧玩家姿态快照</summary>
        private struct Ghost
        {
            public int OwnerIndex;
            public Vector2 Position;
            public int Direction;
            public Rectangle BodyFrame;
            public Rectangle LegFrame;
            public float FullRotation;
            public Vector2 FullRotationOrigin;
            public int Lifetime;
        }

        private const int MaxLifetime = 120;
        //单人满载 120/4 = 30 个，留足多人同时激活的份额
        private const int MaxGhosts = 240;

        private static readonly List<Ghost> ghosts = new(MaxGhosts);

        /// <summary>采一帧姿态，客户端本地留存</summary>
        internal static void Capture(Player owner) {
            if (Main.dedServ || owner?.active != true || ghosts.Count >= MaxGhosts) {
                return;
            }

            ghosts.Add(new Ghost {
                OwnerIndex = owner.whoAmI,
                Position = owner.position,
                Direction = owner.direction,
                BodyFrame = owner.bodyFrame,
                LegFrame = owner.legFrame,
                FullRotation = owner.fullRotation,
                FullRotationOrigin = owner.fullRotationOrigin,
                Lifetime = MaxLifetime,
            });
        }

        internal static void Reset() => ghosts.Clear();

        public override void OnWorldLoad() => Reset();

        public override void OnWorldUnload() => Reset();

        public override void Unload() => Reset();

        public override void PostUpdateEverything() {
            if (ghosts.Count == 0) {
                return;
            }

            for (int i = ghosts.Count - 1; i >= 0; i--) {
                Ghost ghost = ghosts[i];
                if (--ghost.Lifetime <= 0) {
                    ghosts.RemoveAt(i);
                    continue;
                }
                ghosts[i] = ghost;
            }
        }

        /// <summary>批次参数沿用原 Actor 版本，避免观感发生无关变化</summary>
        internal static void Draw(SpriteBatch spriteBatch) {
            if (Main.dedServ || ghosts.Count == 0) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, null, Main.Rasterizer, null,
                Main.GameViewMatrix.ZoomMatrix);

            try {
                //残影按采样先后排列，同源连续，傀儡只在换人时重新准备
                int preparedOwner = -1;
                foreach (Ghost ghost in ghosts) {
                    if (ghost.OwnerIndex < 0 || ghost.OwnerIndex >= Main.maxPlayers) {
                        continue;
                    }

                    Player source = Main.player[ghost.OwnerIndex];
                    if (source == null || !source.active || source.dead) {
                        continue;
                    }

                    if (ghost.OwnerIndex != preparedOwner) {
                        PlayerCloneRenderer.Prepare(source);
                        preparedOwner = ghost.OwnerIndex;
                    }
                    DrawGhost(ghost);
                }
            } catch (Exception ex) {
                CWRMod.Instance?.Logger.Warn("[Sandevistan] Ghost render failed: " + ex);
            }

            spriteBatch.End();
        }

        //固定快照姿态，不跟当前动作
        private static void DrawGhost(in Ghost ghost) {
            float fade = Math.Clamp((float)ghost.Lifetime / MaxLifetime, 0f, 1f);
            if (fade <= 0.01f) {
                return;
            }

            float fadeProgress = 1f - fade;
            float fadeCurve = fade * fade * (3f - 2f * fade);
            Color tint = Color.Lerp(new Color(64, 235, 255, 255),
                new Color(8, 100, 235, 255), fadeProgress);
            tint.A = (byte)(255f * fadeCurve);

            PlayerCloneRenderer.DrawPrepared(ghost.Position, tint, ghost.Direction,
                ghost.BodyFrame, ghost.LegFrame,
                ghost.FullRotation, ghost.FullRotationOrigin);
        }
    }

    /// <summary>残影绘制层，等同于原 ActorDrawLayer.BeforePlayers 的时机</summary>
    internal sealed class SandevistanGhostRender : RenderHandle
    {
        public override void DrawBeforePlayers(SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap)
            => SandevistanGhostVisual.Draw(spriteBatch);
    }
}
