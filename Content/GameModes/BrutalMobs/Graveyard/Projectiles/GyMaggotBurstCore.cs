using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Graveyard.Projectiles
{
    /// <summary>
    /// 蛆尸尸涌凝聚核（无害预告体）：ai[0]=档位。蛆尸死亡后原地蠕动凝聚 34 帧，
    /// 随后向上扇形迸出 3 只蛆弹；相邻蛆弹的角距是具名常量 <see cref="FanSpacingRad"/>，
    /// 发射循环与预告虚影同读同一角度表——看见的间隙就是走得过的间隙。
    /// 死亡驱动只是战斗内短扑之外的加菜（M6 覆盖诚实）
    /// </summary>
    internal class GyMaggotBurstCore : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Maggot;

        /// <summary>凝聚帧数（公平契约 ≥30，各档位一律不缩短）</summary>
        private const int CoagulateFrames = 34;
        private const int BurstFadeFrames = 10;
        /// <summary>迸射蛆弹数（固定 3，档位只调蛆弹初速）</summary>
        internal const int BoltCount = 3;
        /// <summary>公平阀门：相邻蛆弹强制角距（弧度），发射与虚影共用；两弹之间即逃生间隙</summary>
        internal const float FanSpacingRad = 0.62f;
        /// <summary>扇心方向（正上方，尸涌向上迸出）</summary>
        private const float FanCenter = -MathHelper.PiOver2;
        /// <summary>蛆弹初速（档位每级 +0.4，角距测试不变）</summary>
        private const float BoltSpeedBase = 4.6f;

        private int Tier => Math.Clamp((int)Projectile.ai[0], 1, 3);
        private int TotalLife => CoagulateFrames + BurstFadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 20;
            Projectile.hostile = false;//纯预告体，伤害经由蛆弹
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = CoagulateFrames + BurstFadeFrames;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            if (elapsed == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.24f, Pitch = -0.7f, MaxInstances = 5 }, Projectile.Center);
            }

            //凝聚期：血沫与腐绿尘向心蠕聚（≤2 粒/帧）
            if (elapsed < CoagulateFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = Main.rand.NextVector2Unit();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(14f, 34f),
                    Main.rand.NextBool() ? DustID.Blood : DustID.JungleGrass,
                    -dir * Main.rand.NextFloat(0.8f, 1.8f), 140, default, 0.9f);
                dust.noGravity = true;
            }

            if (elapsed == CoagulateFrames) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Emit();
                }
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 5 }, Projectile.Center);
                    for (int i = 0; i < 6; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                            (-Vector2.UnitY).RotatedByRandom(0.9f) * Main.rand.NextFloat(1.5f, 4f), 110, default,
                            Main.rand.NextFloat(0.9f, 1.4f));
                        dust.noGravity = Main.rand.NextBool();
                    }
                }
            }
        }

        /// <summary>提交帧迸射：与虚影同一角度表，<see cref="FanSpacingRad"/> 是循环真正读取的间距</summary>
        private void Emit() {
            float speed = BoltSpeedBase + 0.4f * (Tier - 1);
            int boltType = ModContent.ProjectileType<GyMaggotBolt>();
            for (int i = 0; i < BoltCount; i++) {
                float ang = FanCenter + (i - (BoltCount - 1) * 0.5f) * FanSpacingRad;
                Vector2 vel = ang.ToRotationVector2() * speed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    boltType, Projectile.damage, 0.5f, Main.myPlayer, Main.rand.Next(4));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Main.instance.LoadNPC(NPCID.Maggot);
            Texture2D tex = TextureAssets.Npc[NPCID.Maggot].Value;
            int frames = Math.Max(Main.npcFrameCount[NPCID.Maggot], 1);
            Vector2 center = Projectile.Center - Main.screenPosition;

            if (elapsed >= CoagulateFrames) {
                //迸出闪光（随消散退淡）
                float flash = MathHelper.Clamp(1f - (elapsed - CoagulateFrames) / (float)BurstFadeFrames, 0f, 1f);
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Main.EntitySpriteDraw(glow, center, null, new Color(196, 226, 150, 0) * (0.55f * flash), 0f,
                    glow.Size() / 2f, 0.9f - flash * 0.3f, SpriteEffects.None, 0);
                return false;
            }

            float progress = MathHelper.Clamp(elapsed / (float)CoagulateFrames, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);

            //凝聚体：三只蛆虫环抱蠕动生长（原版贴图实体层）
            for (int k = 0; k < 3; k++) {
                float rot = Main.GlobalTimeWrappedHourly * 2.2f + k * MathHelper.TwoPi / 3f;
                int frame = (int)(elapsed * 0.3f + k) % frames;
                Rectangle rect = tex.Frame(1, frames, 0, frame);
                Vector2 pos = center + rot.ToRotationVector2() * (4f + 4f * progress);
                Color body = Color.Lerp(lightColor, new Color(206, 188, 150), 0.4f) * (0.45f + 0.55f * progress);
                Main.EntitySpriteDraw(tex, pos, rect, body, rot + MathHelper.PiOver2, rect.Size() / 2f,
                    0.5f + 0.5f * progress, SpriteEffects.None, 0);
            }

            //弹道虚影：与迸射同一角度表（FanSpacingRad），虚影之间的空当即逃生间隙
            float ghostDist = 16f + 22f * progress;
            for (int i = 0; i < BoltCount; i++) {
                float ang = FanCenter + (i - (BoltCount - 1) * 0.5f) * FanSpacingRad;
                Rectangle rect = tex.Frame(1, frames, 0, i % frames);
                Vector2 pos = center + ang.ToRotationVector2() * ghostDist;
                Color ghost = new Color(190, 220, 150, 150) * (0.5f * progress * pulse);
                Main.EntitySpriteDraw(tex, pos, rect, ghost, ang, rect.Size() / 2f, 0.7f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
