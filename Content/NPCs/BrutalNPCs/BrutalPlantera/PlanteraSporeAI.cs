using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera
{
    /// <summary>
    /// 孢子地雷：慢漂向玩家的荧光浮雷，互斥保持间距；
    /// 被打爆/引信到点→毒雾+点燃邻雷连锁殉爆。ai[0]=引信倒计时(0闲置)
    /// </summary>
    internal class PlanteraSporeAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.Spore;

        /// <summary>连锁引信帧数</summary>
        private const int FuseFrames = 26;

        public override bool? CanBrutalOverride() {
            return null;
        }

        /// <summary>场上孢子数</summary>
        internal static int CountSpores() {
            int count = 0;
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.Spore) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>服务端撒一颗孢子</summary>
        internal static void SpawnSpore(NPC boss, Vector2 pos, Vector2 vel) {
            if (VaultUtils.isClient || CountSpores() >= PlanteraDirector.MaxSporeMines) {
                return;
            }
            int index = NPC.NewNPC(boss.GetSource_FromAI(), (int)pos.X, (int)pos.Y, NPCID.Spore);
            if (index >= 0 && index < Main.maxNPCs) {
                NPC spore = Main.npc[index];
                spore.velocity = vel;
                spore.ai[1] = Main.rand.NextFloat(MathHelper.TwoPi);
                spore.netUpdate = true;
            }
        }

        public override bool AI() {
            npc.aiStyle = -1;
            npc.timeLeft = 300;
            npc.knockBackResist = 0f;

            //主体没了则孤儿雷静默消散
            if (PlanteraAI.FindBoss() == null) {
                if (!VaultUtils.isClient) {
                    npc.life = 0;
                    npc.active = false;
                    npc.netUpdate = true;
                }
                return false;
            }

            //寿命预算：太老的雷自燃(防无限堆场)
            npc.localAI[1] += 1f;
            if (!VaultUtils.isClient && npc.localAI[1] > 780f && npc.ai[0] <= 0f) {
                LightFuse(npc);
            }

            //引信段
            if (npc.ai[0] > 0f) {
                npc.ai[0] -= 1f;
                npc.velocity *= 0.9f;
                if (npc.ai[0] <= 0f && !VaultUtils.isClient) {
                    Pop();
                    return false;
                }
            }
            else {
                UpdateDrift();
            }

            //碰玩家即炸(接触伤害同帧结算)
            if (!VaultUtils.isClient) {
                Rectangle inflated = npc.Hitbox;
                inflated.Inflate(6, 6);
                foreach (var player in Main.ActivePlayers) {
                    if (player.Alives() && inflated.Intersects(player.Hitbox)) {
                        Pop();
                        return false;
                    }
                }
            }

            Lighting.AddLight(npc.Center, PlanteraRenderHelper.SporeGreen.ToVector3()
                * (npc.ai[0] > 0f ? 0.65f : 0.32f));

            return false;
        }

        /// <summary>慢漂+互斥+浮沉呼吸</summary>
        private void UpdateDrift() {
            Player closest = null;
            float bestDist = float.MaxValue;
            foreach (var player in Main.ActivePlayers) {
                if (!player.Alives()) {
                    continue;
                }
                float dist = npc.Distance(player.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    closest = player;
                }
            }

            if (closest != null) {
                Vector2 toward = (closest.Center - npc.Center).SafeNormalize(Vector2.Zero);
                npc.velocity += toward * 0.022f;
            }

            //同类互斥，保持雷场间距
            foreach (var other in Main.ActiveNPCs) {
                if (other.whoAmI == npc.whoAmI || other.type != NPCID.Spore) {
                    continue;
                }
                float dist = npc.Distance(other.Center);
                if (dist < 64f && dist > 0.01f) {
                    npc.velocity += (npc.Center - other.Center) / dist * 0.035f;
                }
            }

            //浮沉呼吸(用自身年龄计相位，各端从生成起齐步)
            npc.velocity.Y += (float)Math.Sin(npc.localAI[1] * 0.045f + npc.ai[1]) * 0.006f;
            npc.velocity *= 0.985f;
            if (npc.velocity.Length() > 1.4f) {
                npc.velocity *= 0.94f;
            }
            npc.rotation += npc.velocity.X * 0.02f;
        }

        /// <summary>点燃引信，服务端</summary>
        internal static void LightFuse(NPC spore) {
            if (spore.ai[0] > 0f) {
                return;
            }
            spore.ai[0] = FuseFrames;
            spore.netUpdate = true;
        }

        /// <summary>殉爆：毒雾+点邻雷引信，服务端</summary>
        private void Pop() {
            ChainDetonate(npc);
            npc.life = 0;
            npc.HitEffect();
            npc.active = false;
            npc.netUpdate = true;
        }

        /// <summary>爆点效果+邻雷连锁，服务端裁决</summary>
        internal static void ChainDetonate(NPC spore) {
            if (VaultUtils.isClient) {
                return;
            }

            Projectile.NewProjectile(spore.GetSource_FromAI(), spore.Center, Vector2.Zero,
                ModContent.ProjectileType<PlanteraSporeCloud>(),
                Math.Max(spore.damage / 3, 8), 0f, Main.myPlayer, 0.62f);

            foreach (var other in Main.ActiveNPCs) {
                if (other.whoAmI == spore.whoAmI || other.type != NPCID.Spore) {
                    continue;
                }
                if (spore.Distance(other.Center) < PlanteraDirector.SporeChainRadius) {
                    LightFuse(other);
                }
            }
        }

        /// <summary>被玩家打死也走连锁</summary>
        public override bool? SpecialOnKill() {
            if (!VaultUtils.isClient) {
                ChainDetonate(npc);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.45f, Pitch = 0.5f, MaxInstances = 6 }, npc.Center);
                PlanteraRenderHelper.SpawnSporePuff(npc.Center, 0.8f);
            }
            return null;
        }

        public override bool CheckActive() => true;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 mainPos = npc.Center - screenPos;

            //引信频闪：越接近爆越快
            float pulse;
            if (npc.ai[0] > 0f) {
                float fuseT = 1f - npc.ai[0] / FuseFrames;
                pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (30f + fuseT * 60f));
                pulse = MathHelper.Lerp(0.5f, 1f, pulse) + fuseT * 0.4f;
            }
            else {
                pulse = 0.35f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f + npc.ai[1]);
            }

            spriteBatch.Draw(texture, mainPos, null, drawColor,
                npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, mainPos, null, PlanteraRenderHelper.SporeGreen with { A = 0 } * pulse,
                npc.rotation, origin, npc.scale * (1f + pulse * 0.1f), SpriteEffects.None, 0f);

            return false;
        }
    }
}
