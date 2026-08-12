using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaWallOfFlesh
{
    /// <summary>
    /// 血肉墙鬼奴的眼激光：一道"细、快、短促"的血光剪切——3 帧弹出全长、
    /// 16 帧快扫一小段弧、8 帧收细熄灭，全程不足半秒。与毁灭者的持续粗束横扫、
    /// 月球领主的巨粗毁灭射线在宽度/时长/根数上严格区分。
    /// ai[0]=起始角 ai[1]=角速度 ai[2]=眼位（0上/1下），逐帧锚定宿主墙眼；
    /// 宿主没了/开始溶解则快进熄灭。扫过湖面时犁出细碎涟漪
    /// </summary>
    internal class KikasaWallOfFleshEyeLaser : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int SnapFrames = 3;
        internal const int SweepFrames = 16;
        internal const int FadeFrames = 8;
        internal const int TotalLife = SnapFrames + SweepFrames + FadeFrames;

        /// <summary>剪切半弧：单眼扫过 ±0.26 rad 的窄扇</summary>
        internal const float HalfArc = 0.26f;

        private const float MaxLength = 2300f;
        /// <summary>核宽——细刃，不是光炮</summary>
        private const float CoreWidth = 9f;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float StartAngle => ref Projectile.ai[0];
        private ref float SweepSpeed => ref Projectile.ai[1];
        private int EyeIndex => (int)Projectile.ai[2];

        private float beamLen;
        private float widthMul = 1f;

        //血刃三层：暗缘 / 血红主体 / 亮芯
        private static Color EdgeBlood => KikasaDomain.CoolTint(new(120, 24, 24), new(70, 90, 96));
        private static Color MainBlood => KikasaDomain.CoolTint(new(240, 74, 62), new(130, 162, 168));
        private static Color CoreBlood => KikasaDomain.CoolTint(new(255, 178, 152), new(216, 234, 236));

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.timeLeft = TotalLife + 20;
        }

        /// <summary>宿主墙：owner 场上唯一</summary>
        private KikasaWallOfFleshServant FindHost() {
            int servantType = ModContent.ProjectileType<KikasaWallOfFleshServant>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p?.active == true && p.owner == Projectile.owner && p.type == servantType
                    && p.ModProjectile is KikasaWallOfFleshServant servant) {
                    return servant;
                }
            }
            return null;
        }

        public override void AI() {
            KikasaWallOfFleshServant host = FindHost();

            //宿主没了/开始溶解：快进到熄灭段
            if ((host == null || host.IsDismissing) && Timer < SnapFrames + SweepFrames) {
                Timer = SnapFrames + SweepFrames;
            }

            //扫掠角：弹出定格→快扫→熄灭定格
            float sweepT = MathHelper.Clamp(Timer - SnapFrames, 0f, SweepFrames);
            Projectile.rotation = StartAngle + SweepSpeed * sweepT;
            if (host != null) {
                Projectile.Center = host.EyeWorldPos(EyeIndex);
            }

            //长度 3 帧鞭出（陡峭缓出曲线，一甩即满；首帧即近七成长，不留空拍），熄灭期收细不缩长
            if (Timer < SnapFrames) {
                float p = (Timer + 1f) / SnapFrames;
                beamLen = MaxLength * (1f - MathF.Pow(1f - p, 3f));
                widthMul = 1f;
            }
            else if (Timer >= SnapFrames + SweepFrames) {
                float p = (Timer - SnapFrames - SweepFrames) / FadeFrames;
                beamLen = MaxLength;
                widthMul = 1f - MathHelper.Clamp(p, 0f, 1f);
            }
            else {
                beamLen = MaxLength;
                widthMul = 1f;
            }

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 1; i < 5; i++) {
                Lighting.AddLight(Projectile.Center + dir * (beamLen / 5f * i), 0.4f, 0.1f, 0.09f);
            }

            if (Main.dedServ || widthMul < 0.25f) {
                return;
            }

            //沿刃洒落的细血珠：剪切的余屑
            if (Main.rand.NextBool(2)) {
                float along = Main.rand.NextFloat(0.05f, 0.95f);
                Vector2 pos = Projectile.Center + dir * beamLen * along;
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                    dir.RotatedBy(MathHelper.PiOver2 * (Main.rand.NextBool() ? 1f : -1f))
                        * Main.rand.NextFloat(0.6f, 1.8f),
                    Main.rand.NextBool(3) ? EdgeBlood : MainBlood,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(10, 18));
            }

            UpdateLakeShear(dir);
        }

        /// <summary>刃线扫过湖面：交点犁出细碎涟漪与飞血（观看域门控）</summary>
        private void UpdateLakeShear(Vector2 dir) {
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT <= 0.5f
                || KikasaDomain.Viewed != domain) {
                return;
            }
            float lakeY = domain.LakeWorldY;
            float crossT = MathF.Abs(dir.Y) > 0.02f ? (lakeY - Projectile.Center.Y) / dir.Y : -1f;
            if (crossT < 30f || crossT > beamLen) {
                return;
            }
            Vector2 cross = new(Projectile.Center.X + dir.X * crossT, lakeY);
            int t = (int)Timer;
            if (t % 3 == 1) {
                KikasaDomainDeco.RippleAt(cross, 0.6f);
            }
            if (t % 5 == 2) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    cross + new Vector2(Main.rand.NextFloat(-8f, 8f), -3f),
                    new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2.5f, 5f)),
                    MainBlood, Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        //伤害窗与可见剪切严格对齐：弹出完成起、扫掠结束止；熄灭余辉不裁人
        public override bool? CanDamage()
            => Timer >= SnapFrames && Timer <= SnapFrames + SweepFrames + 2 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center,
                Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLen,
                CoreWidth, ref p);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //剪切命中：切口沿刃向喷血
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(12f, 12f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool(3) ? EdgeBlood : MainBlood,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(12, 22));
            }
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.45f, Pitch = 0.2f, MaxInstances = 3 }, target.Center);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (beamLen < 12f || widthMul <= 0.02f) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 origin = new(2f, glow.Height * 0.5f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation;
            //高频细颤：刃在鸣
            float quiver = 1f + 0.09f * MathF.Sin(Main.GlobalTimeWrappedHourly * 52f + Seed * 5f);
            float w = CoreWidth * widthMul * quiver;
            float lenScale = beamLen / (glow.Width - 4f);

            //暗缘 → 血红主体 → 亮芯，三层同轴细刃
            sb.Draw(glow, pos, null, EdgeBlood * (0.5f * widthMul), rot, origin,
                new Vector2(lenScale, w * 3.4f / glow.Height), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, MainBlood * (0.85f * widthMul), rot, origin,
                new Vector2(lenScale, w * 1.7f / glow.Height), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, CoreBlood * widthMul, rot, origin,
                new Vector2(lenScale, w * 0.62f / glow.Height), SpriteEffects.None, 0f);

            //眼口辉光：出刃点一小团积血亮斑压住近端软边
            Vector2 gOrigin = glow.Size() * 0.5f;
            sb.Draw(glow, pos, null, MainBlood * (0.8f * widthMul), 0f, gOrigin,
                new Vector2(30f / glow.Width), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, CoreBlood * (0.5f * widthMul), 0f, gOrigin,
                new Vector2(16f / glow.Width), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
