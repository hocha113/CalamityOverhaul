using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu
{
    /// <summary>
    /// 飞眼编队接管：ai[0]=模式(0编队/闲逛 3四散)，ai[1]=槽位
    /// 编队目标位每帧从 BrainFormationChannel 读取（各端一致推演）
    /// </summary>
    internal class BrainCreeperAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.Creeper;

        private const int ModeFollow = 0;
        private const int ModeScatter = 3;

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetProperty() {
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 10;
        }

        #region 指挥接口（服务端调用）

        /// <summary>入编闲逛</summary>
        public static void CommandIdle(NPC creeper, int slot) {
            creeper.ai[0] = ModeFollow;
            creeper.ai[1] = slot;
            creeper.netUpdate = true;
        }

        /// <summary>四散撤离</summary>
        public static void CommandScatter(NPC creeper) {
            creeper.ai[0] = ModeScatter;
            creeper.netUpdate = true;
        }

        #endregion

        public override bool AI() {
            NPC brain = BrainMotion.FindBrain();

            //脑不在场：怯逃并消散
            if (brain == null) {
                npc.velocity = Vector2.Lerp(npc.velocity, Vector2.UnitY * -7f, 0.03f);
                npc.EncourageDespawn(30);
                npc.damage = 0;
                return false;
            }

            npc.timeLeft = 600;
            int mode = (int)npc.ai[0];
            int slot = (int)npc.ai[1];

            if (mode == ModeScatter) {
                UpdateScatter();
                return false;
            }

            if (BrainFormationChannel.Fresh && BrainFormationChannel.Mode != BrainFormationChannel.ModeNone) {
                UpdateFormation(slot);
            }
            else {
                UpdateIdleOrbit(brain, slot);
            }

            //节拍脉动灯光
            Lighting.AddLight(npc.Center, BrainMotion.BloodDark.ToVector3() * (0.3f + BrainHeartbeat.Pulse * 0.4f));
            return false;
        }

        #region 行为

        /// <summary>闲逛：绕脑松散环游，呼吸浮动（相位取脑的同步时钟，各端一致）</summary>
        private void UpdateIdleOrbit(NPC brain, int slot) {
            npc.damage = 0;
            npc.knockBackResist = 0.35f;

            float baseAngle = MathHelper.TwoPi * (slot % 12) / 12f;
            float time = brain.ai[3] * 0.008f;
            float breathe = (float)Math.Sin(brain.ai[3] * 0.028f + slot * 1.3f) * 26f;
            Vector2 target = brain.Center + (baseAngle + time).ToRotationVector2() * (150f + breathe);

            BrainMotion.SpringHover(npc, target, 0.02f, 0.1f, 17f);
            FaceVelocity();
        }

        /// <summary>编队：读通道推演槽位目标</summary>
        private void UpdateFormation(int slot) {
            npc.knockBackResist = 0f;
            npc.damage = BrainFormationChannel.DamageOn ? npc.defDamage : 0;

            Vector2 target;
            int slotCount = BrainFormationChannel.SlotCount;

            if (BrainFormationChannel.Mode == BrainFormationChannel.ModeCage) {
                float angle = MathHelper.TwoPi * (slot % slotCount) / slotCount + BrainFormationChannel.SpinPhase;
                //缺口：落在缺口扇区的飞眼向两侧让位
                if (BrainFormationChannel.GapAngle > -5f) {
                    float delta = MathHelper.WrapAngle(angle - BrainFormationChannel.GapAngle);
                    float half = BrainFormationChannel.GapHalfWidth;
                    if (Math.Abs(delta) < half) {
                        angle = BrainFormationChannel.GapAngle + Math.Sign(delta == 0f ? 1f : delta) * half * 1.15f;
                    }
                }
                target = BrainFormationChannel.CageCenter + angle.ToRotationVector2() * BrainFormationChannel.CageRadius;
            }
            else {
                //辐条：槽位按辐条分组，沿条向外排布
                int spokes = BrainFormationChannel.SpokeCount;
                int spokeIdx = slot % spokes;
                int depth = slot / spokes;
                int depthCount = Math.Max(1, (slotCount + spokes - 1) / spokes);
                float spokeAngle = MathHelper.TwoPi * spokeIdx / spokes + BrainFormationChannel.SpinPhase;
                float reach = BrainFormationChannel.SpokeReach;
                float dist = MathHelper.Lerp(120f, 560f, (depth + 1f) / depthCount * reach);
                target = BrainFormationChannel.CageCenter + spokeAngle.ToRotationVector2() * dist;
            }

            //编队跟位快而硬
            BrainMotion.SpringHover(npc, target, 0.045f, 0.16f, 30f);
            FaceVelocity();
        }

        /// <summary>四散：向外逃逸渐隐</summary>
        private void UpdateScatter() {
            npc.damage = 0;
            npc.dontTakeDamage = true;
            NPC brain = BrainMotion.FindBrain();
            Vector2 away = brain != null
                ? (npc.Center - brain.Center).SafeNormalize(-Vector2.UnitY)
                : -Vector2.UnitY;
            npc.velocity = Vector2.Lerp(npc.velocity, away * 16f, 0.05f);
            npc.alpha = Math.Min(npc.alpha + 5, 255);
            if (npc.alpha >= 255 && !VaultUtils.isClient) {
                npc.active = false;
                npc.netUpdate = true;
            }
        }

        private void FaceVelocity() {
            if (npc.velocity.LengthSquared() > 1f) {
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
            }
        }

        #endregion

        #region 绘制：本体+节拍红晕+速度残影

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.Creeper);
            Texture2D tex = TextureAssets.Npc[NPCID.Creeper].Value;
            Rectangle frameRect = npc.frame;
            if (frameRect.Height <= 0) {
                frameRect = new Rectangle(0, 0, tex.Width, tex.Height / Math.Max(Main.npcFrameCount[NPCID.Creeper], 1));
            }
            Vector2 origin = frameRect.Size() * 0.5f;
            Vector2 drawPos = npc.Center - screenPos;
            SpriteEffects effects = npc.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float alphaMul = 1f - npc.alpha / 255f;

            //高速残影
            float speed = npc.velocity.Length();
            if (speed > 10f) {
                float trailAlpha = MathHelper.Clamp((speed - 10f) / 22f, 0f, 0.6f) * alphaMul;
                for (int i = 2; i < npc.oldPos.Length; i += 2) {
                    if (npc.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float k = 1f - i / (float)npc.oldPos.Length;
                    Vector2 ghostPos = npc.oldPos[i] + npc.Size * 0.5f - screenPos;
                    spriteBatch.Draw(tex, ghostPos, frameRect, new Color(150, 22, 34, 0) * (trailAlpha * k * 0.6f),
                        npc.rotation, origin, npc.scale, effects, 0f);
                }
            }

            //伤害窗口红晕警示
            if (npc.damage > 0) {
                float pulse = 0.6f + 0.4f * BrainHeartbeat.Pulse;
                Color warn = new Color(210, 30, 40, 0) * (0.55f * pulse * alphaMul);
                for (int i = 0; i < 4; i++) {
                    Vector2 dir = (MathHelper.PiOver2 * i).ToRotationVector2() * 3f;
                    spriteBatch.Draw(tex, drawPos + dir, frameRect, warn, npc.rotation, origin, npc.scale, effects, 0f);
                }
            }

            spriteBatch.Draw(tex, drawPos, frameRect, drawColor * alphaMul,
                npc.rotation, origin, npc.scale, effects, 0f);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;

        #endregion
    }
}
