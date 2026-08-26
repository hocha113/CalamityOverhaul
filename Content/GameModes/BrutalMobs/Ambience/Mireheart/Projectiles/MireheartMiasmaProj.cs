using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mireheart.Projectiles
{
    /// <summary>
    /// 「沼气袋」破裂喷出的沼瘴雾团。ai[0]=体型。
    /// 张开 26 帧 → 驻留 64 帧 → 散尽 44 帧；散逸过 35% 即失去判定（判定窗=可见窗）。
    /// 触碰微量伤害并附短暂原版中毒；缓慢上浮飘散，走位可避。
    /// 团体小、无缺口设计：绕开整团即安全。Boss 在场判定即停
    /// </summary>
    internal class MireheartMiasmaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //====== 具名数值块 ======
        private const int GrowFrames = 26;
        private const int HoldFrames = 64;
        private const int DryFrames = 44;
        private const int TotalFrames = GrowFrames + HoldFrames + DryFrames;
        /// <summary>满径（像素，×体型）</summary>
        private const float BaseRadius = 58f;
        /// <summary>判定半径 = 可见半径 × 此系数（判定略窄，偏袒玩家）</summary>
        private const float CollideRadiusFrac = 0.85f;
        /// <summary>伤害 = 原版黄蜂接触伤害 × 此值（镜像 DamageFrac 写法）</summary>
        private const float DamageFrac = 0.5f;
        /// <summary>敌对弹幕对玩家结算自带 ×2（专家 ×4），此处回折一半取回接触口径</summary>
        private const float HostileProjHalf = 0.5f;
        /// <summary>中毒时长（帧），档位不调伤害与减益，只调沼气频率</summary>
        private const int PoisonFrames = 210;
        /// <summary>雾团绘制份数</summary>
        private const int PuffCount = 6;

        private float Scale => Projectile.ai[0] <= 0f ? 1f : Projectile.ai[0];
        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private float GrowProgress => MathHelper.Clamp(Elapsed / (float)GrowFrames, 0f, 1f);
        private float DryProgress => MathHelper.Clamp(
            (DryFrames - Projectile.timeLeft) / (float)DryFrames, 0f, 1f);
        private float CurrentRadius {
            get {
                float t = GrowProgress;
                return BaseRadius * Scale * (1f - (1f - t) * (1f - t));
            }
        }

        /// <summary>伤害基准：原版黄蜂（本群系代表敌怪）接触伤害折算，微量口径</summary>
        internal static int MiasmaDamage() {
            int baseContact = ContentSamples.NpcsByNetId.TryGetValue(NPCID.Hornet, out NPC hornet)
                ? hornet.damage : 26;
            return Math.Max(3, (int)(baseContact * DamageFrac * HostileProjHalf));
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 240;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //判定窗=可见窗；Boss 在场判定即停（各端从同步世界状态得出同一结论）
            Projectile.hostile = DryProgress <= 0.35f && !CWRWorld.HasBoss;

            //缓慢上浮 + 微幅摆动：确定性推进，各端一致（不触碰 Main.rand）
            float sway = MathF.Sin(Elapsed * 0.05f + Projectile.identity * 1.3f) * 0.14f;
            Projectile.position += new Vector2(sway, -0.22f * (1f - DryProgress * 0.6f));

            if (Main.dedServ) {
                return;
            }
            //雾缘渗尘（≤1 粒/2 帧预算）
            if (Main.rand.NextBool(2) && CurrentRadius > 16f) {
                float freshness = 1f - DryProgress;
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2()
                    * CurrentRadius * Main.rand.NextFloat(0.5f, 1f);
                Dust dust = Dust.NewDustPerfect(pos, DustID.Poisoned,
                    ang.ToRotationVector2() * 0.25f + new Vector2(0f, -0.2f),
                    140, default, 0.9f * freshness + 0.3f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center,
                new Vector3(0.10f, 0.20f, 0.05f) * (1f - DryProgress));
        }

        /// <summary>圆盘判定，判定半径略窄于可见半径</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float radius = CurrentRadius * CollideRadiusFrac;
            Vector2 center = Projectile.Center;
            Vector2 closest = new(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(closest, center) <= radius * radius;
        }

        /// <summary>短暂原版中毒（命中方本机结算，原生同步；禁新建 ModBuff）</summary>
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Poisoned, PoisonFrames);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 fogOrigin = fog.Size() * 0.5f;
            Vector2 center = Projectile.Center - Main.screenPosition;

            float radius = CurrentRadius;
            float fade = MathHelper.Clamp(Elapsed / 14f, 0f, 1f) * (1f - DryProgress);
            if (fade <= 0.01f || radius < 6f) {
                return false;
            }

            Color deep = new(50, 62, 30);
            Color bright = new(138, 192, 72);

            //中央雾体（真 alpha 层承担实体感）
            Main.EntitySpriteDraw(fog, center, null, deep * (0.42f * fade),
                Projectile.identity * 0.9f, fogOrigin, radius * 1.4f / fog.Width, SpriteEffects.None, 0);

            //环布小雾团：确定性散列摆动
            for (int i = 0; i < PuffCount; i++) {
                float hA = Hash(i, 1);
                float hR = Hash(i, 2);
                float hS = Hash(i, 3);
                float swirl = MathF.Sin(Main.GlobalTimeWrappedHourly * (0.6f + hS * 0.5f) + i * 2.1f) * 0.2f;
                float ang = hA * MathHelper.TwoPi + swirl;
                Vector2 pos = center + ang.ToRotationVector2() * radius * (0.25f + 0.6f * MathF.Sqrt(hR));
                float puffScale = (0.16f + 0.12f * hS) * (radius / BaseRadius);
                float rot = hA * MathHelper.TwoPi + Main.GlobalTimeWrappedHourly * (hS - 0.5f) * 0.7f;

                Main.EntitySpriteDraw(fog, pos, null, deep * (0.5f * fade),
                    rot, fogOrigin, puffScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, bright with { A = 0 } * (0.20f * fade),
                    0f, glow.Size() * 0.5f, puffScale * 2.2f, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //散尽余韵：几粒毒沫飘起
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 14f),
                    DustID.Poisoned, new Vector2(0f, -0.4f), 150, default, 0.8f);
                dust.noGravity = true;
            }
        }

        /// <summary>确定性散列（各端一致，不触碰 Main.rand）</summary>
        private float Hash(int i, int salt) => (Projectile.identity * 131 + i * 53 + salt * 29) % 89 / 89f;
    }
}
