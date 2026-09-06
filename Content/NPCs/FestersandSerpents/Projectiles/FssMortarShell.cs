using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles
{
    /// <summary>
    /// 吞沙炮巨弹：腐沙裹灵液的重型炮弹，固定飞时后在空中炸成下锥霰弹伞。
    /// ai[0]=飞行帧数（到时空爆）。
    /// 公平口径：空爆伞正下方 ±MortarGapHalfAngle 是声明安全眼（发射循环实读跳过），
    /// 站进伞心不挨霰弹——学会一次记住一世。
    /// </summary>
    internal class FssMortarShell : FssModProjectile
    {
        public override string Texture => CWRConstant.NPC + "BSS/CactusBall";

        private int FlightFrames => Math.Max((int)Projectile.ai[0], 10);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.scale = 1.9f;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = FlightFrames;
            }

            Projectile.velocity.Y += FssDirector.MortarShellGravity;
            Projectile.rotation += Projectile.velocity.X * 0.012f;

            //重弹烟金尾（同材质拖尾在 PreDraw，此处是碎滴）
            if (!VaultUtils.isServer) {
                if (Main.rand.NextBool(2)) {
                    Dust gold = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                        DustID.Ichor, -Projectile.velocity * 0.06f, 40, default, Main.rand.NextFloat(0.8f, 1.2f));
                    gold.noGravity = false;
                }
                if (Main.rand.NextBool(3)) {
                    Dust sand = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                        DustID.Sand, -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                        110, FssVfx.TaintedSand, Main.rand.NextFloat(1f, 1.5f));
                    sand.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, FssVfx.IchorGold.ToVector3() * 0.55f);
            }

            //临爆预缩：爆前 8 帧微缩变亮（爆点先安静一拍的反向读数）
            if (Projectile.timeLeft <= 8) {
                Projectile.scale = 1.9f * MathHelper.Lerp(1f, 0.82f, (8 - Projectile.timeLeft) / 8f);
            }
        }

        /// <summary>自然到时空爆（被清弹 Kill 不爆）</summary>
        public override void OnKill(int timeLeft) {
            if (timeLeft > 0) {
                return;
            }

            if (!VaultUtils.isServer) {
                FssVfx.IchorBurst(Projectile.Center, 2.2f);
                FssVfx.CorruptSandBurst(Projectile.Center, 1.4f);
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 1f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.7f, Pitch = 0.15f, MaxInstances = 3 }, Projectile.Center);
                FssVfx.Shake(Projectile.Center, 6f, 1500f);
            }

            if (VaultUtils.isClient) {
                return;
            }

            //下锥霰弹伞：150 度锥、正下方 ±MortarGapHalfAngle 声明安全眼（发射循环实读跳过）
            int shardType = ModContent.ProjectileType<FssMortarShard>();
            int rainType = ModContent.ProjectileType<FssIchorGlob>();
            int shardDamage = (int)(Projectile.damage * 0.85f);
            float cone = MathHelper.ToRadians(FssDirector.MortarConeDeg);
            int count = FssDirector.MortarShardCount;
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.PiOver2 + (i / (float)(count - 1) - 0.5f) * cone;
                //中央安全眼：偏离正下方不足 GapHalfAngle 的槽位跳过
                if (Math.Abs(MathHelper.WrapAngle(ang - MathHelper.PiOver2)) < FssDirector.MortarGapHalfAngle) {
                    continue;
                }
                float speed = Main.rand.NextFloat(8.5f, 11.5f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    ang.ToRotationVector2() * speed, shardType, shardDamage, 0.5f, Main.myPlayer);
            }
            //伴随金雨（雨滴模式不留池；同样跳过中央缝）
            for (int i = 0; i < FssDirector.MortarRainDrops; i++) {
                float ang = MathHelper.PiOver2 + Main.rand.NextFloat(-cone * 0.5f, cone * 0.5f);
                if (Math.Abs(MathHelper.WrapAngle(ang - MathHelper.PiOver2)) < FssDirector.MortarGapHalfAngle) {
                    continue;
                }
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    ang.ToRotationVector2() * Main.rand.NextFloat(4f, 7f), rainType,
                    (int)(Projectile.damage * 0.6f), 0.3f, Main.myPlayer, 1f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //同材质拖尾：缩淡重画（横轴比≥0.5 契约由同贴图保证）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null,
                    FssVfx.IchorDeep with { A = 70 } * (0.4f * t),
                    Projectile.rotation - i * 0.05f, origin, Projectile.scale * (0.55f + 0.35f * t), SpriteEffects.None, 0);
            }

            //本体：坏死染色实体 + 灵液鼓光层
            Color body = lightColor.MultiplyRGB(FssVfx.SkinMul);
            Main.EntitySpriteDraw(tex, drawPos, null, body, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0);
            float pulse = 0.55f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f);
            Main.EntitySpriteDraw(tex, drawPos, null, FssVfx.IchorBright with { A = 0 } * pulse,
                Projectile.rotation, origin, Projectile.scale * 1.06f, SpriteEffects.None, 0);
            return false;
        }
    }
}
