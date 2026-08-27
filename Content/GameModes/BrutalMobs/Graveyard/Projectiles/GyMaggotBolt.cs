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
    /// 蛆弹：尸涌迸出的低速弧线小弹。ai[0]=蠕动相位种子。
    /// 空中走抛物线，落地转原地蠕动 60 帧后自灭（短驻场危害点，站开即可）；
    /// 原版蛆虫贴图实体层（有遮挡像素）+ 同材质拖尾，全生命可见=全程判定
    /// </summary>
    internal class GyMaggotBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Maggot;

        /// <summary>落地驻留帧数（计满自灭）</summary>
        internal const int GroundLingerFrames = 60;
        /// <summary>每帧重力（低速弧线，不做落点解算）</summary>
        private const float Gravity = 0.2f;
        private const float MaxFallSpeed = 9f;
        /// <summary>出膛淡入帧：判定随可见度同门开启（公平阀）</summary>
        private const int FadeInFrames = 5;

        private ref float Age => ref Projectile.localAI[0];
        /// <summary>0=空中；≥1=落地后的驻留计帧（各端由碰撞确定性推得）</summary>
        private ref float GroundAge => ref Projectile.localAI[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(180f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            if (GroundAge > 0f) {
                //落地驻留：原地蠕动，横速吃干，计满自灭
                GroundAge++;
                Projectile.velocity.X *= 0.7f;
                Projectile.velocity.Y = 0f;
                Projectile.rotation = 0f;
                if (!Main.dedServ && Main.rand.NextBool(9)) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.JungleGrass,
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.2f, 0.8f)), 150, default, 0.7f);
                    dust.noGravity = true;
                }
                if (GroundAge >= GroundLingerFrames) {
                    Projectile.Kill();
                }
                return;
            }

            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Main.rand.NextBool(8)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    -Projectile.velocity * 0.1f, 160, default, 0.7f);
                dust.noGravity = true;
            }
        }

        /// <summary>着地转驻留而不销毁（落地 60 帧自灭由 <see cref="GroundAge"/> 负责）</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (GroundAge <= 0f) {
                GroundAge = 1f;
                Projectile.velocity = Vector2.Zero;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.25f, Pitch = -0.4f, MaxInstances = 6 }, Projectile.Center);
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.JungleGrass,
                    Main.rand.NextVector2Circular(1.6f, 1.2f), 130, default, Main.rand.NextFloat(0.7f, 1.1f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.Maggot);
            Texture2D tex = TextureAssets.Npc[NPCID.Maggot].Value;
            int frames = Math.Max(Main.npcFrameCount[NPCID.Maggot], 1);
            int frame = (int)((GroundAge > 0f ? GroundAge * 0.34f : Age * 0.2f) + Projectile.ai[0]) % frames;
            Rectangle rect = tex.Frame(1, frames, 0, frame);
            Vector2 orig = rect.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;
            Color body = Color.Lerp(lightColor, new Color(214, 196, 158), 0.35f) * opacity;

            //同材质拖尾（横轴 ≥0.5×弹体；落地驻留期无位移自然无尾）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, rect, body * (0.34f * t), Projectile.rotation, orig,
                    Projectile.scale * (0.55f + 0.3f * t), SpriteEffects.None, 0);
            }

            //本体：落地期以微缩放脉动读作蠕行
            float squirm = GroundAge > 0f ? 1f + 0.08f * MathF.Sin(GroundAge * 0.5f) : 1f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, rect, body,
                Projectile.rotation, orig, Projectile.scale * squirm, SpriteEffects.None, 0);
            return false;
        }
    }
}
