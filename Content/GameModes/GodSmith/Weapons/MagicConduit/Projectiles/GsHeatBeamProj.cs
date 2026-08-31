using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles
{
    /// <summary>
    /// 热射线持续照射通道（Heat Ray 全接管，原版点射通道化）。
    /// 按住持续照射（高频 tick），白热带束宽 ×1.6，命中叠熔融，满 5 层小爆。<br/>
    /// 束端部收口纪律：源头钉在枪口、落点由 LaserScan 探地得出物理答案，
    /// 宽度有生命周期（展开 6t / 白热渐变 / 塌缩收口），判定宽 ≤ 可见亮体宽
    /// </summary>
    internal class GsHeatBeamProj : GsConduitHeldProj
    {
        private const float MaxLength = 1100f;
        private const float BaseWidth = 22f;
        private const float WhiteHotWidthMult = 1.6f;
        private const int GrowTicks = 6;

        protected override int BoundItemID => ItemID.HeatRay;
        protected override float ManaPerSecond => 7f;
        protected override float HeatPerTick => 1.2f;
        protected override int HitCooldown => 3;
        protected override float TickDamageCoef => 0.25f;
        protected override bool UseChannelFlag => false;//原版非 channel 物品，读 controlUseItem
        protected override float MuzzleOffset => 24f;

        /// <summary>束宽的本地平滑量（从同步热段确定性推进）</summary>
        private float widthCur = BaseWidth;
        /// <summary>探地后的实际束长</summary>
        private float beamLength = 60f;

        private readonly float[] laserSamples = new float[3];

        private float GrowProgress => MathHelper.Clamp(Projectile.localAI[1] / GrowTicks, 0f, 1f);

        /// <summary>当前可见核心宽（判定用 ×0.7 内收）</summary>
        private float VisWidth(float collapse01)
            => widthCur * VaultUtils.EaseOutCubic(GrowProgress) * (1f - VaultUtils.EaseInQuad(collapse01));

        private float lastCollapse01;

        protected override void ChannelAI(float collapse01) {
            lastCollapse01 = collapse01;
            float targetWidth = HeatStageSync >= 1 ? BaseWidth * WhiteHotWidthMult : BaseWidth;
            widthCur = MathHelper.Lerp(widthCur, targetWidth, 0.12f);

            //落点的物理答案：三线采样探地取平均
            Vector2 dir = AimUnit;
            Collision.LaserScan(Projectile.Center, dir, VisWidth(collapse01) * 0.5f, MaxLength, laserSamples);
            float sum = 0f;
            for (int i = 0; i < laserSamples.Length; i++) {
                sum += laserSamples[i];
            }
            beamLength = sum / laserSamples.Length;

            if (Projectile.localAI[1] == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.75f, Pitch = -0.3f }, Projectile.Center);
            }

            //沿束光照（各端）
            for (int i = 1; i <= 4; i++) {
                Lighting.AddLight(Projectile.Center + dir * (beamLength * i / 4f),
                    GsConduitVFX.ForgeMain.ToVector3() * 0.5f);
            }

            if (VaultUtils.isServer || collapse01 > 0.5f) {
                return;
            }
            if (Projectile.localAI[1] % 40 == 0) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.35f, Pitch = 0.2f, MaxInstances = 2 }, Projectile.Center);
            }
            //落点熔渣（端部有物理答案的收口演出，预算 ≤2/帧）
            Vector2 impact = Projectile.Center + dir * beamLength;
            if (beamLength < MaxLength - 8f && Main.GameUpdateCount % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(impact + Main.rand.NextVector2Circular(6f, 6f),
                    -dir.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? GsConduitVFX.ForgeBright : GsConduitVFX.ForgeMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 18));
            }
            //沿束浮渣
            if (Main.GameUpdateCount % 3 == 0) {
                float along = Main.rand.NextFloat(0.15f, 0.9f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + dir * beamLength * along,
                    dir * Main.rand.NextFloat(1f, 3f), GsConduitVFX.ForgeBright, Main.rand.NextFloat(0.16f, 0.26f))
                    ?.Configure(false, Main.rand.Next(6, 12));
            }
        }

        protected override bool? DamageGate() => GrowProgress >= 0.4f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + AimUnit * beamLength,
                VisWidth(lastCollapse01) * 0.7f, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //熔融叠层：满 5 层引爆小爆（只在攻击方端执行；小爆弹幕 owner 生成自然同步）
            GsConduitNPC g = target.GetGlobalNPC<GsConduitNPC>();
            int stacks = GsConduitNPC.Bump(ref g.MeltStacks, ref g.MeltLastTick, Main.GameUpdateCount, 1);
            if (stacks < 5) {
                return;
            }
            g.MeltStacks = 0;
            if (Projectile.owner == Main.myPlayer) {
                int burstDamage = Math.Max(1, (int)(Owner.GetWeaponDamage(Owner.HeldItem) * 1.2f));
                Projectile.NewProjectile(Owner.GetSource_Misc("GsConduitMelt"), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsConduitBurstProj>(), burstDamage, 5f, Projectile.owner,
                    70f + 1 * 1024f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //先画枪体（法杖斜握，Item.staff 名单成员），束与枪口辉光压在其上
            DrawWeaponBody();
            float vis = VisWidth(lastCollapse01);
            if (vis < 0.8f || beamLength < 12f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 dir = AimUnit;
            float rot = dir.ToRotation();
            bool whiteHot = HeatStageSync >= 1;
            float flick = 1f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 41f + Projectile.identity * 0.6f);

            GsConduitVFX.DrawBeam(sb, Projectile.Center, rot, beamLength, vis * flick,
                GsConduitVFX.ForgeMain, whiteHot ? Color.White : GsConduitVFX.ForgeBright);

            //枪口辉光收口（源头的物理答案）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 muzzle = Projectile.Center - Main.screenPosition;
            float muzzleScale = vis / 52f;
            sb.Draw(glow, muzzle, null, GsConduitVFX.ForgeMain with { A = 0 } * 0.9f, 0f,
                glow.Size() / 2f, muzzleScale * 1.9f * flick, SpriteEffects.None, 0f);
            sb.Draw(star, muzzle, null, GsConduitVFX.ForgeBright with { A = 0 } * 0.8f,
                Main.GlobalTimeWrappedHourly * 3.1f, star.Size() / 2f, muzzleScale * 0.5f, SpriteEffects.None, 0f);

            //落点辉光（端部收口）
            if (beamLength < MaxLength - 8f) {
                Vector2 impact = Projectile.Center + dir * beamLength - Main.screenPosition;
                sb.Draw(glow, impact, null, GsConduitVFX.ForgeBright with { A = 0 } * (0.85f * flick), 0f,
                    glow.Size() / 2f, muzzleScale * 1.3f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
