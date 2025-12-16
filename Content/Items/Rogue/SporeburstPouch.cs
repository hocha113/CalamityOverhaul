using CalamityOverhaul.Content.Items.Ranged;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue
{
    /// <summary>
    /// 菌泡囊
    /// </summary>
    internal class SporeburstPouch : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "SporeburstPouch";
        public override void SetDefaults() {
            Item.width = Item.height = 22;
            Item.damage = 12;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<SporeburstPouchThrow>();
            Item.shootSpeed = 17f;
            Item.DamageType = CWRRef.GetRogueDamageClass();
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 0, 25, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            float stealthStrike = player.GetPlayerStealthStrikeAvailable() ? 1f : 0f;
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, stealthStrike);
            return false;
        }
    }

    internal class SporeburstPouchThrow : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "SporeburstPouch";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;//增加拖尾长度，让轨迹更平滑
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;//使用更平滑的中心点拖尾模式
        }
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 900;
            Projectile.DamageType = CWRRef.GetRogueDamageClass();
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//关闭接触伤害的冷却，因为伤害完全来自爆炸
            Projectile.extraUpdates = 1;
            Projectile.CWR().Viscosity = true;
        }

        //通过ai[0]判断是否为潜行攻击
        private bool IsStealthStrike => Projectile.ai[0] > 0f;

        public override void AI() {
            //赋予弹道视觉上的重量感和旋转
            Projectile.rotation += 0.3f * Math.Sign(Projectile.velocity.X);
            if (++Projectile.ai[1] > 30) {
                Projectile.velocity.X *= 0.98f;//轻微增加空气阻力
                Projectile.velocity.Y += 0.15f;//轻微增加重力
            }

            //光效，潜行攻击时更亮，颜色也不同
            float lightBrightness = IsStealthStrike ? 0.7f : 0.4f;
            Color lightColor = IsStealthStrike ? Color.Purple : Color.Cyan;
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * lightBrightness);

            //每隔一段时间“喷出”孢子粒子，增加动态效果
            if (Projectile.ai[1] % 10 == 0) {
                Vector2 dustVel = -Projectile.velocity * 0.1f;
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center, dustVel);
                if (prt != null) {
                    prt.Scale = 0.5f;
                }
            }
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //可以添加一个撞击音效
            SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.position);
            target.AddBuff(BuffID.Poisoned, 300);
        }

        public override void OnKill(int timeLeft) {
            //增加爆炸音效和屏幕震动，提升打击感
            SoundEngine.PlaySound(SoundID.Item14, Projectile.position);

            //根据是否为潜行攻击，决定爆炸规模
            int explosionRadius = IsStealthStrike ? 200 : 100;//潜行攻击的爆炸半径翻倍
            int sporeCount = IsStealthStrike ? 16 : 0;//只有潜行攻击才会爆出小孢子
            int explosionDamage = IsStealthStrike ? Projectile.damage * 2 : Projectile.damage;//潜行攻击的爆炸伤害更高

            //使用自定义的粒子效果替换原有的尘埃爆炸，效果更华丽
            for (int i = 0; i < 60; i++) {
                float speed = Main.rand.NextFloat(2f, IsStealthStrike ? 12f : 8f);
                Vector2 velocity = Main.rand.NextVector2Circular(speed, speed);
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center, velocity);
                if (prt != null) {
                    prt.Scale = Main.rand.NextFloat(0.5f, 1.2f);
                    prt.Lifetime = 40;
                    //潜行攻击的粒子颜色更丰富
                    prt.Color = IsStealthStrike ? Main.rand.NextFromList(Color.Cyan, Color.Purple, Color.LightGreen) : Color.White;
                }
            }

            //潜行攻击，爆发出追踪小孢子
            if (Projectile.IsOwnedByLocalPlayer() && sporeCount > 0) {
                for (int i = 0; i < sporeCount; i++) {
                    Vector2 velocity = VaultUtils.RandVr(6, 9);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity
                        , ModContent.ProjectileType<SporeBoboRogue>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
            }

            //造成范围伤害，并传递正确的伤害值
            Projectile.damage = explosionDamage;
            Projectile.Explode(explosionRadius);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2f + new Vector2(0f, Projectile.gfxOffY);
                //拖尾的颜色会随着时间变化，产生彩虹菌菇的效果
                Color color = Color.Lerp(Color.Cyan, Color.Purple, k / (float)Projectile.oldPos.Length) * (1f - k / (float)Projectile.oldPos.Length);
                float scale = Projectile.scale * (1f - k / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(texture, drawPos, null, color * 0.5f, Projectile.oldRot[k], origin, scale, SpriteEffects.None);
            }

            //绘制本体
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition
                , null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);

            //如果是潜行攻击，额外绘制一层发光蒙版，使其更加醒目
            if (IsStealthStrike) {
                //辉光会有呼吸般闪烁的效果
                float glowOpacity = (float)Math.Sin(Main.GameUpdateCount * 0.1f) * 0.2f + 0.5f;
                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null
                    , Color.White * glowOpacity, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            }

            return false;//因为我们自己实现了绘制，所以返回false
        }
    }

    internal class SporeBoboRogue : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;//减小碰撞体积，让它看起来更像孢子
            Projectile.DamageType = CWRRef.GetRogueDamageClass();
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.extraUpdates = 3;//降低额外更新，让弹道轨迹更清晰可见
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() {
            if (Projectile.ai[2] < 20) {
                return false;//只有在开始追踪时才开始造成伤害
            }
            return null;
        }

        public override void AI() {
            //给孢子一个更加“飘逸”和无规则的移动轨迹
            if (++Projectile.ai[2] > 20) {
                if (Projectile.ai[0] == 0 || Projectile.ai[1] == 0) {
                    NPC target = Projectile.Center.FindClosestNPC(1200);
                    if (target != null) {
                        Projectile.ai[0] = target.Center.X;
                        Projectile.ai[1] = target.Center.Y;
                    }
                }
                else {
                    //在追踪目标的同时，增加一个正弦函数的偏移，形成S型蛇皮走位
                    Vector2 targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                    Vector2 wobble = (Projectile.ai[2] * 0.1f).ToRotationVector2() * 8f;
                    Projectile.SmoothHomingBehavior(targetPos + wobble, 1, 0.05f);
                }
            }
            //孢子会随着时间慢慢减速
            Projectile.velocity *= 0.98f;

            //生成尾迹粒子
            if (Projectile.ai[2] > 1 && Main.rand.NextBool(2) && Projectile.velocity.Length() > 1f) {
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center, Projectile.velocity * -0.1f);
                if (prt != null) {
                    prt.Scale = 0.6f;
                    prt.shader = GameShaders.Armor.GetShaderFromItemId(Projectile.CWR().DyeItemID);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 120);//中毒
        }

        public override bool PreDraw(ref Color lightColor) {
            //因为没有纹理，我们自己绘制一个发光的孢子效果
            Texture2D flareTexture = TextureAssets.Extra[ExtrasID.SharpTears].Value;//使用一个游戏内的光斑素材
            Vector2 origin = flareTexture.Size() / 2;
            float scale = Projectile.scale * 0.5f;

            //绘制拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + Projectile.Size / 2f;
                Color trailColor = Color.Lerp(Color.MediumPurple, Color.Turquoise, (float)i / Projectile.oldPos.Length) * (1f - (float)i / Projectile.oldPos.Length);
                Main.EntitySpriteDraw(flareTexture, drawPos, null, trailColor * 0.5f, 0, origin, scale * (1f - (float)i / Projectile.oldPos.Length), SpriteEffects.None);
            }

            //绘制本体，让它看起来像一个能量核心
            Main.EntitySpriteDraw(flareTexture, Projectile.Center - Main.screenPosition, null, Color.DodgerBlue, 0, origin, scale, SpriteEffects.None);
            Main.EntitySpriteDraw(flareTexture, Projectile.Center - Main.screenPosition, null, Color.DeepSkyBlue * 0.5f, 0, origin, scale * 1.5f, SpriteEffects.None);

            return false;
        }
    }
}