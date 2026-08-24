using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>
    /// 星髓凝滴：月蚀噬咬吸出的治疗载体，沿舌线回航月口，可拦截。
    /// 抵达按 头→核心→四手 分配回复（核心裸露期核心优先）。
    /// ai 槽沿用原版：[0]=头 whoAmI+1，[1]=舌弹幕索引，[2]=航程计时
    /// </summary>
    internal class MoonLordLeechBlobAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.MoonLordLeechBlob;

        internal const float TravelFrames = 90f;
        internal const int HealPool = 800;

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override void SetProperty() {
            npc.aiStyle = -1;
            npc.knockBackResist = 0f;
        }

        public override bool AI() {
            npc.aiStyle = -1;
            npc.netOffset = Vector2.Zero;

            int headIndex = (int)Math.Abs(npc.ai[0]) - 1;
            if (headIndex < 0 || headIndex >= Main.maxNPCs
                || !Main.npc[headIndex].active || Main.npc[headIndex].type != NPCID.MoonLordHead) {
                //坠毁由服务端裁定并同步；客户端出生首帧 ai 未到时本地误杀会闪尸+喷渣
                if (!VaultUtils.isClient) {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                }
                return false;
            }
            NPC head = Main.npc[headIndex];

            npc.ai[2]++;
            if (npc.ai[2] >= TravelFrames) {
                //抵达月口：分配治疗（服务端裁定，客户端由同步收敛）
                if (!VaultUtils.isClient) {
                    DistributeHeal(head);
                }
                npc.life = 0;
                npc.HitEffect();
                npc.active = false;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
                return false;
            }

            //沿舌线回航：舌端→月口插值（原版装配）
            int tongueIndex = (int)npc.ai[1];
            Vector2 from = npc.Center;
            if (tongueIndex >= 0 && tongueIndex < Main.maxProjectiles && Main.projectile[tongueIndex].active
                && Main.projectile[tongueIndex].type == ProjectileID.MoonLeech) {
                from = Main.projectile[tongueIndex].Center;
            }
            Vector2 mouth = head.Center + new Vector2(0f, 216f);
            npc.velocity = Vector2.Zero;
            npc.Center = Vector2.Lerp(from, mouth, npc.ai[2] / TravelFrames);

            //星髓拖尾
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                MLordScreenFX.StarBurst(npc.Center, 0.22f, 2);
            }
            Lighting.AddLight(npc.Center, MLordDirector.Phantasmal.ToVector3() * 0.35f);
            return false;
        }

        /// <summary>治疗分配：裸露期核心优先，否则头→核心→手</summary>
        private void DistributeHeal(NPC head) {
            NPC core = MLordFacts.GetCore(head);
            int pool = HealPool;

            bool coreExposed = core != null && (int)core.ai[MLordAiSlots.CorePhase] == MLordPhase.CoreExposed;
            Span<int> order = stackalloc int[2 + MLordPartsStatus.HandSlots];
            int count = 0;
            if (coreExposed) {
                if (core != null) {
                    order[count++] = core.whoAmI;
                }
                order[count++] = head.whoAmI;
            }
            else {
                order[count++] = head.whoAmI;
                if (core != null) {
                    order[count++] = core.whoAmI;
                }
            }
            //手只在核心在场时入列：default 快照的手槽是 0 而非 -1，直接消费会把
            //Main.npc[0]（任意无关 NPC）灌进治疗序列
            if (core != null) {
                MLordPartsStatus parts = MLordFacts.ScanParts(core);
                for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                    if (parts.HandIndex(slot) >= 0) {
                        order[count++] = parts.HandIndex(slot);
                    }
                }
            }

            for (int i = 0; i < count && pool > 0; i++) {
                NPC member = Main.npc[order[i]];
                if (!member.active) {
                    continue;
                }
                int missing = member.lifeMax - member.life;
                if (missing <= 0) {
                    continue;
                }
                int amount = Math.Min(missing, pool);
                member.life += amount;
                pool -= amount;
                member.HealEffect(amount);
                member.netUpdate = true;
            }
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //显式帧动画（接管后原版不再推帧）
            Texture2D tex = TextureAssets.Npc[NPCID.MoonLordLeechBlob].Value;
            int frameCount = Math.Max(Main.npcFrameCount[NPCID.MoonLordLeechBlob], 1);
            Rectangle frame = tex.Frame(1, frameCount, 0, (int)(Main.GameUpdateCount / 6) % frameCount);
            Color light = MLordDrawHelper.CommonLight(npc);
            spriteBatch.Draw(tex, npc.Center - screenPos, frame, light, npc.rotation,
                frame.Size() / 2f, npc.scale, SpriteEffects.None, 0f);

            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (glow != null) {
                float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f + npc.whoAmI);
                Main.EntitySpriteDraw(glow, npc.Center - screenPos, null,
                    MLordDirector.Phantasmal with { A = 0 } * (0.6f * pulse), 0f,
                    glow.Size() / 2f, 0.24f * pulse, SpriteEffects.None, 0);
            }
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
    }
}
