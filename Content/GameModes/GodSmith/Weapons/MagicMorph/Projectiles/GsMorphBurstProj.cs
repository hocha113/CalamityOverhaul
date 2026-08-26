using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 族内通用一次性 AoE 判定弹幕：血蚀引爆、恶魔叉阵落点小爆、雷雨落雷柱等。<br/>
    /// ai[0]=判定半径（px），ai[1]=样式索引（色板与柱形参数，见 <see cref="Presets"/>）；
    /// 出生随生成包定型，前 4 帧判定（每目标一次），其余帧只演出；
    /// 视觉是扩张冲击环 + 出生迸溅，与判定半径同源
    /// </summary>
    internal class GsMorphBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>样式：环三色 + 柱形半高（0=圆判定）+ 迸溅粒子色</summary>
        internal readonly record struct BurstStyle(Color Bright, Color Main, Color Deep, float ColumnHalfHeight);

        /// <summary>0=血红（血蚀引爆） 1=地狱橙（叉阵落点） 2=雷电蓝（落雷柱） 3=霜蓝（霜绽类）</summary>
        internal static readonly BurstStyle[] Presets = [
            new(new Color(255, 120, 120), new Color(205, 40, 52), new Color(96, 12, 24), 0f),
            new(new Color(255, 200, 120), new Color(240, 110, 40), new Color(120, 34, 10), 0f),
            new(new Color(200, 235, 255), new Color(96, 160, 255), new Color(30, 48, 130), 90f),
            new(new Color(215, 245, 255), new Color(120, 190, 240), new Color(40, 70, 140), 0f),
        ];

        private const int LifeTicks = 14;
        private const int DamageWindowEnd = 10;

        private float Radius => Projectile.ai[0] <= 0f ? 60f : Projectile.ai[0];

        private BurstStyle Style {
            get {
                int idx = (int)Projectile.ai[1];
                return idx >= 0 && idx < Presets.Length ? Presets[idx] : Presets[0];
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = LifeTicks;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Projectile.timeLeft > DamageWindowEnd ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 c = Projectile.Center;
            float half = Style.ColumnHalfHeight;
            if (half > 0f) {
                //柱形判定（落雷）：横向按半径、纵向按柱半高
                float nx = MathHelper.Clamp(c.X, targetHitbox.Left, targetHitbox.Right);
                float ny = MathHelper.Clamp(c.Y, targetHitbox.Top, targetHitbox.Bottom);
                return MathHelper.Distance(nx, c.X) <= Radius && MathHelper.Distance(ny, c.Y) <= half;
            }
            Vector2 nearest = new(
                MathHelper.Clamp(c.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(c.Y, targetHitbox.Top, targetHitbox.Bottom));
            return c.DistanceSQ(nearest) <= Radius * Radius;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SpawnBirthVisual();
                }
            }
        }

        /// <summary>出生迸溅（各端客户端；预算 ≤10 粒）</summary>
        private void SpawnBirthVisual() {
            BurstStyle st = Style;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.2f }, Projectile.Center);
            bool column = st.ColumnHalfHeight > 0f;
            int count = column ? 10 : 8;
            for (int i = 0; i < count; i++) {
                Vector2 vel = column
                    ? new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-6f, 6f))
                    : Main.rand.NextVector2Circular(4.5f, 4.5f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + vel * 2f, vel,
                    Main.rand.NextBool() ? st.Bright : st.Main, Main.rand.NextFloat(0.28f, 0.5f))
                    ?.Configure(!column, Main.rand.Next(12, 20));
            }
            Lighting.AddLight(Projectile.Center, st.Main.ToVector3() * 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            BurstStyle st = Style;
            //扩张环：半径 0.35R→R，透明度随寿命衰减（timeLeft 确定函数，各端一致）
            float t = 1f - Projectile.timeLeft / (float)LifeTicks;
            float r = Radius * (0.35f + 0.65f * MathHelper.Clamp(t * 2.2f, 0f, 1f));
            float alpha = 0.85f * (1f - t);
            if (st.ColumnHalfHeight > 0f) {
                //落雷柱：纵向拉伸的窄环两枚叠出电柱轮廓
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, st.ColumnHalfHeight * (0.6f + 0.4f * t), 6f,
                    st.Bright, st.Main, st.Deep, alpha, squish: 0.22f, timeSeed: Projectile.identity * 0.41f);
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, r, 5f,
                    st.Bright, st.Main, st.Deep, alpha * 0.7f, squish: 0.5f, timeSeed: Projectile.identity * 0.77f);
            }
            else {
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, r, 6f,
                    st.Bright, st.Main, st.Deep, alpha, innerGlow: 0.25f, timeSeed: Projectile.identity * 0.41f);
            }
            return false;
        }

        /// <summary>便捷生成：折算伤害并封顶，防终局武器把小爆叠出天文数字（仅本地玩家/攻击方路径调用）</summary>
        internal static void Spawn(Projectile source, Vector2 pos, int damage, float radius, int styleIdx) {
            int dmg = (int)MathHelper.Clamp(damage, 1, 1200);
            Projectile.NewProjectile(source.GetSource_FromAI(), pos, Vector2.Zero,
                ModContent.ProjectileType<GsMorphBurstProj>(), dmg, 2f, source.owner, radius, styleIdx);
        }
    }
}
