using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>
    /// 磁暴收束·二环：磁场力线亮起、音调爬升（长预警）→ 场上废钢堆被整座拽飞进统帅 →
    /// 内外两圈碎片交错环绕（聚拢段无伤害）→ 内环先甩、外环追杀，两波读法。
    /// 场上废钢堆越多风暴越密，迫击埋的伏笔在这里结账
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.MagnetStorm, typeof(ScrapStateContext))]
    internal class ScrapMagnetStormState : ScrapStateBase
    {
        public override string StateName => "MagnetStorm";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.MagnetStorm;

        //==================== 时序 ====================

        /// <summary>废钢堆起飞拍（飞行 20-40f 后陆续被吸收）</summary>
        private const int SuckBeat = 50;
        /// <summary>碎片环生成拍</summary>
        private const int GatherBeat = 90;
        /// <summary>内环甩出拍（碎片自身倒计时 90）</summary>
        private const int InnerFling = 180;
        /// <summary>外环甩出拍（碎片自身倒计时 122）</summary>
        private const int OuterFling = 212;
        private const int StateEnd = 232;

        private bool fieldEnsured;
        private bool sucked;
        private bool gathered;
        private bool innerCue;
        private bool outerCue;
        private int pilesEaten;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            //全程慢压玩家头顶：威慑而非追杀
            Vector2 anchor = ctx.Target.Center + new Vector2(0f, -260f);
            GlideToward(ctx, anchor, 0.02f, 6f, 0.06f);
            LeanByVelocity(npc, 0.06f);

            if (!fieldEnsured) {
                fieldEnsured = true;
                ctx.Owner.EnsureMagnetFieldProj();
            }

            if (t < GatherBeat) {
                //==================== 预警与堆体回收 ====================
                ctx.MagnetGlow = MathHelper.Clamp(t / 40f, 0f, 1f);
                ctx.MagnetPull = 1f;
                ctx.WeldHeat = MathHelper.Clamp(t / (float)GatherBeat, 0f, 0.7f);
                //音调逐级爬升的磁力蜂鸣
                if (t % 15 == 0) {
                    SoundEngine.PlaySound(SoundID.Item15 with {
                        Volume = 0.35f,
                        Pitch = -0.6f + t / (float)GatherBeat * 0.9f,
                        MaxInstances = 2
                    }, npc.Center);
                }
                //废钢堆整座拽飞：吸收数决定风暴密度
                if (t == SuckBeat) {
                    if (!sucked) {
                        sucked = true;
                        if (!VaultUtils.isClient) {
                            pilesEaten = ScrapJunkPile.SuckAll();
                        }
                        SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.55f, Pitch = -0.25f, MaxInstances = 1 }, npc.Center);
                    }
                }
                if (t == GatherBeat - 20) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 1 }, npc.Center);
                }
                Timer++;
                return null;
            }

            if (t < InnerFling) {
                //==================== 聚拢成双环 ====================
                if (!gathered) {
                    gathered = true;
                    GatherStorm(ctx, npc);
                }
                ctx.MagnetGlow = 1f;
                ctx.MagnetPull = 1f;
                ctx.WeldHeat = 1f;
                Timer++;
                return null;
            }

            //==================== 两波甩出（碎片自身按倒计时同拍翻转，这里只放收势演出）====================
            ctx.MagnetGlow = MathHelper.Clamp((StateEnd - t) / 40f, 0f, 1f);
            ctx.MagnetPull = -1f;
            ctx.WeldHeat = MathHelper.Clamp((StateEnd - t) / 40f, 0f, 1f);
            if (!innerCue) {
                innerCue = true;
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 1 }, npc.Center);
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = 0.15f, MaxInstances = 2 }, npc.Center);
                ShakeNearby(npc.Center, 3.5f);
            }
            if (t >= OuterFling && !outerCue) {
                outerCue = true;
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 1 }, npc.Center);
                ShakeNearby(npc.Center, 2.5f);
            }
            npc.velocity *= 0.92f;
            Timer++;
            if (t >= StateEnd) {
                return EndAttack(ctx, 100);
            }
            return null;
        }

        /// <summary>聚拢拍：双环碎片入轨，内环密先甩、外环稀追杀；密度吃堆数</summary>
        private void GatherStorm(ScrapStateContext ctx, NPC npc) {
            ShakeNearby(npc.Center, 2.5f);
            if (VaultUtils.isClient) {
                return;
            }
            int inner = Math.Min(5 + pilesEaten * 2, 12);
            const int outer = 5;
            int damage = ScrapDirector.ScaleProjectileDamage(npc, ScrapDirector.GroundSawDamage);
            for (int i = 0; i < inner; i++) {
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    npc.Center + Main.rand.NextVector2Circular(40f, 40f), Vector2.Zero,
                    ModContent.ProjectileType<ScrapDebris>(), damage, 3f,
                    Main.myPlayer, npc.whoAmI, InnerFling - GatherBeat, 0f);
            }
            for (int i = 0; i < outer; i++) {
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    npc.Center + Main.rand.NextVector2Circular(40f, 40f), Vector2.Zero,
                    ModContent.ProjectileType<ScrapDebris>(), damage, 3f,
                    Main.myPlayer, npc.whoAmI, OuterFling - GatherBeat, 1f);
            }
        }
    }
}
