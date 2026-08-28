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
    /// 【神赋·盔甲】蜂套「蜂后近卫」（A 档）：材质=蜂蜡与琥珀的活物。<br/>
    /// ①命中积攒蜂酿（蜂套是召唤套：仆从与鞭的命中同样进本钩子，天然计入）
    /// ②满 8 层后下一击放出四只近卫蜂：自目标四周散开 10 帧再回咬，命中注毒
    /// ③近卫蜂蛇形飞行 + 高频振翅明灭，是活物不是光点
    /// ④受击惊散掉 2 层蜂酿，蜜色四溅。<br/>
    /// 原版套装奖励（蜂类伤害）保留，神赋是叠加层；蜂酿层数是攻击方端本地量，
    /// 满酿金辉只对佩戴者自己可见（个人读数），跨端可见的部分是近卫蜂实体
    /// </summary>
    internal class GsBeeArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.BeeHeadgear];

        public override int BodyID => ItemID.BeeBreastplate;

        public override int LegsID => ItemID.BeeGreaves;

        protected override string EndowLineFallback =>
            "Queen's Guard: strikes build brew; at 8 stacks the next strike releases four guard bees to maul the target";

        //蜂蜡琥珀色板
        internal static readonly Color WaxDark = new(92, 62, 24);
        internal static readonly Color Amber = new(212, 150, 48);
        internal static readonly Color HoneyBright = new(255, 214, 120);

        /// <summary>放蜂所需蜂酿层数</summary>
        private const int FullCharge = 8;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //满酿态：蜜色微光绕身（个人读数，层数只在攻击方端存在）
            Lighting.AddLight(player.Center, Amber.ToVector3() * 0.2f);
            if (Main.rand.NextBool(10)) {
                Vector2 at = player.Center + Main.rand.NextVector2CircularEdge(18f, 24f);
                PRTLoader.NewParticle<PRT_Light>(at, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f)),
                    HoneyBright, Main.rand.NextFloat(0.07f, 0.12f))?.Configure(14, 0.7f);
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //近卫蜂自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsBeeGuardProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //放蜂：满酿后这一击唤出四只近卫蜂
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
                for (int i = 0; i < 5; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(12f, 12f),
                        DustID.Honey, Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f));
                    d.noGravity = true;
                }
            }
            //proc 弹幕 owner 侧生成；每只伤害按触发伤害 12% 折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int beeDamage = Math.Clamp((int)(damageDone * 0.12f), 4, 60);
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(-0.3f, 0.3f);
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithBeeEndow"),
                        target.Center + ang.ToRotationVector2() * 12f,
                        ang.ToRotationVector2() * Main.rand.NextFloat(4f, 6f),
                        ModContent.ProjectileType<GsBeeGuardProj>(), beeDamage, 0.5f, player.whoAmI);
                }
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击惊散：蜂酿掉两层，蜜色四溅
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Honey, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 近卫蜂：一只蜂蜡琥珀的活物，不是追踪光点。自目标四周向外散开 10 帧再回咬；
    /// 蛇形摆动飞行 + 两侧翅斑高频振翅明灭，躯干三层叠色（焦纹压边/琥珀主体/蜜亮芯）
    /// + 速度轻拉伸，命中注毒迸蜜屑
    /// </summary>
    internal class GsBeeGuardProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>散开段帧数，之后强转向回咬</summary>
        private const int ScatterFrames = 10;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //散开段之后强转向回咬最近目标
            if (Life > ScatterFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 9f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.12f);
                }
                else {
                    Projectile.velocity *= 0.97f;
                }
            }
            //蛇形：每帧叠加垂直于速度方向的 sin 摆动，蜂的活物感
            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += perp * (MathF.Sin(Life * 0.35f + Seed) * 0.55f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, GsBeeArmor.Amber.ToVector3() * (0.12f * VisualFade));
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
            target.AddBuff(BuffID.Poisoned, 180);
            if (Main.dedServ) {
                return;
            }
            //命中反馈：蜇刺短响 + 蜜屑迸溅
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.3f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f),
                    Main.rand.NextBool() ? GsBeeArmor.HoneyBright : GsBeeArmor.Amber,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //余痕：蜜珠回落，比蜂体活得久
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    DustID.Honey, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 1.5f)));
                d.noGravity = false;
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsBeeArmor.HoneyBright, 0.1f)?.Configure(8, 0.6f);
        }

        //==================== 绘制：躯干三层叠色 + 双翅斑高频明灭 + 速度轻拉伸 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Texture2D wing = CWRAsset.SoftGlow?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 posDraw = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            //速度轻拉伸，飞得快躯干微微拉长
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.012f, 0f, 0.06f);

            //两侧翅斑：垂直于速度方向 ±6px，alpha 高频振翅明灭（黑底贴图走加色观感）
            if (wing != null) {
                float flap = 0.25f + 0.3f * MathF.Abs(MathF.Sin(Life * 1.8f + Seed));
                Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                Vector2 wingOrigin = wing.Size() * 0.5f;
                for (int side = -1; side <= 1; side += 2) {
                    Main.EntitySpriteDraw(wing, posDraw + perp * (6f * side), null,
                        (Color.White with { A = 0 }) * (flap * fade), rotation, wingOrigin,
                        new Vector2(0.14f, 0.10f), SpriteEffects.None, 0);
                }
            }

            //焦纹压边
            Main.EntitySpriteDraw(tex, posDraw, null, GsBeeArmor.WaxDark * (0.9f * fade), rotation, origin,
                new Vector2(0.16f, 0.20f + stretch), SpriteEffects.None, 0);
            //琥珀主体
            Main.EntitySpriteDraw(tex, posDraw, null, GsBeeArmor.Amber * fade, rotation, origin,
                new Vector2(0.12f, 0.15f + stretch * 0.8f), SpriteEffects.None, 0);
            //蜜亮芯，加色湿光
            Main.EntitySpriteDraw(tex, posDraw, null, (GsBeeArmor.HoneyBright with { A = 0 }) * (0.6f * fade),
                rotation, origin, new Vector2(0.06f, 0.09f + stretch * 0.4f), SpriteEffects.None, 0);
            return false;
        }
    }
}
