using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.BloomTomes
{
    /// <summary>
    /// 蕾典，荒花魔典。发射会开花的蕾弹，命中或落地绽放花瓣圈；
    /// 每第四发化为盛放弹，花瓣更多并向上崩针。实体见 <see cref="BloomTomeBolt"/>
    /// </summary>
    internal class BloomTome : BssModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "BloomTome";

        /// <summary>施法计数，每第四发盛放（物品实例状态，仅主人端 Shoot 里推进）</summary>
        private int castCount;

        public override void SetDefaults() {
            Item.width = 42;
            Item.height = 44;
            Item.damage = 14;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 8;
            Item.useTime = Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2.5f;
            Item.UseSound = SoundID.Item43;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<BloomTomeBolt>();
            Item.shootSpeed = 11.5f;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 1, 20);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            castCount++;
            bool blooming = castCount % 4 == 0;
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback,
                player.whoAmI, ai0: blooming ? 1f : 0f);
            return false;
        }
    }

    /// <summary>
    /// 蕾弹：书页里掷出的花蕾，后段微坠，命中敌人或落地绽放花瓣圈。
    /// ai[0]=1 为盛放弹：更大，花瓣更多，并向上崩出两根荒针
    /// </summary>
    internal class BloomTomeBolt : BssModProjectile
    {
        public override string Texture => CWRConstant.NPC + "BSS/BloomBud";

        /// <summary>1 为盛放弹</summary>
        private ref float Blooming => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.scale = Blooming > 0f ? 1.1f : 0.85f;
            }

            //后段微坠：蕾有分量
            if (++Projectile.localAI[1] > 30f) {
                Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y + 0.08f, -18f, 10f);
            }
            //贴图茎在上：让蕾尖顺着飞行方向
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            if (!Main.dedServ && Main.rand.NextBool(Blooming > 0f ? 3 : 7)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center,
                    Main.rand.NextBool() ? DustID.RedTorch : DustID.JunglePlants,
                    -Projectile.velocity * 0.05f, 150, default, 0.7f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = 0.25f, Volume = 0.85f }, Projectile.Center);

            bool blooming = Blooming > 0f;
            BloomArsenal.PetalRing(Projectile, Projectile.Center, blooming ? 8 : 4,
                (int)(Projectile.damage * (blooming ? 0.7f : 0.6f)), 3f, blooming ? 6f : 5f);
            if (blooming) {
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 vel = new Vector2(0f, -6.2f).RotatedBy(i * 0.3f) * Main.rand.NextFloat(0.9f, 1.1f);
                    BloomArsenal.ShedNeedle(Projectile, Projectile.Center, vel,
                        (int)(Projectile.damage * 0.5f), 3f, gravity: true);
                }
            }

            if (!Main.dedServ) {
                for (int i = 0; i < (blooming ? 5 : 3); i++) {
                    BssVfx.PetalDrift(Projectile.Center, Main.rand.NextVector2Circular(1.2f, 0.8f), 0.65f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            Color trailTint = lightColor.MultiplyRGB(BloomArsenal.Bloom);
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, trailTint * (0.3f * t), Projectile.rotation,
                    origin, Projectile.scale * 0.9f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            //盛放弹带一层暖光
            if (Blooming > 0f) {
                Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                    new Color(255, 80, 70, 0) * 0.35f, Projectile.rotation, origin,
                    Projectile.scale * 1.15f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
