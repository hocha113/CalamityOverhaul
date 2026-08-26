using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Wastes.Projectiles
{
    /// <summary>
    /// 死亡凝晶核（无害预告体）。ai[0]=碎片数 ai[1]=体型。
    /// 冰壳怪死亡后原地凝晶 34 帧，随后径向放射冰晶碎片；
    /// 安全扇区由 identity 派生（两端一致），预告期以虚影逐条标出弹道，
    /// 扇区内无虚影即无碎片：虚影与发射走同一个 <see cref="InSafeSector"/>
    /// </summary>
    internal class WastesIceShatterCore : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBolt;

        /// <summary>预告帧数（公平契约 ≥30，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 34;
        private const int BurstFadeFrames = 10;
        /// <summary>安全扇区半角（弧度），发射与虚影共用同一判定</summary>
        internal const float SafeSectorHalfAngle = 0.55f;
        /// <summary>碎片基础速度（小型体按体型折减）</summary>
        private const float ShardSpeedBase = 6.2f;

        private int ShardCount => Math.Max((int)Projectile.ai[0], 3);
        private float CoreScale => Projectile.ai[1];
        private int TotalLife => TelegraphFrames + BurstFadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>安全扇区中心方向：由 identity 派生，各端一致，预告期即可读</summary>
        private float GapCenter => Projectile.identity * 2.399963f % MathHelper.TwoPi;

        internal static bool InSafeSector(float angle, float gapCenter)
            => Math.Abs(MathHelper.WrapAngle(angle - gapCenter)) < SafeSectorHalfAngle;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//纯预告体，伤害经由碎片
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + BurstFadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            if (elapsed == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 5 }, Projectile.Center);
            }

            //凝晶期：向心霜尘（≤2 粒/帧）
            if (elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = Main.rand.NextVector2Unit();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(20f, 40f) * CoreScale,
                    DustID.Frost, -dir * Main.rand.NextFloat(1f, 2.2f), 120, default, 1f);
                dust.noGravity = true;
            }

            if (elapsed == TelegraphFrames) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Emit();
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.8f, Pitch = 0.1f, MaxInstances = 5 }, Projectile.Center);
                    for (int i = 0; i < 6; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Ice,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 90, default,
                            Main.rand.NextFloat(1f, 1.6f));
                        dust.noGravity = true;
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(0.15f, 0.25f, 0.38f) * CoreScale);
        }

        /// <summary>提交帧放射：与虚影同一 InSafeSector，扇区是循环真正跳过的角度带</summary>
        private void Emit() {
            float speed = ShardSpeedBase * (CoreScale < 1f ? 0.85f : 1f);
            float gapCenter = GapCenter;
            int shardType = ModContent.ProjectileType<WastesIceShardProj>();
            int count = ShardCount;
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count;
                if (InSafeSector(ang, gapCenter)) {
                    continue;//具名安全扇区
                }
                Vector2 vel = ang.ToRotationVector2() * speed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    shardType, Projectile.damage, 1f, Main.myPlayer);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;

            if (elapsed >= TelegraphFrames) {
                //碎裂闪光（加色，随消散退淡）
                float flash = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)BurstFadeFrames, 0f, 1f);
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Color burst = new Color(170, 225, 255, 0) * (0.7f * flash);
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, burst, 0f,
                    glow.Size() / 2f, (1.6f - flash * 0.6f) * CoreScale, SpriteEffects.None, 0);
                return false;
            }

            float progress = elapsed / (float)TelegraphFrames;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.identity);

            //核心凝晶：三枚冰晶 120 度环抱生长（实体层）
            for (int k = 0; k < 3; k++) {
                float rot = Main.GlobalTimeWrappedHourly * 2f + k * MathHelper.TwoPi / 3f;
                Vector2 pos = Projectile.Center + rot.ToRotationVector2() * 6f * CoreScale - Main.screenPosition;
                Color coreColor = Color.Lerp(lightColor, new Color(196, 234, 255), 0.6f) * (0.5f + 0.5f * progress);
                Main.EntitySpriteDraw(tex, pos, null, coreColor, rot + MathHelper.PiOver2, orig,
                    (0.45f + 0.4f * progress) * CoreScale, SpriteEffects.None, 0);
            }

            //弹道虚影：与放射同一角度与扇区判定，扇区内的空缺就是安全方向
            float gapCenter = GapCenter;
            int count = ShardCount;
            float ghostDist = (22f + 26f * progress) * CoreScale;
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count;
                if (InSafeSector(ang, gapCenter)) {
                    continue;
                }
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * ghostDist - Main.screenPosition;
                Color ghost = new Color(180, 224, 255, 150) * (0.5f * progress * pulse);
                Main.EntitySpriteDraw(tex, pos, null, ghost, ang + MathHelper.PiOver2, orig,
                    0.6f * CoreScale, SpriteEffects.None, 0);
            }

            //安全扇区亮楔（加色光，指示逃生方向）
            Texture2D lane = CWRAsset.SoftGlow.Value;
            Vector2 lanePos = Projectile.Center + gapCenter.ToRotationVector2() * (ghostDist + 26f) - Main.screenPosition;
            Color laneColor = new Color(140, 255, 220, 0) * (0.45f * progress);
            Main.EntitySpriteDraw(lane, lanePos, null, laneColor, gapCenter, lane.Size() / 2f,
                new Vector2(2.2f, 0.5f), SpriteEffects.None, 0);
            return false;
        }
    }
}
