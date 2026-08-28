using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items
{
    /// <summary>
    /// 荒花沙蟒武器系列（蕾锋/棘弦/唤蕾号/蕾冠）共用件：系列调色板与友方花瓣、荒针弹幕。
    /// 弹幕伤害类别由 ai[2] 档位决定（0 近战 1 远程 2 召唤），各端每帧从同步的 ai 值推导
    /// </summary>
    internal static class BloomArsenal
    {
        /// <summary>绯红花瓣色（与 <see cref="NPCs.BloomsandSerpents.Projectiles.BssPetalProj"/> 同源）</summary>
        public static readonly Color Bloom = new(215, 70, 78);
        /// <summary>仙掌绿</summary>
        public static readonly Color Leaf = new(120, 150, 62);
        /// <summary>沙壳浅褐</summary>
        public static readonly Color Husk = new(219, 162, 108);

        /// <summary>ai[2] 伤害类别档位</summary>
        public static DamageClass DamageClassOf(float flag) => flag switch {
            1f => DamageClass.Ranged,
            2f => DamageClass.Summon,
            _ => DamageClass.Melee,
        };

        /// <summary>以 center 为心向外绽放一圈友方花瓣。内部只在弹幕主人端生成</summary>
        public static void PetalRing(Projectile source, Vector2 center, int count, int damage, float classFlag, float speed = 5.5f) {
            if (!source.IsOwnedByLocalPlayer()) {
                return;
            }
            float baseRot = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < count; i++) {
                Vector2 vel = (baseRot + MathHelper.TwoPi * i / count).ToRotationVector2()
                    * speed * Main.rand.NextFloat(0.85f, 1.1f);
                Projectile.NewProjectile(source.GetSource_FromAI(), center, vel,
                    ModContent.ProjectileType<BloomPetalProj>(), damage, 1.5f, source.owner, ai2: classFlag);
            }
        }

        /// <summary>撒一根友方荒针。gravity 打开走抛物线。内部只在弹幕主人端生成</summary>
        public static void ShedNeedle(Projectile source, Vector2 center, Vector2 velocity, int damage, float classFlag, bool gravity) {
            if (!source.IsOwnedByLocalPlayer()) {
                return;
            }
            Projectile.NewProjectile(source.GetSource_FromAI(), center, velocity,
                ModContent.ProjectileType<BloomNeedleProj>(), damage, 1f, source.owner,
                ai1: gravity ? 1f : 0f, ai2: classFlag);
        }
    }

    /// <summary>
    /// 荒花瓣（友方）：从绽放点向外飞散后减速凋落，原版花瓣贴图压成绯红。
    /// ai[2]=伤害类别档位
    /// </summary>
    internal class BloomPetalProj : BssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 50;
            Projectile.scale = 1.25f;
        }

        public override void AI() {
            Projectile.DamageType = BloomArsenal.DamageClassOf(Projectile.ai[2]);
            float age = ++Projectile.localAI[0];

            //冲出去的劲逐渐让位给飘
            Projectile.velocity *= 0.965f;
            Projectile.velocity.Y += 0.03f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2
                + MathF.Sin(age * 0.17f + Projectile.identity) * 0.35f;

            if (!Main.dedServ && Main.rand.NextBool(10)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, Vector2.Zero, 160, default, 0.6f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            BssVfx.PetalDrift(Projectile.Center, Main.rand.NextVector2Circular(0.8f, 0.5f), 0.6f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            //末段淡出
            float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            Color body = lightColor.MultiplyRGB(BloomArsenal.Bloom) * fade;

            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, body * (0.3f * t), Projectile.rotation,
                    origin, Projectile.scale * 0.9f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                body, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            //瓣心薄薄一层暖光
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 60, 66, 0) * (0.3f * fade), Projectile.rotation, origin,
                Projectile.scale * 1.1f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 荒针（友方）：仙人掌钉刺的玩家版，沿用 boss 素材与残影质感。
    /// ai[1]=1 抛物线坠落，ai[2]=伤害类别档位
    /// </summary>
    internal class BloomNeedleProj : BssModProjectile
    {
        public override string Texture => CWRConstant.NPC + "BSS/Needle";

        /// <summary>贴图尖端朝向（素材尖朝左下，与 BssNeedleProj 一致）</summary>
        private const float AuthoredTipAngle = 2.356f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 120;
            Projectile.scale = 1.05f;
        }

        public override void AI() {
            Projectile.DamageType = BloomArsenal.DamageClassOf(Projectile.ai[2]);

            if (Projectile.ai[1] > 0f) {
                Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y + 0.22f, -20f, 13f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() - AuthoredTipAngle;

            if (!Main.dedServ && Main.rand.NextBool(7)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                    -Projectile.velocity * 0.05f, 140, default, 0.65f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                    Main.rand.NextVector2Circular(1.3f, 1.3f), 120, default, Main.rand.NextFloat(0.55f, 0.85f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, lightColor * (0.3f * t), Projectile.rotation,
                    origin, Projectile.scale * (0.8f + 0.2f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
