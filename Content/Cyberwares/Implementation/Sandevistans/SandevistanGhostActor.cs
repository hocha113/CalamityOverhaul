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
            //每帧首个残影批量绘一次
            if (Main.GameUpdateCount == lastBatchDrawFrame) {
                return false;
            }
            lastBatchDrawFrame = Main.GameUpdateCount;

            List<SandevistanGhostActor> ghosts = ActorLoader.GetActiveActors<SandevistanGhostActor>();
            if (ghosts.Count == 0) {
                return false;
            }

            //不切 RenderTarget，只切 SpriteBatch；Retro/低水波下世界画在 backbuffer，切 RT 再回会丢背景闪黑
            //同 MimicPhantom/CloneFish
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);

            try {
                //同源残影傀儡只准备一次
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

        //固定快照姿态，不跟当前动作
        private static void DrawGhost(SandevistanGhostActor ghost) {
            float fade = Math.Clamp(ghost.Alpha, 0f, 1f);
            float fadeProgress = 1f - fade;
            float fadeCurve = fade * fade * (3f - 2f * fade);
            Color tint = Color.Lerp(new Color(64, 235, 255, 255),
                new Color(8, 100, 235, 255), fadeProgress);
            tint.A = (byte)(255f * fadeCurve);

            PlayerCloneRenderer.DrawPrepared(ghost.SnapshotPosition, tint, ghost.SnapshotDirection,
                ghost.SnapshotBodyFrame, ghost.SnapshotLegFrame,
                ghost.SnapshotFullRotation, ghost.SnapshotFullRotationOrigin);
        }
    }
}
