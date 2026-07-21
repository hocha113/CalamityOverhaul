using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles
{
    /// <summary>热射线基类；ai[0]宿主，ai[1]起始角，ai[2]角速度(0定向)；未展开无伤</summary>
    internal abstract class PrimeHeatRayBase : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>全功率碰撞宽</summary>
        protected abstract float MaxWidth { get; }
        /// <summary>光束长</summary>
        protected abstract float MaxLength { get; }
        /// <summary>全功率帧</summary>
        protected abstract int SustainFrames { get; }
        protected virtual int ExpandTime => 10;
        protected virtual int CollapseTime => 12;
        protected virtual Color ThemeColor => new(255, 86, 22);
        protected virtual Color ThemeGlow => new(255, 212, 120);
        /// <summary>出生音效，null则静默</summary>
        protected virtual SoundStyle? BirthSound => SoundID.Zombie104 with { Volume = 0.75f, Pitch = 0.1f, MaxInstances = 3 };

        internal int TotalLife => ExpandTime + SustainFrames + CollapseTime;

        protected ref float Timer => ref Projectile.localAI[0];
        protected NPC Host => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;

        protected float beamWidth;
        protected float beamLength;

        /// <summary>宿主有效性</summary>
        protected abstract bool HostValid();
        /// <summary>更新束根位置</summary>
        protected virtual void UpdateAnchor() { }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;//实际生命由 TotalLife 裁决
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (!HostValid() && Timer < TotalLife - CollapseTime) {
                Timer = TotalLife - CollapseTime;
            }

            if (Timer == 0 && !VaultUtils.isServer && BirthSound.HasValue) {
                SoundEngine.PlaySound(BirthSound.Value, Projectile.Center);
            }

            //扫射角
            float sweepT = MathHelper.Clamp(Timer - ExpandTime, 0f, SustainFrames);
            Projectile.rotation = Projectile.ai[1] + Projectile.ai[2] * sweepT;

            UpdateAnchor();

            //宽度缓动
            float collapseStart = TotalLife - CollapseTime;
            if (Timer < ExpandTime) {
                float t = Timer / ExpandTime;
                beamWidth = MathHelper.Lerp(2f, MaxWidth, VaultUtils.EaseOutCubic(t));
                beamLength = MathHelper.Lerp(0f, MaxLength, VaultUtils.EaseOutQuad(t));
            }
            else if (Timer >= collapseStart) {
                float t = (Timer - collapseStart) / CollapseTime;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(t));
                beamLength = MaxLength;
            }
            else {
                beamWidth = MaxWidth;
                beamLength = MaxLength;
            }
            beamWidth *= 1f + 0.06f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 36f);

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            //沿束光照
            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 5; i++) {
                Lighting.AddLight(Projectile.Center + beamDir * (beamLength / 5f * i), ThemeColor.ToVector3() * 0.7f);
            }

            if (VaultUtils.isServer || beamWidth < MaxWidth * 0.3f) {
                return;
            }

            //沿线火花
            if (Main.rand.NextBool(3)) {
                float along = Main.rand.NextFloat();
                Vector2 sparkPos = Projectile.Center + beamDir * beamLength * along
                    + beamDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.4f, beamWidth * 0.4f);
                PRTLoader.NewParticle<PRT_Spark>(sparkPos,
                    beamDir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(2f, 6f),
                    Color.Gold, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(false, 14);
            }
        }

        //未展开无伤
        public override bool? CanDamage() => beamWidth >= MaxWidth * 0.6f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength,
                beamWidth * 0.7f, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (beamWidth <= 0.5f || beamLength <= 10f) {
                return false;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation;
            float flicker = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 43f);

            Color outer = ThemeColor with { A = 0 };
            Color mid = ThemeGlow with { A = 0 };
            Color core = Color.White with { A = 0 };

            if (EffectLoader.PrimeSkullBeam?.Value != null) {
                DrawShaderBeam(rot);
            }
            else {
                DrawFallbackBeam(drawPos, rot, outer, mid, core, flicker);
            }

            //枪口辉光随宽缩放
            float muzzleScale = beamWidth / 64f;
            Main.EntitySpriteDraw(glow, drawPos, null, outer * 0.95f, 0f, glow.Size() / 2f,
                muzzleScale * 2.2f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, core, 0f, glow.Size() / 2f,
                muzzleScale * 1.05f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, mid * 0.85f, Main.GlobalTimeWrappedHourly * 3.4f,
                star.Size() / 2f, muzzleScale * 0.6f * flicker, SpriteEffects.None, 0);

            return false;
        }

        private void DrawShaderBeam(float rot) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.PrimeSkullBeam.Value;
            float expandProgress = MathHelper.Clamp(Timer / ExpandTime, 0f, 1f);
            shader.Parameters["uColor"]?.SetValue(ThemeColor.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(ThemeGlow.ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 1.7f);
            shader.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f));
            shader.Parameters["uIntensity"]?.SetValue(1.1f);
            shader.Parameters["uExpandProgress"]?.SetValue(expandProgress);
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);
            shader.Parameters["uImage2"]?.SetValue(CWRAsset.PerlinNoise.Value);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            //视觉宽>碰撞宽
            float visualWidth = beamWidth * 3.6f;
            sb.Draw(quad, Projectile.Center - Main.screenPosition, null, Color.White, rot,
                new Vector2(0, quad.Height / 2f),
                new Vector2(beamLength / quad.Width, visualWidth / quad.Height),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawFallbackBeam(Vector2 drawPos, float rot, Color outer, Color mid, Color core, float flicker) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 lineOrigin = new(0, line.Height / 2f);
            float lenScale = beamLength / line.Width;

            Main.EntitySpriteDraw(line, drawPos, null, outer * 0.45f, rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 3.2f * flicker), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, Color.Lerp(outer, mid, 0.5f) * 0.85f, rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 1.7f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, core * 0.95f, rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 0.8f * flicker), SpriteEffects.None, 0);
        }
    }

    /// <summary>十字绞杀热射线，钉预警线原位</summary>
    internal class PrimeCrossBeamProj : PrimeHeatRayBase
    {
        protected override float MaxWidth => 50f;
        protected override float MaxLength => 1470f;
        protected override int SustainFrames => 68;
        protected override Color ThemeColor => new(255, 56, 26);
        protected override Color ThemeGlow => new(255, 186, 96);
        //音效由状态侧播一次
        protected override SoundStyle? BirthSound => null;

        protected override bool HostValid() {
            NPC head = Host;
            return head.Alives() && head.type == NPCID.SkeletronPrime
                && (int)head.ai[PrimeAiSlots.HeadStateSlot] == (int)PrimeStateIndex.CrossExecute;
        }
    }

    /// <summary>炮臂热射线，ai[2]=0定向</summary>
    internal class PrimeArmHeatRayProj : PrimeHeatRayBase
    {
        /// <summary>扫射全功率帧</summary>
        internal static int SweepSustain => 70;
        /// <summary>炮口前伸</summary>
        internal static float MuzzleOffset => 70f;

        protected override float MaxWidth => 38f;
        protected override float MaxLength => 1900f;
        protected override int SustainFrames => Projectile.ai[2] != 0f ? SweepSustain : 42;

        protected override bool HostValid() {
            NPC arm = Host;
            if (!arm.Alives() || arm.type != NPCID.PrimeLaser) {
                return false;
            }
            //出生宽限，等ai同步
            if (Timer < 8) {
                return true;
            }
            //切出发射则收束
            int armState = (int)arm.ai[PrimeAiSlots.ArmStateSlot];
            if (armState != (int)PrimeArmStateIndex.LaserSweep
                && armState != (int)PrimeArmStateIndex.LaserChargedShot) {
                return false;
            }
            //转阶段收束
            ((int)arm.ai[PrimeAiSlots.ArmHeadIndex]).TryGetNPC(out NPC head);
            return !(head.Alives() && head.type == NPCID.SkeletronPrime
                && (int)head.ai[PrimeAiSlots.HeadStateSlot] == (int)PrimeStateIndex.PhaseTransition);
        }

        protected override void UpdateAnchor() {
            NPC arm = Host;
            if (arm.Alives()) {
                Projectile.Center = arm.Center + Projectile.rotation.ToRotationVector2() * MuzzleOffset;
            }
        }
    }
}
