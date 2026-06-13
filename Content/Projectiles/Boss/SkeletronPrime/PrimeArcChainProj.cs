using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    /// <summary>
    /// 机械骷髅王电弧链锁：头臂间带伤害的高压电弧束带(TetherSpin 扇叶)
    /// <br/>ai[0] = 机械臂 NPC 的 whoAmI
    /// <br/>ai[1] = 头部 NPC 的 whoAmI
    /// <br/>ai[2] = 总持续时间（帧）
    /// <br/>前 <see cref="WarmupTime"/> 帧预警细弱无伤害；
    /// 头/臂失效或头部脱离 TetherSpin 时快速消散
    /// </summary>
    internal class PrimeArcChainProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;

        [VaultLoaden(CWRConstant.Masking + "ThunderTrail")]
        private static Asset<Texture2D> ThunderTex = null;

        /// <summary>预警帧数：链先以细弱形态拉起，给玩家定位扇区的时间</summary>
        internal static int WarmupTime => 30;
        /// <summary>消散帧数</summary>
        internal static int FadeTime => 12;
        /// <summary>电弧路径采样点数</summary>
        internal static int ArcPointCount => 14;
        /// <summary>碰撞宽度 px（满功率时）</summary>
        internal static float HitWidth => 32f;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Arm => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private NPC Head => ((int)Projectile.ai[1]).TryGetNPC(out NPC n) ? n : null;
        private int Duration => (int)Projectile.ai[2];

        private ThunderTrail mainTrail;
        private ThunderTrail coreTrail;
        private float power; //0~1 当前功率（展开/收束曲线）

        /// <summary>特斯拉橙金，区别于双子青蓝电弧</summary>
        internal static Color ArcColor => new(255, 168, 64);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1200;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC arm = Arm;
            NPC head = Head;

            //链锁有效性：头臂都活着且头部仍处于 TetherSpin
            bool hostValid = head.Alives() && head.type == NPCID.SkeletronPrime && arm.Alives()
                && (int)head.ai[PrimeAiSlots.HeadStateSlot] == (int)PrimeStateIndex.TetherSpin;
            if (!hostValid) {
                if (Timer < Duration - FadeTime) {
                    Timer = Duration - FadeTime;
                }
                if (!head.Alives() || !arm.Alives()) {
                    Projectile.Kill();
                    return;
                }
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.85f, Pitch = -0.1f }, Projectile.Center);
            }

            //锚定在头臂中点
            if (head.Alives() && arm.Alives()) {
                Projectile.Center = (head.Center + arm.Center) / 2f;
            }

            //功率曲线：预警期细弱 → 快速展开 → 收尾消散
            if (Timer < WarmupTime) {
                power = Timer / (float)WarmupTime * 0.25f;
            }
            else if (Timer >= Duration - FadeTime) {
                power = MathHelper.Lerp(1f, 0f, (Timer - (Duration - FadeTime)) / (float)FadeTime);
            }
            else {
                float t = MathHelper.Clamp((Timer - WarmupTime) / 12f, 0f, 1f);
                power = MathHelper.Lerp(0.25f, 1f, VaultUtils.EaseOutCubic(t));
            }

            //全功率瞬间的爆鸣
            if ((int)Timer == WarmupTime && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.15f }, Projectile.Center);
            }

            Timer++;
            if (Timer >= Duration) {
                Projectile.Kill();
                return;
            }

            if (VaultUtils.isServer || !head.Alives() || !arm.Alives()) {
                return;
            }

            BuildArcPath(head.Center, arm.Center);

            //沿线光照与飞溅火花
            for (int i = 0; i < 5; i++) {
                Lighting.AddLight(Vector2.Lerp(head.Center, arm.Center, i / 4f), ArcColor.ToVector3() * 0.55f * power);
            }
            if (power > 0.5f && Main.rand.NextBool(3)) {
                Vector2 sparkPos = Vector2.Lerp(head.Center, arm.Center, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(sparkPos,
                    Main.rand.NextVector2Circular(5f, 5f), Color.Gold, Main.rand.NextFloat(0.9f, 1.5f))?.Configure(false, 15);
            }
        }

        /// <summary>在头臂之间采样并扰动电弧路径（两端固定，中段正弦摆动）</summary>
        private void BuildArcPath(Vector2 start, Vector2 end) {
            Vector2[] points = new Vector2[ArcPointCount];
            Vector2 dir = end - start;
            Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            float waveSeed = Main.GlobalTimeWrappedHourly * 9f + Projectile.ai[0] * 1.7f;

            for (int i = 0; i < ArcPointCount; i++) {
                float t = i / (float)(ArcPointCount - 1);
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                float wave = (float)Math.Sin(waveSeed + t * 11f) * 15f * envelope * power;
                points[i] = start + dir * t + perp * wave;
            }

            if (mainTrail == null) {
                mainTrail = new ThunderTrail(ThunderTex, GetMainWidth, GetMainColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 3,
                };
                mainTrail.SetRange((0, 10));
                mainTrail.SetExpandWidth(6);

                coreTrail = new ThunderTrail(ThunderTex, GetCoreWidth, GetCoreColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                coreTrail.SetRange((0, 5));
                coreTrail.SetExpandWidth(3);
            }

            mainTrail.BasePositions = points;
            coreTrail.BasePositions = points;
            if ((int)Timer % 3 == 0) {
                mainTrail.RandomThunder();
                coreTrail.RandomThunder();
            }
        }

        private float GetMainWidth(float factor) => (17f + 9f * (float)Math.Sin(factor * MathHelper.Pi)) * power;
        private float GetCoreWidth(float factor) => (7f + 4f * (float)Math.Sin(factor * MathHelper.Pi)) * power;
        private Color GetMainColor(float factor) => ArcColor;
        private Color GetCoreColor(float factor) => Color.White;
        private float GetArcAlpha(float factor) => power;

        //预警期无伤害
        public override bool? CanDamage() => power >= 0.5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC head = Head;
            NPC arm = Arm;
            if (!head.Alives() || !arm.Alives()) {
                return false;
            }
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                head.Center, arm.Center, HitWidth * power, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Electrified, 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (power <= 0.02f) {
                return false;
            }

            NPC head = Head;
            NPC arm = Arm;
            bool anchored = head.Alives() && arm.Alives();

            //底层能量束带（着色器缺失时仅剩 ThunderTrail，仍可读）
            if (anchored && EffectLoader.PrimeArcChain?.Value != null) {
                DrawShaderRibbon(head.Center, arm.Center);
            }

            mainTrail?.DrawThunder(Main.instance.GraphicsDevice);
            coreTrail?.DrawThunder(Main.instance.GraphicsDevice);

            //两端连接点辉光
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color glowColor = ArcColor with { A = 0 };
            float pulse = 1f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 24f);
            if (head.Alives()) {
                Main.EntitySpriteDraw(glow, head.Center - Main.screenPosition, null, glowColor * power,
                    0f, glow.Size() / 2f, 1f * power * pulse, SpriteEffects.None, 0);
            }
            if (arm.Alives()) {
                Main.EntitySpriteDraw(glow, arm.Center - Main.screenPosition, null, glowColor * power,
                    0f, glow.Size() / 2f, 0.8f * power * pulse, SpriteEffects.None, 0);
            }

            return false;
        }

        /// <summary>电弧底层的体积束带：噪声游走亮带 + 行进光珠，由 PrimeArcChain 着色器绘制</summary>
        private void DrawShaderRibbon(Vector2 start, Vector2 end) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.PrimeArcChain.Value;
            shader.Parameters["uColor"]?.SetValue(ArcColor.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(new Vector3(1f, 0.95f, 0.75f));
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 1.5f);
            shader.Parameters["uIntensity"]?.SetValue(0.9f);
            shader.Parameters["uProgress"]?.SetValue(power);
            shader.Parameters["uSeed"]?.SetValue(Projectile.ai[0] % 7f / 7f);
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = CWRAsset.Placeholder_White.Value;
            Vector2 dir = end - start;
            float dist = dir.Length();
            sb.Draw(quad, start - Main.screenPosition, null, Color.White, dir.ToRotation(),
                new Vector2(0, quad.Height / 2f),
                new Vector2(dist / quad.Width, 110f / quad.Height),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
