using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>短促可读射线：预警线充能 → 瞬灼射线 12 帧 → 熄灭
    /// ai[0]=预警帧数, ai[1]=角度, ai[2]=跟随NPC索引+1（0静止）</summary>
    internal class GolemEyeRay : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int FireFrames = 12;
        internal const int FadeFrames = 8;
        internal const float MaxLength = 1500f;

        private int TelegraphFrames => (int)Math.Max(Projectile.ai[0], 1f);
        private int TotalFrames => TelegraphFrames + FireFrames + FadeFrames;
        /// <summary>寿命进度推导阶段（各端一致）</summary>
        private int Elapsed => TotalFrames - Projectile.timeLeft;
        private bool Firing => Elapsed >= TelegraphFrames && Elapsed < TelegraphFrames + FireFrames;
        private bool Fading => Elapsed >= TelegraphFrames + FireFrames;
        private float Rotation => Projectile.ai[1];

        //初始化标记与跟随偏移（各端首帧本地解算）
        private bool initialized;
        private Vector2 followOffset;
        //射线实际长度（撞地裁剪）
        private float beamLength = MaxLength;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            //哨兵值：首帧若未被网络校时则本地归位
            Projectile.timeLeft = 60000;
            Projectile.netImportant = true;
        }

        /// <summary>中途加入校时：同步已流逝帧数</summary>
        public override void SendExtraAI(System.IO.BinaryWriter writer) {
            writer.Write((short)System.Math.Max(Elapsed, 0));
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader) {
            short elapsed = reader.ReadInt16();
            Projectile.timeLeft = System.Math.Max(TotalFrames - elapsed, 1);
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                //未收到校时的端（服务端/单机）本地归位
                if (Projectile.timeLeft > TotalFrames) {
                    Projectile.timeLeft = TotalFrames;
                }
                if (TryGetFollowNpc(out NPC follow)) {
                    followOffset = Projectile.Center - follow.Center;
                }
            }

            //预警期跟随发射口，开火即锁定
            if (!Firing && !Fading && TryGetFollowNpc(out NPC npcFollow)) {
                Projectile.Center = npcFollow.Center + followOffset;
            }

            //长度按地形裁剪（射线走机关缝隙，不穿墙）
            beamLength = ScanLength();

            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.42f, 0.13f));

            //开火首帧音画
            if (Elapsed == TelegraphFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = 0.35f, Volume = 0.7f }, Projectile.Center);
                Vector2 dir = Rotation.ToRotationVector2();
                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(beamLength),
                        DustID.SolarFlare, dir.RotatedByRandom(0.4f) * Main.rand.NextFloat(1f, 3f), 0, default, 1.2f);
                    dust.noGravity = true;
                }
            }
        }

        private bool TryGetFollowNpc(out NPC npc) {
            npc = null;
            int index = (int)Projectile.ai[2] - 1;
            if (index < 0 || index >= Main.maxNPCs) {
                return false;
            }
            npc = Main.npc[index];
            return npc.active;
        }

        //LaserScan 采样暂存（主线程复用，免每帧分配）
        private static readonly float[] scanSamples = new float[3];

        /// <summary>激光扫描裁剪长度</summary>
        private float ScanLength() {
            float[] samples = scanSamples;
            Collision.LaserScan(Projectile.Center, Rotation.ToRotationVector2(), 8f, MaxLength, samples);
            float total = 0f;
            foreach (float s in samples) {
                total += s;
            }
            return Math.Max(total / samples.Length, 120f);
        }

        public override bool? CanDamage() => Firing ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Firing) {
                return false;
            }
            float collisionPoint = 0f;
            Vector2 end = Projectile.Center + Rotation.ToRotationVector2() * beamLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, end, 20f, ref collisionPoint);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new(0f, line.Height / 2f);
            float lenScale = beamLength / line.Width;

            if (!Firing && !Fading) {
                //预警细线：进度推进 + 末端白热
                float progress = Elapsed / (float)TelegraphFrames;
                float flash = MathHelper.Clamp((progress - 0.78f) / 0.22f, 0f, 1f);
                Color baseCol = Color.Lerp(new Color(255, 150, 30), new Color(255, 230, 150), flash) with { A = 0 };
                Main.EntitySpriteDraw(line, drawPos, null, baseCol * (0.4f + 0.35f * progress),
                    Rotation, origin, new Vector2(lenScale, 0.14f + 0.1f * flash), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, drawPos, null, baseCol * 0.8f,
                    0f, glow.Size() / 2f, 0.4f + 0.25f * progress, SpriteEffects.None, 0);
                //远端盖帽：线贴图长轴两端硬切，命中点补光点封口，兼作落点预告
                Vector2 telegraphTip = drawPos + Rotation.ToRotationVector2() * beamLength;
                Main.EntitySpriteDraw(glow, telegraphTip, null, baseCol * (0.45f + 0.4f * progress),
                    0f, glow.Size() / 2f, 0.3f + 0.22f * flash, SpriteEffects.None, 0);
                return false;
            }

            //射击/衰减期：三层实束
            float life = Fading
                ? 1f - (Elapsed - TelegraphFrames - FireFrames) / (float)FadeFrames
                : 1f;
            float pulse = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f);

            Main.EntitySpriteDraw(line, drawPos, null, new Color(200, 90, 20, 0) * (0.75f * life),
                Rotation, origin, new Vector2(lenScale, 0.68f * life * pulse), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, new Color(255, 180, 70, 0) * (0.9f * life),
                Rotation, origin, new Vector2(lenScale, 0.4f * life), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, new Color(255, 245, 205, 0) * life,
                Rotation, origin, new Vector2(lenScale, 0.16f * life), SpriteEffects.None, 0);
            //根部与末端辉光
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 200, 90, 0) * life,
                0f, glow.Size() / 2f, 0.85f * life, SpriteEffects.None, 0);
            Vector2 tip = drawPos + Rotation.ToRotationVector2() * beamLength;
            Main.EntitySpriteDraw(glow, tip, null, new Color(255, 200, 90, 0) * (0.8f * life),
                0f, glow.Size() / 2f, 0.6f * life, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>发射助手（服务端）；跟随偏移由各端首帧按发射口相对位置自解算</summary>
        internal static void Fire(NPC owner, Vector2 muzzle, float rotation, int telegraphFrames, int damage,
            int followNpcIndex = -1) {
            if (VaultUtils.isClient || owner == null || !owner.active) {
                return;
            }
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), muzzle, Vector2.Zero,
                ModContent.ProjectileType<GolemEyeRay>(), damage, 0f, Main.myPlayer,
                telegraphFrames, rotation, followNpcIndex + 1);
            if (id >= 0 && id < Main.maxProjectiles) {
                Main.projectile[id].netUpdate = true;
            }
        }
    }
}
