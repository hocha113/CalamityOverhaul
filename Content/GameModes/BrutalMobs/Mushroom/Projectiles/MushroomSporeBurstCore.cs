using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Mushroom.Projectiles
{
    /// <summary>
    /// 死亡孢核（无害预告体）。ai[0]=风味 ai[1]=基准角（生成帧权威端锁定，随生成包同步）。
    /// 困难孢子系死亡后原地凝聚 34 帧（可见膨胀），随后沿槽位放射孢弹；
    /// <see cref="BurstGapSlot"/> 是放射循环真正跳过的具名槽位缺口，骷髅版弹数 +2 但缺口加宽
    /// （<see cref="SkeletonGapHalfWidthSlots"/>，强度公平对冲）；虚影与放射走同一 <see cref="InGap"/>。
    /// 施法者已死，无来源取消语义（镜像 WastesIceShatterCore）
    /// </summary>
    internal class MushroomSporeBurstCore : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>凝聚预告帧数（公平契约 ≥30，各档位一律不缩短）</summary>
        internal const int BurstTelegraphFrames = 34;
        private const int BurstFadeFrames = 10;

        //==== 公平阀门：具名槽位缺口（放射循环真正读取） ====
        /// <summary>常规孢爆槽位数（Zombie/Bat：7 槽跳 1 → 6 发；槽位数不随档位增长）</summary>
        internal const int BurstSlots = 7;
        /// <summary>骷髅版槽位数（11 槽跳 3 → 8 发：弹数 +2 的具体来源）</summary>
        internal const int SkeletonBurstSlots = 11;
        /// <summary>具名孢爆缺口槽：放射与虚影共同跳过的槽位索引（基准角方向）</summary>
        internal const int BurstGapSlot = 0;
        /// <summary>骷髅版缺口半宽（缺口槽左右各加 1 槽）：缺口弧约 98° &gt; 常规约 51°，对冲弹数 +2</summary>
        internal const int SkeletonGapHalfWidthSlots = 1;

        private int Flavor => (int)Projectile.ai[0];
        private float BaseAngle => Projectile.ai[1];
        private int Slots => Flavor == MushroomSporeBoltProj.FlavorSkeleton ? SkeletonBurstSlots : BurstSlots;
        private int TotalLife => BurstTelegraphFrames + BurstFadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>槽位是否落在缺口内（环形槽距判定，放射与虚影共用）</summary>
        private bool InGap(int i) {
            int half = Flavor == MushroomSporeBoltProj.FlavorSkeleton ? SkeletonGapHalfWidthSlots : 0;
            int d = Math.Abs(i - BurstGapSlot);
            d = Math.Min(d, Slots - d);
            return d <= half;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 360;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//纯预告体，伤害经由孢弹
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = BurstTelegraphFrames + BurstFadeFrames;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            if (elapsed == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.3f, Pitch = -0.5f, MaxInstances = 5 }, Projectile.Center);
            }

            //凝聚期：向心孢尘（≤2 粒/帧）
            if (elapsed < BurstTelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = Main.rand.NextVector2Unit();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(18f, 42f),
                    DustID.GlowingMushroom, -dir * Main.rand.NextFloat(1f, 2.4f), 120, default, 1.1f);
                dust.noGravity = true;
            }

            if (elapsed == BurstTelegraphFrames) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Emit();
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.7f, Pitch = 0.2f, MaxInstances = 5 }, Projectile.Center);
                    for (int i = 0; i < 8; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GlowingMushroom,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 90, default,
                            Main.rand.NextFloat(1f, 1.6f));
                        dust.noGravity = true;
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, MushroomSporeBoltProj.SporeBright.ToVector3()
                * (0.12f + 0.2f * MathHelper.Clamp(elapsed / (float)BurstTelegraphFrames, 0f, 1f)));
        }

        /// <summary>提交帧放射：与虚影同一 InGap，缺口是循环真正跳过的槽位带</summary>
        private void Emit() {
            (float speed, float gravity) = MushroomSporeBoltProj.FlavorShot(Flavor);
            int boltType = ModContent.ProjectileType<MushroomSporeBoltProj>();
            int slots = Slots;
            for (int i = 0; i < slots; i++) {
                if (InGap(i)) {
                    continue;//具名槽位缺口：逃生方向
                }
                float ang = BaseAngle + MathHelper.TwoPi * i / slots;
                Vector2 vel = ang.ToRotationVector2() * speed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    boltType, Projectile.damage, 1f, Main.myPlayer, gravity, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            if (elapsed >= BurstTelegraphFrames) {
                //破裂闪光（加色，随消散退淡）
                float flash = MathHelper.Clamp(1f - (elapsed - BurstTelegraphFrames) / (float)BurstFadeFrames, 0f, 1f);
                Color burst = (MushroomSporeBoltProj.SporeBright with { A = 0 }) * (0.7f * flash);
                Main.EntitySpriteDraw(glow, center, null, burst, 0f, glow.Size() / 2f,
                    1.5f - flash * 0.55f, SpriteEffects.None, 0);
                return false;
            }

            float progress = elapsed / (float)BurstTelegraphFrames;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 17f + Projectile.identity);

            //孢核本体：双层孢珠随凝聚膨胀（可见膨胀=预告进度）
            MushroomSporeBoltProj.DrawGlobAt(center, Main.GlobalTimeWrappedHourly * 2.2f,
                0.5f + 0.5f * progress, new Vector2(0.34f, 0.34f) * (0.55f + 0.85f * progress));

            //弹道虚影：与放射同一槽位与缺口判定，空缺方向就是安全方向
            int slots = Slots;
            float ghostDist = 20f + 26f * progress;
            for (int i = 0; i < slots; i++) {
                if (InGap(i)) {
                    continue;
                }
                float ang = BaseAngle + MathHelper.TwoPi * i / slots;
                Vector2 pos = center + ang.ToRotationVector2() * ghostDist;
                MushroomSporeBoltProj.DrawGlobAt(pos, ang + MathHelper.PiOver2,
                    0.5f * progress * pulse, new Vector2(0.2f, 0.28f));
            }

            //缺口亮楔（加色光，指示逃生方向）
            Vector2 lanePos = center + BaseAngle.ToRotationVector2() * (ghostDist + 24f);
            Color laneColor = new Color(150, 255, 235, 0) * (0.45f * progress);
            Main.EntitySpriteDraw(glow, lanePos, null, laneColor, BaseAngle, glow.Size() / 2f,
                new Vector2(2.2f, 0.5f), SpriteEffects.None, 0);
            return false;
        }
    }
}
