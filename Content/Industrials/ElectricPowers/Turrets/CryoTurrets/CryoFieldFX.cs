using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.CryoTurrets
{
    /// <summary>
    /// 冰冻塔脉冲霜环绘制:复用共享参数化冲击环(<see cref="ShockRingDraw"/>,冰蓝色板),
    /// 从塔心扩散到作用半径,诚实标示寒场边界。合同要求调用方处于实体批,
    /// PreDrawEverything 无活动批,这里自开实体形制批再逐塔调用
    /// </summary>
    internal class CryoFieldRender : GlobalTileProcessor
    {
        //冰蓝色板,与 CryoTurret.Tint 同系
        private static readonly Color RingBright = new(235, 250, 255);
        private static readonly Color RingDeep = new(70, 120, 210);

        public override bool PreDrawEverything(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return true;
            }

            bool begun = false;
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is not CryoTurretTP cryo || !cryo.Active) {
                    continue;
                }
                float t = cryo.FrostRingT;
                if (t >= 1f || cryo.GlowIntensity < 0.1f) {
                    continue;
                }
                if (!VaultUtils.IsPointOnScreen(cryo.PosInWorld - Main.screenPosition, cryo.DrawExtendMode)) {
                    continue;
                }

                if (!begun) {
                    begun = true;
                    //ShockRingDraw 合同:调用方须处于实体绘制批(Deferred AlphaBlend)
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                        DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                }

                //快张缓收:EaseOutCubic 到作用半径,后半程消隐
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                float radius = cryo.EffectiveRange * eased;
                float alpha = (1f - t) * (1f - t) * 0.55f * cryo.GlowIntensity;
                ShockRingDraw.Draw(spriteBatch, tp.CenterInWorld, radius,
                    MathHelper.Lerp(30f, 14f, t), RingBright, CryoTurret.Tint, RingDeep,
                    alpha, innerGlow: 0.12f, timeSeed: tp.Position.X * 0.173f);
            }

            if (begun) {
                spriteBatch.End();
            }
            return true;
        }
    }

    /// <summary>
    /// 冻结瞬间观测:Frozen buff 经原版同步全端可见,上升沿+寒场覆盖判定→冰晶爆裂。
    /// 纯客户端表现,不改判定
    /// </summary>
    internal class CryoFreezeFX : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool wasFrozen;

        public override void PostAI(NPC npc) {
            if (Main.dedServ) {
                return;
            }
            bool frozen = npc.HasBuff(BuffID.Frozen);
            if (frozen && !wasFrozen && InRunningCryoField(npc)) {
                SpawnFreezeBurst(npc);
            }
            wasFrozen = frozen;
        }

        /// <summary>该NPC是否在某台运转中冰冻塔的寒场里(冻结归因门,防别家Frozen误触)</summary>
        private static bool InRunningCryoField(NPC npc) {
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is CryoTurretTP cryo && cryo.Active && cryo.GlowIntensity > 0.3f
                    && npc.Center.DistanceSQ(tp.CenterInWorld) <= cryo.EffectiveRange * cryo.EffectiveRange * 1.2f) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>冻结爆裂:冰晶碎片外溅+霜闪点缀+冰尘,读作体表水汽瞬间结晶</summary>
        private static void SpawnFreezeBurst(NPC npc) {
            if (!VaultUtils.IsPointOnScreen(npc.Center - Main.screenPosition, 200)) {
                return;
            }
            Color icy = new(205, 235, 255);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f);
                PRTLoader.NewParticle<PRT_DefCrystalShard>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                    vel, icy, Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(Main.rand.Next(20, 34), Main.rand.NextFloat(-0.3f, 0.3f), 0.09f);
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f),
                    Vector2.Zero, icy, Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(12, 20));
            }
            //中心一记大晶闪
            PRTLoader.NewParticle<PRT_DefFrostGlint>(npc.Center, Vector2.Zero, Color.White, 2.2f)
                ?.Configure(14);
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    DustID.Ice, 0f, 0f, 100, default, 1.1f);
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(2f, 2f);
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.45f, Pitch = 0.25f }, npc.Center);
        }
    }
}
