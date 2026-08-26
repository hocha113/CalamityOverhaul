using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Yoyos
{
    /// <summary>
    /// 悠悠球族个性小弹：三风格弹丸（动脉血珠 / 叶列茨孢子 / 克眼血泪）。<br/>
    /// ai[0] = 风格（0 血珠抛物线 / 1 孢子缓漂 / 2 血泪直飞），随生成包同步；
    /// 伤害在生成时按各球条目折算烘焙。owner 端生成，命中判定 owner 权威
    /// </summary>
    internal class GsYoyoPelletProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithYoyos";

        internal const int StyleBloodBead = 0;
        internal const int StyleSpore = 1;
        internal const int StyleBloodTear = 2;

        private static readonly Color BeadRed = new(210, 40, 50);
        private static readonly Color SporeGreen = new(140, 230, 120);
        private static readonly Color TearCrimson = new(185, 30, 60);

        private int Style => (int)Projectile.ai[0];

        private Color StyleColor => Style switch {
            StyleSpore => SporeGreen,
            StyleBloodTear => TearCrimson,
            _ => BeadRed,
        };

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI() {
            //风格差异只在运动与配色，阶段全是确定函数
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Style == StyleSpore) {
                    Projectile.timeLeft = 50;
                }
            }
            switch (Style) {
                case StyleSpore:
                    Projectile.velocity *= 0.965f;
                    break;
                case StyleBloodTear:
                    Projectile.velocity *= 1.005f;
                    break;
                default:
                    Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.30f, 12f);
                    break;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, StyleColor.ToVector3() * 0.14f);

            if (VaultUtils.isServer) {
                return;
            }
            if (Main.rand.NextBool(5)) {
                if (Style == StyleSpore) {
                    PRTLoader.NewParticle<PRT_FarmSpore>(Projectile.Center, -Projectile.velocity * 0.1f,
                        SporeGreen, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 22), true);
                }
                else {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, -Projectile.velocity * 0.15f,
                        StyleColor, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(12, 18));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //弹体 = 双层软光点（黑底贴图 A=0 加色），亮核在上
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color body = StyleColor * 0.85f;
            body.A = 0;
            Color core = Color.White * 0.6f;
            core.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, body, 0f, glow.Size() / 2f, 0.22f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, core, 0f, glow.Size() / 2f, 0.10f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    Main.rand.NextVector2Circular(1.6f, 1.6f), StyleColor,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(Style != StyleSpore, Main.rand.Next(10, 18));
            }
        }
    }

    /// <summary>
    /// 悠悠球族驻留灼烧区：小瀑布折返火线段 / 冥火贴地火环。<br/>
    /// ai[0] = 风格（0 火线段 100×26 存活 0.5s / 1 贴地火环 170×28 存活 1.2s），
    /// 尺寸与寿命在 AI 首帧按 ai[0] 确定性落地，各端一致；
    /// idStatic 免疫 20t 防多段叠层挂机
    /// </summary>
    internal class GsYoyoBurnZoneProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithYoyos";

        internal const int StyleFireLine = 0;
        internal const int StyleGroundRing = 1;

        private static readonly Color FlameDeep = new(255, 96, 24);
        private static readonly Color FlameHot = new(255, 196, 96);

        private bool IsRing => (int)Projectile.ai[0] == StyleGroundRing;

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 72;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 20;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (IsRing) {
                    Projectile.Resize(170, 28);
                }
                else {
                    Projectile.timeLeft = 30;
                }
            }
            float env = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            Lighting.AddLight(Projectile.Center, FlameDeep.ToVector3() * (0.5f * env));

            if (VaultUtils.isServer) {
                return;
            }
            //火苗自区域底部窜起，持续期每帧 ≤2 粒
            float halfW = Projectile.width * 0.5f;
            Vector2 ground = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            if (Main.rand.NextBool(IsRing ? 2 : 3)) {
                PRTLoader.NewParticle<PRT_HellFire>(
                    ground + new Vector2(Main.rand.NextFloat(-halfW, halfW), -Main.rand.NextFloat(0f, 6f)),
                    new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(1.0f, 2.2f)),
                    Color.White, Main.rand.NextFloat(0.4f, 0.7f) * MathF.Max(0.4f, env));
            }
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    ground + new Vector2(Main.rand.NextFloat(-halfW, halfW), -4f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1.5f, 3f)),
                    FlameHot, Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //区域感靠火粒与一层贴地暖光，不画本体
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float env = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            Color c = FlameDeep * (0.35f * env);
            c.A = 0;
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, c, 0f,
                glow.Size() / 2f, new Vector2(Projectile.width / 42f, 0.5f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 代码2「二进制」折返镜像：与本体平行错位的实体化数据残影，50% 伤害。<br/>
    /// ai[0] = 悠悠球弹幕 type（绘制取贴图），ai[1] = 存活帧（随折返时长烘焙）。
    /// 用真弹幕而非手写线判定，联机命中权威天然正确
    /// </summary>
    internal class GsYoyoMirrorProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithYoyos";

        private static readonly Color DataCyan = new(90, 220, 235);

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 70;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                int life = (int)Projectile.ai[1];
                if (life > 4 && life < 120) {
                    Projectile.timeLeft = life;
                }
            }
            Projectile.rotation += 0.45f;
            Lighting.AddLight(Projectile.Center, DataCyan.ToVector3() * 0.2f);

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, -Projectile.velocity * 0.06f,
                    DataCyan, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(DataCyan, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int yoyoType = (int)Projectile.ai[0];
            if (yoyoType <= ProjectileID.None || yoyoType >= ProjectileLoader.ProjectileCount) {
                return false;
            }
            Main.instance.LoadProjectile(yoyoType);
            Texture2D tex = TextureAssets.Projectile[yoyoType].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //数据体：半透明本体 + 青色加色缘（identity 定相脉动）
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity * 0.9f);
            Color edge = DataCyan * (0.5f * pulse);
            edge.A = 0;
            Main.EntitySpriteDraw(tex, pos, null, edge, Projectile.rotation, tex.Size() / 2f, 1.15f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, Color.White * 0.45f, Projectile.rotation, tex.Size() / 2f, 1f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f), DataCyan, 0.6f)?.Configure(DataCyan, 16);
            }
        }
    }
}
