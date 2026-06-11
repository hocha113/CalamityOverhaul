using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.Destroyer
{
    /// <summary>
    /// 机械电弧：连接两个NPC（体节/探针）之间的高温电弧，纯演出无伤害。
    /// 服务端生成同步，客户端用 <see cref="ThunderTrail"/> 绘制抖动闪电。
    /// <br/>ai[0]: 端点NPC A 索引
    /// <br/>ai[1]: 端点NPC B 索引
    /// </summary>
    internal class DestroyerArc : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxLife = 30;
        private const int ArcPointCount = 7;

        private ThunderTrail arcTrail;
        private readonly Vector2[] arcPoints = new Vector2[ArcPointCount];
        private float arcWidth;
        private float arcAlpha;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
        }

        public override bool ShouldUpdatePosition() => false;

        private float WidthFunc(float factor) => (float)Math.Sin(factor * MathHelper.Pi) * arcWidth;
        private Color ColorFunc(float factor) => new Color(255, 150, 70);
        private float AlphaFunc(float factor) => arcAlpha;

        public override void AI() {
            NPC a = CWRUtils.GetNPCInstance((int)Projectile.ai[0]);
            NPC b = CWRUtils.GetNPCInstance((int)Projectile.ai[1]);
            if (!a.Alives() || !b.Alives()) {
                Projectile.Kill();
                return;
            }

            //首帧各端本地播放电弧噼啪声
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                if (!VaultUtils.isServer) {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                        Volume = 0.5f,
                        Pitch = -0.25f + Main.rand.NextFloat(-0.1f, 0.2f),
                        MaxInstances = 6
                    }, Projectile.Center);
                }
            }

            Projectile.Center = Vector2.Lerp(a.Center, b.Center, 0.5f);

            float life = 1f - Projectile.timeLeft / (float)MaxLife;
            float fade = Math.Min(MathHelper.Clamp(life * 5f, 0f, 1f),
                MathHelper.Clamp(Projectile.timeLeft / 9f, 0f, 1f));
            float dist = Vector2.Distance(a.Center, b.Center);
            arcWidth = MathHelper.Clamp(dist * 0.045f, 5f, 15f) * fade;
            arcAlpha = fade;

            if (VaultUtils.isServer) {
                return;
            }

            //端点间采样并周期性抖动重建
            for (int i = 0; i < ArcPointCount; i++) {
                arcPoints[i] = Vector2.Lerp(a.Center, b.Center, i / (float)(ArcPointCount - 1));
            }

            arcTrail ??= new ThunderTrail(CWRAsset.ThunderTrail, WidthFunc, ColorFunc, AlphaFunc) {
                CanDraw = true,
                UseNonOrAdd = true,
                PartitionPointCount = 3
            };
            arcTrail.BasePositions = arcPoints;
            if (Projectile.timeLeft % 3 == 0 || Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                arcTrail.SetRange((0, MathHelper.Clamp(dist * 0.08f, 6f, 26f)));
                arcTrail.SetExpandWidth(4);
                arcTrail.RandomThunder();
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.35f, 0.12f) * fade);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (arcTrail == null) {
                return false;
            }
            arcTrail.DrawThunder(Main.instance.GraphicsDevice);

            //端点小光斑
            Texture2D glow = CWRAsset.SoftGlow.Value;
            NPC a = CWRUtils.GetNPCInstance((int)Projectile.ai[0]);
            NPC b = CWRUtils.GetNPCInstance((int)Projectile.ai[1]);
            Color c = new Color(255, 170, 90, 0) * (arcAlpha * 0.75f);
            if (a.Alives()) {
                Main.EntitySpriteDraw(glow, a.Center - Main.screenPosition, null, c, 0f, glow.Size() / 2f, 0.7f, SpriteEffects.None, 0);
            }
            if (b.Alives()) {
                Main.EntitySpriteDraw(glow, b.Center - Main.screenPosition, null, c, 0f, glow.Size() / 2f, 0.7f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
