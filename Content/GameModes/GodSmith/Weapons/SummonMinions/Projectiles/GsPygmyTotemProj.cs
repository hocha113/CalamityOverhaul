using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 猎首战鼓图腾：矮人妖在集结旗点竖起的三头木雕柱。本体不伤害，
    /// 是猎首战鼓的光环载体（半径圈内自家仆从增伤，由方案侧查询判定）。
    /// 生命周期 = 破土升起 12 帧 / 战鼓循环（每 50 帧一记鼓点，脉环外扩 + 图腾目芒炽亮）
    /// / 末段 12 帧沉土散场。材质：丛林硬木雕柱 + 图腾目孔炬光 + 羽冠
    /// </summary>
    internal class GsPygmyTotemProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        /// <summary>战鼓光环半径（方案侧增伤查询用同一常量）</summary>
        internal const float AuraRadius = 190f;

        private const int TotemLife = 300;
        private const int RiseFrames = 12;
        private const int SinkFrames = 12;
        private const int DrumGap = 50;

        private static readonly Color WoodDark = new(96, 62, 38);
        private static readonly Color WoodMid = new(136, 92, 52);
        private static readonly Color TorchOrange = new(255, 156, 60);
        private static readonly Color FeatherGreen = new(112, 200, 96);

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.5471f % MathHelper.TwoPi;

        /// <summary>破土进度 0~1</summary>
        private float RiseT => MathHelper.Clamp(Life / RiseFrames, 0f, 1f);

        /// <summary>沉土收尾 1~0</summary>
        private float SinkT => MathHelper.Clamp(Projectile.timeLeft / (float)SinkFrames, 0f, 1f);

        /// <summary>本帧鼓点相位（0~1，1 为刚敲响）</summary>
        private float DrumT => 1f - Life % DrumGap / (float)DrumGap;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 86;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotemLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, TorchOrange.ToVector3() * 0.25f);
            //破土首帧：入土闷响 + 土屑迸出
            if (Life == 1f) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.8f, Pitch = -0.3f },
                    Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    Dust dirt = Dust.NewDustDirect(Projectile.Bottom - new Vector2(14f, 6f),
                        28, 8, DustID.Dirt, 0f, -2f, 60, default, 1.2f);
                    dirt.velocity *= 1.4f;
                }
            }
            //鼓点：脉环 + 目芒 + 鼓声（各端按同一 Life 节拍本地播放）
            if (Life > RiseFrames && Projectile.timeLeft > SinkFrames
                && Life % DrumGap == 0f) {
                SoundEngine.PlaySound(SoundID.Item53 with { Volume = 0.55f, Pitch = -0.55f },
                    Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    float ang = Seed + i / 8f * MathHelper.TwoPi;
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + ang.ToRotationVector2() * 20f,
                        ang.ToRotationVector2() * 3.2f, TorchOrange,
                        0.12f)?.Configure(16, 0.8f);
                }
            }
            //日常炬烬
            if (Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Top + new Vector2(Main.rand.NextFloat(-6f, 6f), 4f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.8f, 1.6f)),
                    TorchOrange, Main.rand.NextFloat(0.16f, 0.26f))?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            float present = RiseT * SinkT;
            //柱体从地面长出：以柱底为锚缩放高度
            Vector2 basePos = Projectile.Bottom - Main.screenPosition;
            float height = 78f * present;
            float sway = 0.02f * (float)Math.Sin(Life * 0.07f + Seed);

            //三段头雕：自底向上渐窄，木色分层（真 alpha 深浅两色叠出雕面）
            for (int i = 0; i < 3; i++) {
                float segH = height / 3f;
                float segW = (26f - i * 4f) * present;
                Vector2 segMid = basePos - new Vector2(0f, segH * (i + 0.5f));
                Main.EntitySpriteDraw(soft, segMid, null, WoodDark * 0.95f, sway,
                    soft.Size() / 2f, new Vector2(segW / soft.Width, (segH + 2f) / soft.Height),
                    SpriteEffects.None, 0);
                Main.EntitySpriteDraw(soft, segMid - new Vector2(segW * 0.14f, 0f), null,
                    WoodMid * 0.8f, sway, soft.Size() / 2f,
                    new Vector2(segW * 0.62f / soft.Width, (segH - 3f) / soft.Height),
                    SpriteEffects.None, 0);
                //目孔炬光：鼓点瞬间最亮（加色横缝）
                float eyeGlow = 0.35f + 0.65f * MathHelper.Clamp(DrumT * 1.6f - 0.6f, 0f, 1f);
                Main.EntitySpriteDraw(soft, segMid - new Vector2(0f, segH * 0.12f), null,
                    (TorchOrange with { A = 0 }) * (eyeGlow * present), sway,
                    soft.Size() / 2f, new Vector2(segW * 0.55f / soft.Width, 2.4f / soft.Height),
                    SpriteEffects.None, 0);
            }
            //顶部羽冠：五根羽片扇开
            for (int i = -2; i <= 2; i++) {
                float featherAng = -MathHelper.PiOver2 + i * 0.34f
                    + 0.05f * (float)Math.Sin(Life * 0.1f + Seed + i);
                Vector2 crown = basePos - new Vector2(0f, height);
                Main.EntitySpriteDraw(soft, crown, null, FeatherGreen * (0.7f * present),
                    featherAng, new Vector2(0f, soft.Height / 2f),
                    new Vector2(16f / soft.Width, 3f / soft.Height), SpriteEffects.None, 0);
            }
            //鼓点脉环：外扩的光环（加色，随相位衰减）
            if (Life > RiseFrames && DrumT > 0.55f) {
                float ringR = MathHelper.Lerp(0.2f, AuraRadius / 64f, 1f - DrumT * DrumT);
                Main.EntitySpriteDraw(glow, basePos - new Vector2(0f, height * 0.5f), null,
                    (TorchOrange with { A = 0 }) * (0.4f * DrumT * present), 0f,
                    glow.Size() / 2f, ringR, SpriteEffects.None, 0);
            }
            //基座底光
            Main.EntitySpriteDraw(glow, basePos, null,
                (TorchOrange with { A = 0 }) * (0.3f * present), 0f, glow.Size() / 2f,
                new Vector2(0.7f, 0.25f), SpriteEffects.None, 0);
            return false;
        }
    }
}
