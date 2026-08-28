using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.States
{
    /// <summary>
    /// A8 满场引爆（P3 招牌）：八腿撑地立至全高（剪影即预告）→ 怒吼蓄力，
    /// 全场脓池同时进入引信（引信 = 蓄力剩余 + 距离/波速）→ 蓄力结束的一刻，
    /// 泉柱行波从蟒身由近及远滚过整场——整局吐下的每一口痰在此一起收账。
    /// 公平口径：逃生答案是池间站缝（泉柱只在池位起，池距天然宽于柱宽），
    /// 不是跑赢波前（波速高于跑速，行波只管演出气势）；每池引燃后自带
    /// 冒泡转急 + 升调 ≥40 帧（池子就是自己的预告实体）、池位全是玩家
    /// 亲眼见过的落点；池数不足先环射补种（演出成立性阀）。收招留整拍呼吸。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FssStateIndex.FieldDetonate, typeof(FssStateContext))]
    internal class FssFieldDetonateState : FssStateBase
    {
        public override string StateName => "FieldDetonate";
        public override FssStateIndex StateIndex => FssStateIndex.FieldDetonate;

        private int ChargeEnd => FssDirector.DetonateRaiseFrames + FssDirector.DetonateChargeFrames;

        private bool seeded;
        private bool ignited;
        private bool lateIgnited;
        /// <summary>落地闸门已过（未贴地不起立，立腿剪影必须踩在地上）</summary>
        private bool grounded;
        private int approachTimer;
        /// <summary>行波观察段的估算时长（引燃帧写定）</summary>
        private int waveWatch = 90;

        public override void OnEnter(FssStateContext ctx) {
            base.OnEnter(ctx);
            seeded = false;
            ignited = false;
            lateIgnited = false;
            grounded = false;
            approachTimer = 0;

            //演出成立性阀：池太少先环射补种（重团弧线落地成池，正好赶上引信）
            if (!VaultUtils.isClient && CountPools() < FssDirector.DetonateMinPools) {
                seeded = true;
                NPC npc = ctx.Npc;
                int damage = FssDirector.ScaleProjectileDamage(npc, FssDirector.IchorGlobDamage);
                int type = ModContent.ProjectileType<FssIchorGlob>();
                for (int i = 0; i < FssDirector.DetonateSeedGlobs; i++) {
                    float ang = -MathHelper.Pi * (0.18f + 0.64f * i / (FssDirector.DetonateSeedGlobs - 1));
                    float speed = Main.rand.NextFloat(9f, 15f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center - new Vector2(0f, 30f),
                        ang.ToRotationVector2() * speed, type, damage, 0.5f, Main.myPlayer, 2f);
                }
            }
        }

        public override IFssState OnUpdate(FssStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            //落地闸门：未贴地先爬降，不走主时间线（空中入招时立腿剪影不许悬播）
            if (!grounded) {
                ctx.Mode = FssMoveMode.Crawl;
                ctx.CrawlSpeed = 3f;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.LegCommand = FssLegCommand.March;
                float surfaceY = FssVfx.FindGroundY(npc.Center - new Vector2(0f, 60f));
                approachTimer++;
                if (npc.Center.Y >= surfaceY - FssDirector.CrawlRideHeight - 50f || approachTimer > 40) {
                    grounded = true;
                }
                return null;
            }

            if (t < FssDirector.DetonateRaiseFrames) {
                //立起至全高：剪影预告
                ctx.Mode = FssMoveMode.Crawl;
                ctx.CrawlSpeed = 0f;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.LegCommand = FssLegCommand.Raise;
                float w = t / (float)FssDirector.DetonateRaiseFrames;
                ctx.FrontRaise = MathHelper.Clamp(w * 1.15f, 0f, 1f);
                ctx.CystGlow = Math.Max(ctx.CystGlow, w * 0.5f);
                if (seeded && !Main.dedServ && t % 8 == 0) {
                    FssVfx.FesterTrickle(MouthPos(npc), 1.8f);
                }
            }
            else if (t < ChargeEnd) {
                //怒吼蓄力：吼即引信起跑（全场池按距离次序上引信）
                HoldRaise(ctx);
                int local = t - FssDirector.DetonateRaiseFrames;
                float w = local / (float)FssDirector.DetonateChargeFrames;
                ctx.CystGlow = Math.Max(ctx.CystGlow, 0.5f + 0.5f * w);
                ctx.PulseKind = 1;
                ctx.PulsePhase = w;
                ctx.ShakeStrength = Math.Max(ctx.ShakeStrength, 0.3f + 0.5f * w);

                if (!ignited) {
                    ignited = true;
                    if (!Main.dedServ) {
                        FssVfx.Roar(npc.Center, -0.35f, 1.25f);
                        FssVfx.Shake(npc.Center, 7f, 1800f);
                        FssVfx.IchorBurst(npc.Center, 2.2f, -Vector2.UnitY);
                    }
                    //引信 = 蓄力剩余 + 距离/波速：蓄力结束那一刻波前正好从蟒身出发
                    FssIchorPool.IgniteAround(npc.Center, FssDirector.DetonateMaxRadius,
                        FssDirector.DetonateChargeFrames,
                        1f / FssDirector.DetonateWaveSpeed, tall: true);
                    waveWatch = FssDirector.DetonateChargeFrames
                        + (int)(FarthestPoolDist(npc.Center) / FssDirector.DetonateWaveSpeed) + 40;
                }
                //蓄力途中的次级隆隆
                if (!Main.dedServ && local % 9 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.5f + 0.4f * w, Pitch = -0.4f + 0.5f * w, MaxInstances = 4 }, npc.Center);
                }
            }
            else if (t < FssDirector.DetonateRaiseFrames + waveWatch) {
                //行波观察段：保持立姿看着自己的杰作滚过全场
                HoldRaise(ctx);
                ctx.CystGlow = Math.Max(ctx.CystGlow, 0.6f);
                //补种兜底：蓄力期间才落地的新池此刻收编进行波（已引燃的保留更短引信不受扰）
                if (!lateIgnited) {
                    lateIgnited = true;
                    FssIchorPool.IgniteAround(npc.Center, FssDirector.DetonateMaxRadius,
                        6, 1f / FssDirector.DetonateWaveSpeed, tall: true);
                }
                if (!Main.dedServ && t % 16 == 0) {
                    FssVfx.Shake(npc.Center, 1.8f, 2000f);
                }
            }
            else if (t < FssDirector.DetonateRaiseFrames + waveWatch + FssDirector.DetonateBreathFrames) {
                //整拍呼吸：落回爬姿，屏幕安静一口气（公平留白）
                ctx.Mode = FssMoveMode.Crawl;
                ctx.CrawlSpeed = 4f;
                ctx.CrawlDirX = FacingToTarget(ctx);
                ctx.LegCommand = FssLegCommand.March;
            }
            else {
                ctx.AttackCooldown = FssDirector.AttackCooldown(ctx.Phase) + 10;
                return EndAttack(ctx);
            }

            Timer++;
            //超时保险
            if (t > 420) {
                return EndAttack(ctx);
            }
            return null;
        }

        /// <summary>立姿保持</summary>
        private void HoldRaise(FssStateContext ctx) {
            ctx.Mode = FssMoveMode.Crawl;
            ctx.CrawlSpeed = 0f;
            ctx.CrawlDirX = ctx.CrawlDirX != 0f ? ctx.CrawlDirX : 1f;
            ctx.LegCommand = FssLegCommand.Raise;
            ctx.FrontRaise = 1f;
        }

        private static int CountPools() {
            int type = ModContent.ProjectileType<FssIchorPool>();
            int count = 0;
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == type) {
                    count++;
                }
            }
            return count;
        }

        private static float FarthestPoolDist(Vector2 from) {
            int type = ModContent.ProjectileType<FssIchorPool>();
            float best = 0f;
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == type) {
                    best = Math.Max(best, Vector2.Distance(p.Center, from));
                }
            }
            return Math.Min(best, FssDirector.DetonateMaxRadius);
        }
    }
}
