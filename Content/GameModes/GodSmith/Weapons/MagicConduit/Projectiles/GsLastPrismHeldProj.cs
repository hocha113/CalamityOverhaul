using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 最后棱镜「聚焦」通道：六股彩束随热量收拢，白热合为一根白炽主束。<br/>
    /// 聚焦度各端确定性重建：出生热量随 MarkData 过线 + 引导帧数（localAI[1] 各端自增）
    /// × 固定积热率折算，owner 的真热量只用于伤害与泄压裁决；
    /// 束长每束 LaserScan 探物理落点，判定几何与绘制几何同源
    /// </summary>
    internal class GsLastPrismHeldProj : GsConduitHeldProj
    {
        private const float MaxLength = 1360f;
        private const int BeamCount = 6;
        /// <summary>散束半张角（弧度），聚焦时收拢到 0</summary>
        private const float MaxSplay = 0.5f;
        private const float SplayWidth = 9f;
        private const float FocusWidth = 26f;
        private const int GrowTicks = 8;

        public override string LocalizationCategory => "GodSmithMagicConduit";

        protected override int BoundItemID => ItemID.LastPrism;
        protected override float HeatPerTick => HeatRate;
        protected override int HitCooldown => 3;
        protected override float TickDamageCoef => 0.3f;
        protected override float MuzzleOffset => 18f;
        protected override int CollapseTicks => 10;

        internal const float HeatRate = 0.75f;

        /// <summary>棱镜本体悬在束根，贴图上端为前（镜像原版宿主 633 的 +π/2 约定）</summary>
        protected override GsConduitBodyPose BodyPose => GsConduitBodyPose.MuzzleForward;

        /// <summary>持续蓝耗：顶格 NoBreak 只涨蓝耗（经典不毁）</summary>
        protected override float ManaPerSecond
            => Owner.GetModPlayer<GsHeatPlayer>().Heat >= GsHeatPlayer.HeatMax ? 17f : 11f;

        /// <summary>各束长度（LaserScan 落点缓存）</summary>
        private readonly float[] beamLengths = new float[BeamCount];
        private readonly float[] laserSamples = new float[3];

        /// <summary>泄压折束令（方案 FireVent 调用，owner 端）</summary>
        internal void RequestCollapse() => BeginCollapse();

        private float GrowProgress => MathHelper.Clamp(Projectile.localAI[1] / GrowTicks, 0f, 1f);

        /// <summary>
        /// 聚焦度 0~1（各端同式重建：出生热量 MarkData + 引导帧 × 积热率，引导中不冷却故单调）
        /// </summary>
        private float Focus01 {
            get {
                float born = Projectile.TryGetGlobalProjectile(out GodSmithProjRouter router) ? router.MarkData : 0f;
                float est = MathHelper.Clamp(born + Projectile.localAI[1] * HeatRate, 0f, GsHeatPlayer.HeatMax);
                return VaultUtils.EaseQuadInOut(est / GsHeatPlayer.HeatMax);
            }
        }

        /// <summary>第 i 束的方向（散束按聚焦度收拢）</summary>
        private Vector2 BeamDir(int i, float focus, float collapse01) {
            float lane = BeamCount <= 1 ? 0f : i / (BeamCount - 1f) * 2f - 1f;
            //塌缩时束再度散开（断束的「散瓣」余韵）
            float splay = MaxSplay * (1f - focus) + collapse01 * 0.35f;
            float wobble = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + i * 2.1f + Projectile.identity * 0.5f)
                * 0.05f * (1f - focus);
            return AimUnit.RotatedBy(lane * splay + wobble);
        }

        /// <summary>当前每束可见宽（判定 ×0.75 内收）</summary>
        private float VisWidth(float focus, float collapse01)
            => MathHelper.Lerp(SplayWidth, FocusWidth, focus)
                * VaultUtils.EaseOutCubic(GrowProgress) * (1f - VaultUtils.EaseInQuad(collapse01));

        private float lastCollapse01;

        protected override void ChannelAI(float collapse01) {
            lastCollapse01 = collapse01;
            float focus = Focus01;
            Vector2 muzzle = Projectile.Center;

            //各束探物理落点
            for (int i = 0; i < BeamCount; i++) {
                Vector2 dir = BeamDir(i, focus, collapse01);
                Collision.LaserScan(muzzle, dir, VisWidth(focus, collapse01) * 0.5f, MaxLength, laserSamples);
                float sum = 0f;
                for (int s = 0; s < laserSamples.Length; s++) {
                    sum += laserSamples[s];
                }
                beamLengths[i] = sum / laserSamples.Length;
            }

            if (Projectile.localAI[1] == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.8f, Pitch = 0.35f }, muzzle);
            }
            //聚焦升调滴答：随聚焦度升高的棱鸣（施法者本地即可感的读数）
            if (!VaultUtils.isServer && Projectile.localAI[1] % 32 == 0 && collapse01 <= 0f) {
                SoundEngine.PlaySound(SoundID.Item101 with {
                    Volume = 0.28f + 0.2f * focus,
                    Pitch = -0.4f + 0.9f * focus,
                    MaxInstances = 2
                }, muzzle);
            }

            //沿束光照
            for (int i = 0; i < BeamCount; i += 2) {
                Vector2 dir = BeamDir(i, focus, collapse01);
                Lighting.AddLight(muzzle + dir * beamLengths[i] * 0.5f, HueOf(i).ToVector3() * 0.35f);
            }
            Lighting.AddLight(muzzle, Color.White.ToVector3() * (0.3f + 0.4f * focus));

            if (VaultUtils.isServer || collapse01 > 0.5f) {
                return;
            }
            //飞行相：束身棱尘（沿随机一束漂散的光屑）
            if (Main.GameUpdateCount % 2 == 0) {
                int i = Main.rand.Next(BeamCount);
                Vector2 dir = BeamDir(i, focus, collapse01);
                float along = Main.rand.NextFloat(0.1f, 0.95f);
                PRTLoader.NewParticle<PRT_Sparkle>(muzzle + dir * beamLengths[i] * along,
                    dir.RotatedBy(MathHelper.PiOver2 * (Main.rand.NextBool() ? 1 : -1)) * Main.rand.NextFloat(0.4f, 1.2f),
                    focus > 0.85f ? Color.White : HueOf(i), Main.rand.NextFloat(0.25f, 0.4f))
                    ?.Configure(HueOf(i), Main.rand.Next(8, 14), 0.06f, 0.7f);
            }
            //命中相：主落点白炽迸溅（聚焦越深越盛）
            int main = BeamCount / 2;
            Vector2 mainDir = BeamDir(main, focus, collapse01);
            if (beamLengths[main] < MaxLength - 8f && Main.GameUpdateCount % 2 == 0) {
                Vector2 impact = muzzle + mainDir * beamLengths[main];
                PRTLoader.NewParticle<PRT_Spark>(impact + Main.rand.NextVector2Circular(5f, 5f),
                    -mainDir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 4.5f + 3f * focus),
                    Main.rand.NextBool() ? Color.White : HueOf(Main.rand.Next(BeamCount)),
                    Main.rand.NextFloat(0.26f, 0.44f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        protected override void OwnerExtraTick(GsHeatPlayer hp) {
            //聚焦浓缩：散束压低单束伤、聚束抬升（owner 端真值路径）
            float focus = Focus01;
            Projectile.damage = Math.Max(1, (int)(Projectile.damage * (0.78f + 0.5f * focus)));

            //白热折射：主束落点每 9t 折出两道棱光碎束
            if (hp.InWhiteHot && Projectile.localAI[1] % 9 == 0) {
                int main = BeamCount / 2;
                if (beamLengths[main] < MaxLength - 8f) {
                    Vector2 dir = BeamDir(main, focus, 0f);
                    Vector2 impact = Projectile.Center + dir * (beamLengths[main] - 6f);
                    int shardDamage = Math.Max(1, (int)(Owner.GetWeaponDamage(Owner.HeldItem) * 0.35f));
                    for (int s = 0; s < 2; s++) {
                        float bend = (s == 0 ? 1f : -1f) * (0.5f + 0.3f * Hash01(s));
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), impact,
                            dir.RotatedBy(bend) * 24f, ModContent.ProjectileType<GsLastPrismShardProj>(),
                            shardDamage, 1.5f, Projectile.owner, Main.rand.Next(BeamCount));
                    }
                }
            }
        }

        /// <summary>identity 定相伪随机（绘制/几何路径禁 Main.rand）</summary>
        private float Hash01(int salt) {
            uint h = (uint)(Projectile.identity * 747796405 + salt * 2891336453);
            h = (h >> 13) ^ h;
            return ((h * 1274126177u) & 0xFFFFFF) / 16777216f;
        }

        protected override bool? DamageGate() => GrowProgress >= 0.5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float focus = Focus01;
            float vis = VisWidth(focus, lastCollapse01);
            float point = 0f;
            //聚束近乎重合时只查主束，散束逐束查线
            if (focus >= 0.92f) {
                int main = BeamCount / 2;
                Vector2 dir = BeamDir(main, focus, lastCollapse01);
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    Projectile.Center, Projectile.Center + dir * beamLengths[main], vis * 0.75f, ref point);
            }
            for (int i = 0; i < BeamCount; i++) {
                Vector2 dir = BeamDir(i, focus, lastCollapse01);
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    Projectile.Center, Projectile.Center + dir * beamLengths[i], vis * 0.75f, ref point)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>色环第 i 束的谱色</summary>
        internal static Color HueOf(int i) => Main.hslToRgb(i / (float)BeamCount * 0.92f, 0.85f, 0.6f);

        public override bool PreDraw(ref Color lightColor) {
            //先画棱镜本体（原版由宿主 633 自绘，接管后由此补回），束与辉体压在其上
            DrawWeaponBody();
            float focus = Focus01;
            float vis = VisWidth(focus, lastCollapse01);
            if (vis < 0.8f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 muzzleWorld = Projectile.Center;
            float flick = 1f + 0.07f * MathF.Sin(Main.GlobalTimeWrappedHourly * 47f + Projectile.identity * 0.8f);

            //六束彩光：散开各持本色，聚拢彼此叠白
            for (int i = 0; i < BeamCount; i++) {
                Vector2 dir = BeamDir(i, focus, lastCollapse01);
                Color hue = HueOf(i);
                float w = vis * (focus >= 0.92f ? 0.55f : 0.8f) * flick;
                GsConduitVFX.DrawBeam(sb, muzzleWorld, dir.ToRotation(), beamLengths[i], w,
                    hue, Color.Lerp(hue, Color.White, 0.5f), MathHelper.Lerp(0.62f, 0.4f, focus));
            }
            //聚焦主束：白炽核压顶
            if (focus > 0.55f) {
                int main = BeamCount / 2;
                Vector2 dir = BeamDir(main, focus, lastCollapse01);
                float coreStrength = (focus - 0.55f) / 0.45f;
                GsConduitVFX.DrawBeam(sb, muzzleWorld, dir.ToRotation(), beamLengths[main],
                    vis * flick, Color.White, Color.White, 0.9f * coreStrength);
            }

            //枪口棱镜辉体：辉底 + 逆旋双星 + 色环粒（源头收口的物理答案）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 muzzle = muzzleWorld - Main.screenPosition;
            float mScale = (0.4f + 0.5f * focus) * flick;
            sb.Draw(glow, muzzle, null, Color.White with { A = 0 } * (0.55f + 0.35f * focus), 0f,
                glow.Size() / 2f, mScale * 1.5f, SpriteEffects.None, 0f);
            sb.Draw(star, muzzle, null, Color.White with { A = 0 } * 0.85f,
                Main.GlobalTimeWrappedHourly * 3.4f, star.Size() / 2f, mScale * 0.5f, SpriteEffects.None, 0f);
            sb.Draw(star, muzzle, null, HueOf((int)(Main.GlobalTimeWrappedHourly * 4f) % BeamCount) with { A = 0 } * 0.6f,
                -Main.GlobalTimeWrappedHourly * 2.2f, star.Size() / 2f, mScale * 0.72f, SpriteEffects.None, 0f);

            //各束落点辉光（端部收口）
            for (int i = 0; i < BeamCount; i++) {
                if (beamLengths[i] >= MaxLength - 8f) {
                    continue;
                }
                Vector2 dir = BeamDir(i, focus, lastCollapse01);
                Vector2 impact = muzzleWorld + dir * beamLengths[i] - Main.screenPosition;
                sb.Draw(glow, impact, null, HueOf(i) with { A = 0 } * (0.7f * flick), 0f,
                    glow.Size() / 2f, 0.22f + 0.3f * focus, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
