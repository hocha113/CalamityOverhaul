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
    /// 【神赋·盔甲】银套「银月环卫」：材质=圣银月光，清冷皎洁。<br/>
    /// ①命中积攒月辉，满 6 层后下一击召出一轮银月绕身环卫（在场上限 2，满编时层数攒着等空位）
    /// ②银月呼吸半径绕行、拖出褪色银弧③触敌即绽放银辉爆④受击崩落 2 层月辉（在场银月不消失）。<br/>
    /// 原版套装奖励保留，神赋是叠加层；层数是攻击方端本地量，跨端可见的部分是银月实体
    /// </summary>
    internal class GsSilverArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.SilverHelmet];

        public override int BodyID => ItemID.SilverChainmail;

        public override int LegsID => ItemID.SilverGreaves;

        protected override string EndowLineFallback =>
            "Silver Vigil: strikes build moonlight; at 6 stacks a silver moon rises to orbit you, bursting into radiance on contact";

        //圣银月光色板
        internal static readonly Color MoonBlue = new(150, 180, 220);
        internal static readonly Color SilverWhite = new(225, 232, 240);
        internal static readonly Color PureWhite = new(250, 252, 255);

        /// <summary>升月所需月辉层数</summary>
        private const int FullCharge = 6;

        /// <summary>同时在场银月上限</summary>
        private const int MaxMoons = 2;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //银月自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsSilverMoonProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //满编：这次命中不消耗层数，攒着等轨道空位
            if (player.ownedProjectileCounts[ModContent.ProjectileType<GsSilverMoonProj>()] >= MaxMoons) {
                return;
            }

            //升月：满层后这一击召出一轮绕身银月
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = -0.2f }, player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero, MoonBlue, 0.4f)
                    ?.Configure(0.15f, 1f, 14);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Light>(player.Center + Main.rand.NextVector2Circular(14f, 20f),
                        new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)),
                        Main.rand.NextBool() ? SilverWhite : MoonBlue, Main.rand.NextFloat(0.08f, 0.13f))?.Configure(16, 0.7f);
                }
            }
            //proc 弹幕 owner 侧生成；月伤 35% 封 10..120，出生轨道角随机（ai[0] 随生成包过线）
            if (player.whoAmI == Main.myPlayer) {
                int moonDamage = Math.Clamp((int)(damageDone * 0.35f), 10, 120);
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithSilverEndow"),
                    player.Center + ang.ToRotationVector2() * 68f, Vector2.Zero,
                    ModContent.ProjectileType<GsSilverMoonProj>(), moonDamage, 1.5f, player.whoAmI, ang);
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击崩落两层月辉（在场银月不消失）
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Silver, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 环卫银月：一轮清冷的圣银小月，不是光点。ai[0] 存轨道角每帧推进，
    /// 呼吸半径绕行佩戴者（确定性正弦），触敌即绽放银辉爆自灭；
    /// 三层月体（月蓝晕/银白珠/纯白芯）+ 轨道拖尾褪色银弧
    /// </summary>
    internal class GsSilverMoonProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>轨道角寄存器</summary>
        private ref float OrbitAngle => ref Projectile.ai[0];

        private ref float Life => ref Projectile.ai[1];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 5 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 5f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 480;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //轨道运动：角度每帧推进，呼吸半径确定性正弦，位置硬设不走速度积分
            OrbitAngle += 0.085f;
            float radius = 68f + MathF.Sin(Main.GameUpdateCount * 0.05f + Projectile.identity) * 6f;
            Projectile.Center = owner.Center + OrbitAngle.ToRotationVector2() * radius;
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = OrbitAngle;

            //飞行相：月身偶洒银辉微尘
            if (!Main.dedServ && Life % 6 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    DustID.Silver, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.6f)), 140, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GsSilverArmor.MoonBlue.ToVector3() * (0.28f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //触敌绽放银辉爆并自灭
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, GsSilverArmor.SilverWhite, 0.35f)
                    ?.Configure(0.12f, 0.9f, 14);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f),
                        Main.rand.NextBool() ? GsSilverArmor.PureWhite : GsSilverArmor.MoonBlue,
                        Main.rand.NextFloat(0.28f, 0.5f))?.Configure(false, Main.rand.Next(14, 24));
                }
            }
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //月落余痕：银尘缓散，比月体活得久
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Silver, Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.5f, 2f),
                    120, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsSilverArmor.SilverWhite, 0.12f)?.Configure(10, 0.6f);
        }

        //==================== 绘制：三层月体 + 轨道拖尾褪色银弧 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (tex == null || glow == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            //月体呼吸，确定性相位
            float breathe = 1f + MathF.Sin(Life * 0.4f + Seed * 5f) * 0.06f;

            //轨道拖尾：旧位置褪色银弧（黑底贴图一律 A=0 走加色观感）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.22f * fade;
                Vector2 gpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(glow, gpos, null, (GsSilverArmor.MoonBlue with { A = 0 }) * ghost,
                    0f, glowOrigin, 0.34f * (1f - i * 0.05f), SpriteEffects.None, 0);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //外月晕：月蓝大而淡，加色观感
            Main.EntitySpriteDraw(glow, pos, null, (GsSilverArmor.MoonBlue with { A = 0 }) * (0.45f * fade),
                0f, glowOrigin, 0.85f * breathe, SpriteEffects.None, 0);
            //银白珠体：真 alpha 正常叠层
            Main.EntitySpriteDraw(tex, pos, null, GsSilverArmor.SilverWhite * fade,
                Projectile.rotation, origin, new Vector2(0.2f, 0.2f) * breathe, SpriteEffects.None, 0);
            //纯白亮芯：加色小点
            Main.EntitySpriteDraw(tex, pos, null, (GsSilverArmor.PureWhite with { A = 0 }) * (0.7f * fade),
                Projectile.rotation, origin, new Vector2(0.09f, 0.09f) * breathe, SpriteEffects.None, 0);
            return false;
        }
    }
}
