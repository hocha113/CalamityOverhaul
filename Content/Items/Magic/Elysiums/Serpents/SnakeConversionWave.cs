using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Serpents
{
    /// <summary>
    /// 化蛇波潮：以施法点为心扩张的环形圣光波
    /// 波及的弱小敌人被就地化为圣蛇，其余敌人受到圣光伤害
    /// ai[0]=最大半径 ai[1]=蓄力比0~1
    /// </summary>
    internal class SnakeConversionWave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float MaxRadius => ref Projectile.ai[0];
        private ref float ChargeRatio => ref Projectile.ai[1];

        private const int ExpandTime = 30;
        private const int FadeTime = 14;
        private const int TotalLife = ExpandTime + FadeTime;
        //判定环带自前锋向内的宽度(像素)，可见拖裙比它更宽
        private const float HitBandWidth = 46f;
        private const int MaxServantSerpents = 8;

        private int Timer => TotalLife - Projectile.timeLeft;
        private float ExpandProgress => Math.Min(Timer / (float)ExpandTime, 1f);
        private float CurrentRadius => MaxRadius * VaultUtils.EaseOutCubic(ExpandProgress);

        //本波已转化数量(主人端统计)
        private int convertedCount;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//每个目标只被这道波击中一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Main.dedServ) {
                return;
            }

            //波前碎光：沿前锋圆周洒落圣尘
            float radius = CurrentRadius;
            if (radius > 30f && Timer <= ExpandTime) {
                int sparkCount = 2 + (int)(ChargeRatio * 2);
                for (int i = 0; i < sparkCount; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1.5f, 4f)
                        + (angle + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(-1f, 1f);
                    Color color = Color.Lerp(new Color(255, 224, 150), Color.White, Main.rand.NextFloat(0.5f));
                    PRTLoader.NewParticle<PRT_Light>(pos, vel, color, Main.rand.NextFloat(0.2f, 0.36f))
                        ?.Configure(Main.rand.Next(12, 20), 0.85f);
                }
            }

            float lightStrength = 0.8f * (1f - Timer / (float)TotalLife);
            Lighting.AddLight(Projectile.Center, lightStrength, lightStrength * 0.9f, lightStrength * 0.7f);
        }

        /// <summary>环带判定：与 shader 前锋同一半径来源</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Timer > ExpandTime + 2) {
                return false;//扩张结束后波前静止，不再结算
            }

            float radius = CurrentRadius;
            if (radius < 20f) {
                return false;
            }

            Vector2 center = Projectile.Center;
            Vector2 nearest = new(MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right)
                , MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            float dNear = Vector2.Distance(center, nearest);

            float dFar = 0f;
            dFar = Math.Max(dFar, Vector2.Distance(center, targetHitbox.TopLeft()));
            dFar = Math.Max(dFar, Vector2.Distance(center, targetHitbox.TopRight()));
            dFar = Math.Max(dFar, Vector2.Distance(center, targetHitbox.BottomLeft()));
            dFar = Math.Max(dFar, Vector2.Distance(center, targetHitbox.BottomRight()));

            return dNear <= radius + 8f && dFar >= radius - HitBandWidth;
        }

        /// <summary>可转化判定：非Boss且生命上限低于蓄力阈值</summary>
        private bool IsConvertible(NPC npc) {
            if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]) {
                return false;
            }
            if (!npc.CanBeChasedBy(Projectile) || npc.friendly || npc.type == NPCID.TargetDummy) {
                return false;
            }
            float threshold = 3000f + ChargeRatio * 5000f;
            return npc.lifeMax <= threshold;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (IsConvertible(target)) {
                //转化目标：无视防御并保证致死
                modifiers.DefenseEffectiveness *= 0f;
                modifiers.SourceDamage.Flat += target.lifeMax * 2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool converted = IsConvertible(target) && target.life <= 0;

            //转化演出：圣光柱升腾 + 十字星迸发(各端本地)
            if (!Main.dedServ) {
                int starCount = converted ? 8 : 4;
                for (int i = 0; i < starCount; i++) {
                    float angle = MathHelper.TwoPi * i / starCount;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                    Color color = Color.Lerp(new Color(255, 216, 130), Color.White, Main.rand.NextFloat(0.2f, 0.6f));
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center, vel, color, Main.rand.NextFloat(0.7f, 1.2f))
                        ?.Configure(false, Main.rand.Next(14, 22));
                }
                if (converted) {
                    //升天光柱：一串向上的光尘
                    for (int i = 0; i < 7; i++) {
                        Vector2 pos = target.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-8f, 8f));
                        Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(2.5f, 6f));
                        PRTLoader.NewParticle<PRT_Light>(pos, vel, new Color(255, 236, 180), Main.rand.NextFloat(0.28f, 0.5f))
                            ?.Configure(Main.rand.Next(22, 34), 0.95f);
                    }
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.9f, Pitch = 0.3f }, target.Center);
                }
            }

            //圣蛇只由主人端生成(弹幕命中本就在主人端结算，此处为保险门)
            if (!converted || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Player owner = Main.player[Projectile.owner];
            if (owner.ownedProjectileCounts[ModContent.ProjectileType<HolySerpent>()] + convertedCount >= MaxServantSerpents) {
                return;
            }

            convertedCount++;
            Vector2 spawnVel = new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(7f, 11f));
            int serpentDamage = (int)(Projectile.damage * 0.75f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, spawnVel
                , ModContent.ProjectileType<HolySerpent>(), serpentDamage, Projectile.knockBack * 0.5f
                , Projectile.owner, ChargeRatio);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.ElysiumStaff?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || canvas == null || noise == null) {
                return false;
            }

            float quadSize = (MaxRadius + 80f) * 2f;
            float radiusUv = CurrentRadius / quadSize;
            float fade = Timer <= ExpandTime
                ? 1f
                : 1f - (Timer - ExpandTime) / (float)FadeTime;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();

            effect.CurrentTechnique = effect.Techniques["ConversionWave"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["waveRadius"]?.SetValue(radiusUv);
            effect.Parameters["waveFade"]?.SetValue(fade);
            effect.Parameters["waveGrow"]?.SetValue(ExpandProgress);
            effect.Parameters["warmGold"]?.SetValue(new Vector3(1f, 0.863f, 0.588f));
            effect.Parameters["brightGold"]?.SetValue(new Vector3(1f, 0.784f, 0.392f));
            effect.Parameters["holyWhite"]?.SetValue(new Vector3(1f, 0.98f, 0.94f));

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White
                , 0f, canvas.Size() * 0.5f, quadSize, SpriteEffects.None, 0f);
            sb.End();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
