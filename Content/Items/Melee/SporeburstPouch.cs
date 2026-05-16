using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue
{
    /// <summary>
    /// 菌泡囊 —— 战士的菌孢破片手雷
    /// 砸向敌人后引爆，迸发毒孢冲击波，并向四周喷射追踪小孢子破片
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
            Item.UseSound = SoundID.Item1 with { Pitch = -0.1f };
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<SporeburstPouchThrow>();
            Item.shootSpeed = 17f;
            Item.DamageType = DamageClass.Melee;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 0, 25, 0);
        }
    }

    /// <summary>
    /// 菌孢手雷弹体
    /// 受重力旋转下落，命中或寿命结束时爆炸并向四周喷射追踪孢子破片
    /// </summary>
    internal class SporeburstPouchThrow : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "SporeburstPouch";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 900;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            //旋转下落，营造手雷的重量感
            Projectile.rotation += 0.3f * Math.Sign(Projectile.velocity.X);
            if (++Projectile.ai[1] > 30) {
                Projectile.velocity.X *= 0.98f;
                Projectile.velocity.Y += 0.15f;
            }

            //菌孢手雷的青色辉光
            Lighting.AddLight(Projectile.Center, new Color(120, 220, 200).ToVector3() * 0.45f);

            //周期性渗出菌孢
            if (Projectile.ai[1] % 10 == 0) {
                Vector2 dustVel = -Projectile.velocity * 0.1f;
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center, dustVel);
                if (prt != null) {
                    prt.Scale = 0.5f;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.position);
            target.AddBuff(BuffID.Poisoned, 300);
        }

        public override void OnKill(int timeLeft) {
            //战士手雷: 引爆音效 + 屏震
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.1f, Volume = 0.85f }, Projectile.position);

            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Projectile.Center, Vector2.UnitX, 3f, 4.5f, 6, 500f, FullName));
            }

            //菌孢爆裂粒子
            for (int i = 0; i < 60; i++) {
                float speed = Main.rand.NextFloat(2f, 9f);
                Vector2 velocity = Main.rand.NextVector2Circular(speed, speed);
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center, velocity);
                if (prt != null) {
                    prt.Scale = Main.rand.NextFloat(0.5f, 1.2f);
                    prt.Lifetime = 40;
                    prt.Color = Main.rand.NextFromList(Color.Cyan, Color.Purple, Color.LightGreen, Color.White);
                }
            }

            //发射追踪孢子破片
            if (Projectile.IsOwnedByLocalPlayer()) {
                int sporeCount = 8;
                for (int i = 0; i < sporeCount; i++) {
                    Vector2 velocity = VaultUtils.RandVr(6, 9);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity
                        , ModContent.ProjectileType<SporeBoboRogue>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
            }

            //范围爆炸伤害
            Projectile.Explode(150);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2;

            //彩虹菌孢拖尾
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2f
                    + new Vector2(0f, Projectile.gfxOffY);
                Color color = Color.Lerp(Color.Cyan, Color.Purple, k / (float)Projectile.oldPos.Length)
                    * (1f - k / (float)Projectile.oldPos.Length);
                float scale = Projectile.scale * (1f - k / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(texture, drawPos, null, color * 0.5f, Projectile.oldRot[k],
                    origin, scale, SpriteEffects.None);
            }

            //本体
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);

            return false;
        }
    }

    /// <summary>
    /// 菌孢破片 —— 战士手雷爆炸时迸射的小型追踪破片
    /// 短暂滞空后锁定附近敌人并以蛇皮走位追击
    /// (类名沿用历史命名 SporeBoboRogue 以保持本地化兼容)
    /// </summary>
    internal class SporeBoboRogue : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.extraUpdates = 3;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() {
            //滞空蓄势阶段不造成伤害
            if (Projectile.ai[2] < 20) {
                return false;
            }
            return null;
        }

        public override void AI() {
            //蛇皮追击逻辑
            if (++Projectile.ai[2] > 20) {
                if (Projectile.ai[0] == 0 || Projectile.ai[1] == 0) {
                    NPC target = Projectile.Center.FindClosestNPC(1200);
                    if (target != null) {
                        Projectile.ai[0] = target.Center.X;
                        Projectile.ai[1] = target.Center.Y;
                    }
                }
                else {
                    Vector2 targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
                    Vector2 wobble = (Projectile.ai[2] * 0.1f).ToRotationVector2() * 8f;
                    Projectile.SmoothHomingBehavior(targetPos + wobble, 1, 0.05f);
                }
            }

            //缓慢减速
            Projectile.velocity *= 0.98f;

            //尾迹粒子
            if (Projectile.ai[2] > 1 && Main.rand.NextBool(2) && Projectile.velocity.Length() > 1f) {
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center, Projectile.velocity * -0.1f);
                if (prt != null) {
                    prt.Scale = 0.6f;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D flareTexture = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            Vector2 origin = flareTexture.Size() / 2;
            float scale = Projectile.scale * 0.5f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + Projectile.Size / 2f;
                Color trailColor = Color.Lerp(Color.MediumPurple, Color.Turquoise, i / (float)Projectile.oldPos.Length)
                    * (1f - i / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(flareTexture, drawPos, null, trailColor * 0.5f, 0, origin,
                    scale * (1f - i / (float)Projectile.oldPos.Length), SpriteEffects.None);
            }

            Main.EntitySpriteDraw(flareTexture, Projectile.Center - Main.screenPosition, null,
                Color.DodgerBlue, 0, origin, scale, SpriteEffects.None);
            Main.EntitySpriteDraw(flareTexture, Projectile.Center - Main.screenPosition, null,
                Color.DeepSkyBlue * 0.5f, 0, origin, scale * 1.5f, SpriteEffects.None);

            return false;
        }
    }
}
