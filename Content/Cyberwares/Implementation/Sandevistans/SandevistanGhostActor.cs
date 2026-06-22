using CalamityOverhaul.Common;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 斯安威斯坦残影 Actor，单帧玩家快照
    /// <br/>BeforePlayers 层；每帧首个残影统一用 PlayerRenderer 直绘到当前目标，不切换 RenderTarget
    /// </summary>
    internal class SandevistanGhostActor : Actor
    {
        private static uint lastBatchDrawFrame;

        public Vector2 SnapshotPosition;
        public Vector2 SnapshotVelocity;
        public int SnapshotDirection;
        public Rectangle SnapshotBodyFrame;
        public Rectangle SnapshotLegFrame;
        public float SnapshotFullRotation;
        public Vector2 SnapshotFullRotationOrigin;
        public int OwnerIndex;
        public int Lifetime;
        public int MaxLifetime;
        public float Alpha => Math.Clamp((float)Lifetime / MaxLifetime, 0f, 1f);

        public override void OnSpawn(params object[] args) {
            Width = 4;
            Height = 4;
            DrawLayer = ActorDrawLayer.BeforePlayers;
            DrawExtendMode = 600;
            MaxLifetime = 120;
            Lifetime = MaxLifetime;

            if (args is not null && args.Length >= 1 && args[0] is Player owner) {
                CapturePlayerState(owner);
            }
        }

        private void CapturePlayerState(Player owner) {
            OwnerIndex = owner.whoAmI;
            SnapshotPosition = owner.position;
            SnapshotVelocity = owner.velocity;
            SnapshotDirection = owner.direction;
            SnapshotBodyFrame = owner.bodyFrame;
            SnapshotLegFrame = owner.legFrame;
            SnapshotFullRotation = owner.fullRotation;
            SnapshotFullRotationOrigin = owner.fullRotationOrigin;
            Position = owner.Center;
        }

        public override void AI() {
            Lifetime--;
            if (Lifetime <= 0) {
                ActorLoader.KillActor(WhoAmI);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            //每帧首个残影触发一次批量绘制
            if (Main.GameUpdateCount == lastBatchDrawFrame) {
                return false;
            }
            lastBatchDrawFrame = Main.GameUpdateCount;

            List<SandevistanGhostActor> ghosts = ActorLoader.GetActiveActors<SandevistanGhostActor>();
            if (ghosts.Count == 0) {
                return false;
            }

            //直接绘到当前渲染目标，绝不切换 RenderTarget：
            //复古/Trippy 光照、低水波质量等模式下世界是直接画在 backbuffer 上的，
            //一旦切到自建 RT 再切回，backbuffer 已绘的物块与背景会被丢弃，造成整屏黑屏/闪黑。
            //镜像 MimicPhantom/CloneFish 的安全做法：仅切换 SpriteBatch 批次，由 PlayerRenderer 直绘。
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);

            try {
                //同源残影只准备一次傀儡，避免逐体重复 CopyVisuals/ResetEffects
                int preparedOwner = -1;
                foreach (SandevistanGhostActor ghost in ghosts) {
                    if (!ghost.Active || ghost.Alpha <= 0.01f) {
                        continue;
                    }

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

            //恢复 ActorLoader 批次
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        //用已准备好的傀儡绘制身体残影：固定在过去那一帧的姿态/朝向，不跟随玩家当前动作，也不重放特效
        private static void DrawGhost(SandevistanGhostActor ghost) {
            //蓝→青绿 tint，A 通道随生命淡出
            float fadeProgress = 1f - ghost.Alpha;
            Color tint = Color.Lerp(new Color(0, 180, 255, 255), new Color(0, 255, 200, 255), fadeProgress);
            tint.A = (byte)(255 * ghost.Alpha);

            PlayerCloneRenderer.DrawPrepared(ghost.SnapshotPosition, tint, ghost.SnapshotDirection,
                ghost.SnapshotBodyFrame, ghost.SnapshotLegFrame,
                ghost.SnapshotFullRotation, ghost.SnapshotFullRotationOrigin);
        }
    }
}
