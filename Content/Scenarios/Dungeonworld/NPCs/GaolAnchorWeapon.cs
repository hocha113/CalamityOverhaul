using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 狱锚：不溺者掉落的近战投掷武器（33%），原版锚武器的加强重制。
    /// 掷出后命中墙面短暂钉线：玩家↔锚之间绷出一道伤害锁链（Boss 招式的玩家版回声），
    /// 钉线结束自动收回。同屏限一发（链只有一条）。贴图借原版锚（零新画像素）
    /// </summary>
    internal class GaolAnchorWeapon : UndrownedModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Anchor;

        public override void SetDefaults() {
            Item.width = 34;
            Item.height = 34;
            Item.damage = 52;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 7f;
            Item.useTime = 34;
            Item.useAnimation = 34;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<GaolAnchorProj>();
            Item.shootSpeed = 15f;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 2);
            Item.UseSound = SoundID.Item1;
        }

        public override bool CanUseItem(Player player) {
            //链只有一条：在场有自己的锚就不再掷
            return player.ownedProjectileCounts[ModContent.ProjectileType<GaolAnchorProj>()] < 1;
        }
    }

    /// <summary>
    /// 狱锚弹幕：掷出（微坠）→ 嵌墙钉线 45f（玩家↔锚链线对敌判定）→ 收回归手。
    /// ai[0]=相位（0 飞行 / 1 钉线 / 2 收回），归属端推演并盖 netUpdate；
    /// 命中判定全在归属端（原版友方弹幕契约），嵌定对同步 tile 确定性判定
    /// </summary>
    internal class GaolAnchorProj : UndrownedModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Anchor;

        private const int PinFrames = 45;
        private const float LineWidth = 12f;

        private ref float Phase => ref Projectile.ai[0];
        private ref float PinTimer => ref Projectile.ai[1];

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            //钉线期反复命中同一目标的节流
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if ((int)Phase == 0) {
                //掷出：重物微坠，锚头顺飞行向
                Projectile.velocity.Y += 0.25f;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                //飞远自动转收回（不无限飞）
                if (Vector2.Distance(owner.Center, Projectile.Center) > 560f) {
                    Phase = 2;
                    Projectile.netUpdate = true;
                }
                return;
            }

            if ((int)Phase == 1) {
                //钉线：锚静止，链绷直（伤害线在 Colliding 门控）
                Projectile.velocity = Vector2.Zero;
                PinTimer++;
                if (!Main.dedServ && (int)PinTimer % 8 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        Vector2.Lerp(owner.Center, Projectile.Center, Main.rand.NextFloat()),
                        Main.rand.NextVector2Circular(0.6f, 0.6f),
                        Color.Lerp(Undrowned.RustOrange, Color.White, 0.4f),
                        Main.rand.NextFloat(0.25f, 0.45f))?.Configure(true, Main.rand.Next(6, 12));
                }
                if (PinTimer >= PinFrames) {
                    Phase = 2;
                    Projectile.netUpdate = true;
                }
                return;
            }

            //收回：加速归手
            Projectile.tileCollide = false;
            Vector2 to = owner.Center - Projectile.Center;
            if (to.Length() < 34f) {
                Projectile.Kill();
                return;
            }
            float speed = MathF.Min(9f + PinTimer * 0.1f, 22f);
            PinTimer++;
            Projectile.velocity = to.SafeNormalize(Vector2.UnitX) * speed;
            Projectile.rotation += 0.3f * MathF.Sign(Projectile.velocity.X + 0.01f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //嵌墙钉线（各端对同步 tile 确定性一致）
            if ((int)Phase == 0) {
                Phase = 1;
                PinTimer = 0;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.6f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 2 }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int k = 0; k < 5; k++) {
                        PRTLoader.NewParticle<PRT_RustFleck>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                            Main.rand.NextVector2Circular(1.6f, 1.2f) - new Vector2(0f, 0.6f),
                            Color.Lerp(Undrowned.RustOrange, Undrowned.RustDeep, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(14, 24));
                    }
                }
            }
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //钉线期：锚体 + 玩家到锚的链线
            if ((int)Phase == 1) {
                if (projHitbox.Intersects(targetHitbox)) {
                    return true;
                }
                Player owner = Owner;
                if (owner != null && owner.active) {
                    float _ = 0f;
                    return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                        owner.Center, Projectile.Center, LineWidth, ref _);
                }
            }
            return null;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.Anchor);
            Texture2D tex = TextureAssets.Item[ItemID.Anchor]?.Value;
            Texture2D chainTex = TextureAssets.Chain22?.Value;
            if (tex == null || chainTex == null) {
                return false;
            }
            Player owner = Owner;
            if (owner != null && owner.active) {
                Undrowned.DrawChainLine(Main.spriteBatch, chainTex, owner.Center, Projectile.Center, lightColor, 1f);
            }
            Undrowned.DrawAnchor(Main.spriteBatch, tex, Projectile.Center, Projectile.rotation, lightColor, 1f);
            return false;
        }
    }
}
