using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles
{
    /// <summary>
    /// 黏疮：抛物线落下黏附砖面，鼓胀升调后原地喷竖直灵液泉，脚下近地再留一小池。
    /// ai[0]=黏附后的鼓胀引信帧数。本体无接触伤害（放置型威胁，鼓胀即预告）；
    /// 喷发只认自然到期——被转阶段清弹 Kill 的疮不爆，安静退场。
    /// </summary>
    internal class FssStickyCyst : FssModProjectile
    {
        public override string Texture => CWRConstant.NPC + "BSS/CactusBall";

        private bool Stuck => Projectile.localAI[0] == 1f;
        private int Fuse => Math.Max((int)Projectile.ai[0], 10);
        /// <summary>鼓胀进度 0→1（黏附后按 timeLeft 推进，各端同看）</summary>
        private float Swell => Stuck ? MathHelper.Clamp(1f - Projectile.timeLeft / (float)Fuse, 0f, 1f) : 0f;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override bool ShouldUpdatePosition() => !Stuck;

        public override void AI() {
            if (!Stuck) {
                //飞行段：重力弧线 + 滴金
                Projectile.velocity.Y += 0.34f;
                if (Projectile.velocity.Y > 16f) {
                    Projectile.velocity.Y = 16f;
                }
                Projectile.rotation += Projectile.velocity.X * 0.03f;
                if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                    Dust drip = Dust.NewDustPerfect(Projectile.Center, DustID.Ichor,
                        -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                        40, default, Main.rand.NextFloat(0.6f, 0.9f));
                    drip.noGravity = false;
                }
                return;
            }

            //鼓胀段：升调咔哒 + 渗金加密（疮自己就是预告实体）
            float swell = Swell;
            if (!VaultUtils.isServer) {
                int clickGap = swell > 0.7f ? 5 : 8;
                if (Projectile.timeLeft % clickGap == 0) {
                    SoundEngine.PlaySound(SoundID.Item56 with {
                        Volume = 0.4f,
                        Pitch = -0.2f + 0.7f * swell,
                        MaxInstances = 5,
                    }, Projectile.Center);
                }
                if (Main.rand.NextBool(swell > 0.6f ? 3 : 7)) {
                    Dust seep = Dust.NewDustPerfect(
                        Projectile.Center + Main.rand.NextVector2Circular(10f + 6f * swell, 10f + 6f * swell),
                        DustID.Ichor, -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.6f),
                        40, default, Main.rand.NextFloat(0.7f, 1.1f));
                    seep.noGravity = false;
                }
                Lighting.AddLight(Projectile.Center, FssVfx.IchorGold.ToVector3() * (0.15f + 0.4f * swell));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!Stuck) {
                Projectile.localAI[0] = 1f;
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = Fuse;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
                    FssVfx.IchorBurst(Projectile.Center, 0.6f, -oldVelocity.SafeNormalize(-Vector2.UnitY));
                }
            }
            return false;
        }

        /// <summary>自然到期才喷发（黏附态）；被清弹 Kill 的安静退场</summary>
        public override void OnKill(int timeLeft) {
            bool naturalErupt = Stuck && timeLeft <= 0;

            if (!VaultUtils.isServer) {
                FssVfx.IchorBurst(Projectile.Center, naturalErupt ? 1.4f : 0.7f, -Vector2.UnitY);
                if (naturalErupt) {
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.8f, Pitch = 0.1f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            if (VaultUtils.isClient || !naturalErupt) {
                return;
            }
            //竖直灵液泉从疮体喷起（基点即疮位）
            Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<FssGeyserColumn>(),
                Projectile.damage, 0.5f, Main.myPlayer, 2f, 0f);
            //脚下近地补一小池（挂在墙上的不留）
            float groundY = FssVfx.FindGroundY(Projectile.Center - new Vector2(0f, 8f), 160f);
            if (groundY - Projectile.Center.Y < 140f) {
                FssIchorPool.TrySpawn(Projectile.GetSource_FromAI(),
                    new Vector2(Projectile.Center.X, groundY),
                    (int)(Projectile.damage * 0.6f), false);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float swell = Swell;
            //鼓胀呼吸：越临爆抖得越急
            float pulse = 1f + swell * 0.35f
                + MathF.Sin(Main.GlobalTimeWrappedHourly * (8f + swell * 22f)) * 0.05f * swell;

            //本体（坏死染色，漫反射乘光照）
            Color body = lightColor.MultiplyRGB(FssVfx.SkinMul);
            Main.EntitySpriteDraw(tex, drawPos, null, body, Projectile.rotation,
                origin, Projectile.scale * pulse, SpriteEffects.None, 0);

            //灵液鼓光：加色层随鼓胀增亮（充能读数）
            if (swell > 0.02f || !Stuck) {
                float glow = Stuck ? 0.2f + 0.6f * swell : 0.18f;
                Main.EntitySpriteDraw(tex, drawPos, null, FssVfx.IchorBright with { A = 0 } * glow,
                    Projectile.rotation, origin, Projectile.scale * pulse * 1.06f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
