using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【神赋·盔甲】铂金套「王权时刻」：材质=冷贵金属的白辉，清贵克制。<br/>
    /// ①命中积攒铂辉，满 7 层后下一击开启王权时刻（5 秒窗口态）②窗口内每次命中自目标
    /// 放出一枚追踪铂星屑（在场上限 8）③窗口到时自然落幕，受击则立即落幕爆散铂白尘
    /// ④非窗口期受击崩落 2 层铂辉。<br/>
    /// 原版套装奖励保留，神赋是叠加层；层数与窗口态是攻击方端本地量，
    /// 跨端可见的部分是星屑实体
    /// </summary>
    internal class GsPlatinumArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.PlatinumHelmet];

        public override int BodyID => ItemID.PlatinumChainmail;

        public override int LegsID => ItemID.PlatinumGreaves;

        protected override string EndowLineFallback =>
            "Regal Hour: strikes build radiance; at 7 stacks the next strike begins the Regal Hour, every blow for 5 seconds looses a homing star shard, ending early if you are struck";

        //铂白冷辉色板
        internal static readonly Color PlatBlue = new(190, 205, 230);
        internal static readonly Color PlatWhite = new(232, 238, 248);
        internal static readonly Color PlatCore = new(255, 255, 255);

        /// <summary>开窗所需铂辉层数</summary>
        private const int FullCharge = 7;

        /// <summary>王权时刻窗口帧长（5 秒）</summary>
        private const int RegalFrames = 300;

        /// <summary>窗口内同时在场星屑上限，防高攻速武器刷屏</summary>
        private const int MaxStars = 8;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            //窗口态只在攻击方端存在（EndowFlag 仅在 OnEndowHitNPC 里置位）
            bool regal = state.EndowFlag && Main.GameUpdateCount < state.EndowTimer;

            //窗口到时自然落幕：收尾小演出
            if (state.EndowFlag && !regal) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = -0.3f }, player.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero, PlatBlue, 0.45f)
                        ?.Configure(0.15f, 1f, 16);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_Light>(player.Center + Main.rand.NextVector2Circular(14f, 20f),
                            new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)),
                            Main.rand.NextBool() ? PlatWhite : PlatBlue, Main.rand.NextFloat(0.08f, 0.13f))?.Configure(16, 0.7f);
                    }
                }
                state.EndowFlag = false;
                return;
            }

            //窗口期光环：头顶银白星点偶发 + 微光（个人读数，窗口态只在自己端存在）
            if (regal && !VaultUtils.isServer) {
                Lighting.AddLight(player.Center, PlatBlue.ToVector3() * 0.3f);
                if (Main.rand.NextBool(6)) {
                    Vector2 at = player.Top + new Vector2(Main.rand.NextFloat(-16f, 16f), -Main.rand.NextFloat(4f, 14f));
                    PRTLoader.NewParticle<PRT_Light>(at, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                        Main.rand.NextBool() ? PlatCore : PlatWhite, Main.rand.NextFloat(0.06f, 0.11f))?.Configure(14, 0.8f);
                }
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //星屑自身命中不喂层不再生星，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsPlatinumStarProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            bool regal = state.EndowFlag && Main.GameUpdateCount < state.EndowTimer;
            if (regal) {
                //窗口内：每击自目标放出一枚追踪星屑（在场上限内）
                SpawnStar(player, target, damageDone);
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //满层：这一击开启王权时刻
            state.EndowFlag = true;
            state.EndowTimer = Main.GameUpdateCount + RegalFrames;
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = 0.2f }, player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero, PlatWhite, 0.55f)
                    ?.Configure(0.18f, 1.3f, 18);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(player.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                        Main.rand.NextBool() ? PlatCore : PlatBlue, Main.rand.NextFloat(0.3f, 0.55f))
                        ?.Configure(false, Main.rand.Next(14, 24));
                }
            }
            //开窗的这一击也算窗口第一击，立即放出第一枚星屑
            SpawnStar(player, target, damageDone);
        }

        /// <summary>窗口内放星：owner 侧生成，星伤 15% 封 5..90，在场上限 8</summary>
        private static void SpawnStar(Player player, NPC target, int damageDone) {
            if (player.whoAmI != Main.myPlayer
                || player.ownedProjectileCounts[ModContent.ProjectileType<GsPlatinumStarProj>()] >= MaxStars) {
                return;
            }
            int starDamage = Math.Clamp((int)(damageDone * 0.15f), 5, 90);
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithPlatinumEndow"),
                target.Center + ang.ToRotationVector2() * 14f,
                ang.ToRotationVector2() * Main.rand.NextFloat(3.5f, 5f),
                ModContent.ProjectileType<GsPlatinumStarProj>(), starDamage, 1f, player.whoAmI);
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //窗口中受击：王权立即落幕，爆散一圈铂白尘
            bool regal = state.EndowFlag && Main.GameUpdateCount < state.EndowTimer;
            if (regal) {
                state.EndowFlag = false;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = -0.4f }, player.Center);
                    for (int i = 0; i < 12; i++) {
                        float ang = MathHelper.TwoPi * i / 12f;
                        Dust d = Dust.NewDustPerfect(player.Center, DustID.Platinum,
                            ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 4.5f), 80, default, Main.rand.NextFloat(1f, 1.4f));
                        d.noGravity = true;
                    }
                }
                return;
            }
            //蓄层期受击崩落两层铂辉
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Platinum, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 铂星屑：一粒冷贵金属铸出的四芒星，不是暖光点。缓慢自旋、微脉动，
    /// 飞行 8 帧散开后咬向最近目标；三层四芒星全加色叠层（铂淡蓝星辉/铂白主星/纯白小芯），
    /// 命中清脆星音，亡处铂辉散逸
    /// </summary>
    internal class GsPlatinumStarProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "StarTexture";

        /// <summary>散开段帧数，之后开始追踪</summary>
        private const int ScatterFrames = 8;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>自旋方向由 identity 定，全生命期稳定</summary>
        private float SpinDir => Projectile.identity % 2 == 0 ? 1f : -1f;

        /// <summary>出生 5 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 5f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 75;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //缓慢自旋，方向确定
            Projectile.rotation += 0.07f * SpinDir;

            //散开段之后咬向最近目标，转向率随时间收紧
            if (Life > ScatterFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 10f;
                    float turn = MathHelper.Clamp((Life - ScatterFrames) / 22f, 0.05f, 0.15f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.96f;
                }
            }

            //飞行相：星屑洒落铂辉微光
            if (!Main.dedServ && Life % 5 == 0) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center - Projectile.velocity * 0.5f,
                    Projectile.velocity * 0.08f,
                    Main.rand.NextBool(3) ? GsPlatinumArmor.PlatCore : GsPlatinumArmor.PlatBlue,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(Main.rand.Next(8, 14), 0.7f);
            }
            Lighting.AddLight(Projectile.Center, GsPlatinumArmor.PlatBlue.ToVector3() * (0.26f * VisualFade));
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 500f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //命中反馈：清脆星音 + 铂白迸屑
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.45f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool() ? GsPlatinumArmor.PlatCore : GsPlatinumArmor.PlatWhite,
                    Main.rand.NextFloat(0.28f, 0.48f))?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //星陨余痕：铂辉散逸比星体活得久
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsPlatinumArmor.PlatWhite, 0.13f)?.Configure(10, 0.6f);
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Platinum, Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.8f, 2.5f),
                    110, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
            }
        }

        //==================== 绘制：三层四芒星全加色 + 缓旋微脉动 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.StarTexture?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //星体微脉动，确定性相位
            float pulse = 1f + MathF.Sin(Life * 0.35f + Seed * 5f) * 0.08f;

            //铂淡蓝星辉：大而淡（黑底贴图一律 A=0 走加色观感）
            Main.EntitySpriteDraw(tex, pos, null, (GsPlatinumArmor.PlatBlue with { A = 0 }) * (0.4f * fade),
                Projectile.rotation, origin, 0.26f * pulse, SpriteEffects.None, 0);
            //铂白主星
            Main.EntitySpriteDraw(tex, pos, null, (GsPlatinumArmor.PlatWhite with { A = 0 }) * (0.85f * fade),
                Projectile.rotation, origin, 0.17f * pulse, SpriteEffects.None, 0);
            //纯白小芯：反向缓旋添层次
            Main.EntitySpriteDraw(tex, pos, null, (GsPlatinumArmor.PlatCore with { A = 0 }) * (0.7f * fade),
                -Projectile.rotation * 0.5f, origin, 0.08f * pulse, SpriteEffects.None, 0);
            return false;
        }
    }
}
