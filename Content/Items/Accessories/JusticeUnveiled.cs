using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Projectiles.Rogue;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Projectiles.Weapons;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.Longinus;
using CalamityOverhaul.Content.RemakeItems.Melee;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    /// <summary>
    /// 正义的显现
    /// </summary>
    internal class JusticeUnveiled : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "JusticeUnveiled";
        public const int DropProbabilityDenominator = 6000;
        private static bool OnLoaden;
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 2, 15, 0);
            Item.rare = ModContent.RarityType<Turquoise>();
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.CWR().IsJusticeUnveiled = true;
            //检测换弹
            if (player.CWR().PlayerIsKreLoadTime > 0) {
                OnLoaden = true;
            }
        }

        public static bool SpwanBool(Player player, Projectile projectile, NPC target, NPC.HitInfo hit) {
            int type = ModContent.ProjectileType<DivineJustice>();
            int type2 = ModContent.ProjectileType<JusticeUnveiledExplode>();
            int type3 = ModContent.ProjectileType<JUZenithWorldTime>();

            if (projectile.numHits > 0) {
                return false;
            }
            if (projectile.type == type || projectile.type == type2) {
                return false;
            }

            if (!player.CWR().IsJusticeUnveiled) {
                return false;
            }
            if (player.ownedProjectileCounts[type] > 0 || player.ownedProjectileCounts[type2] > 0) {
                return false;
            }

            if (Main.zenithWorld) {
                if (player.GetProjectileHasNum(type3) == 0) {
                    Projectile.NewProjectile(player.FromObjectGetParent(), target.Center, Vector2.Zero, type3, 0, 0, player.whoAmI);
                    return true;
                }
                else {
                    return false;
                }
            }

            Item item = player.GetItem();
            if (item.type > ItemID.None && item.CWR().HasCartridgeHolder && item.CWR().AmmoCapacity <= 20) {
                if (OnLoaden) {
                    OnLoaden = false;
                    return true;
                }
            }

            if (projectile.type == ModContent.ProjectileType<StellarContemptEcho>()
                || projectile.type == ModContent.ProjectileType<GalaxySmasherEcho>()
                || projectile.type == ModContent.ProjectileType<TriactisHammerProj>()
                || projectile.type == ModContent.ProjectileType<LonginusThrow>()) {
                return true;
            }

            if (projectile.type == ModContent.ProjectileType<ExorcismProj>() && projectile.Calamity().stealthStrike) {
                return true;
            }

            if (projectile.DamageType != DamageClass.Ranged) {
                return false;
            }
            if (!hit.Crit) {
                return false;
            }
            return true;
        }

        public static void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) {
            if (Main.player[projectile.owner].CWR().IsJusticeUnveiled && !Main.zenithWorld) {
                modifiers.CritDamage *= 0;//制造暴击伤害缩放
            }
        }

        public static void OnHitNPCSpwanProj(Player player, Projectile projectile, NPC target, NPC.HitInfo hit) {
            if (SpwanBool(player, projectile, target, hit)) {
                if (Main.zenithWorld && projectile.type == ModContent.ProjectileType<LonginusThrow>()) {
                    foreach (var npc in Main.ActiveNPCs) {
                        if (npc.friendly) {
                            continue;
                        }
                        Projectile.NewProjectile(player.FromObjectGetParent()
                        , npc.Center + new Vector2(0, -1120), new Vector2(0, 6)
                        , ModContent.ProjectileType<DivineJustice>(), projectile.damage, 2, player.whoAmI, npc.whoAmI);
                    }
                    return;
                }
                else {
                    Projectile.NewProjectile(player.FromObjectGetParent()
                    , target.Center + new Vector2(0, -1120), new Vector2(0, 6)
                    , ModContent.ProjectileType<DivineJustice>(), projectile.damage, 2, player.whoAmI, target.whoAmI);
                }
            }
        }
    }

    internal class JUZenithWorldTime : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
        }
        public override void AI() => Projectile.Center = Owner.GetPlayerStabilityCenter();
        public override bool PreDraw(ref Color lightColor) {
            RDefiledGreatsword.DrawRageEnergyChargeBar(Owner, 255, Projectile.timeLeft / 300f);
            return false;
        }
    }

    internal class DivineJustice : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 190;
            Projectile.extraUpdates = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanHitNPC(NPC target) {
            if (CWRUtils.GetNPCInstance((int)Projectile.ai[0]) != null && target.whoAmI == Projectile.ai[0]) {
                return true;
            }
            return false;
        }

        public override void AI() {
            PRT_Spark spark = new PRT_Spark(Projectile.Center, new Vector2(0, 2), false, 22, 1, Color.Gold);
            PRTLoader.AddParticle(spark);
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer() && CWRUtils.GetNPCInstance((int)Projectile.ai[0]) != null) {
                if (Main.zenithWorld) {
                    SoundEngine.PlaySound(SpearOfLonginus.AT, Projectile.Center);
                }
                else {
                    SoundEngine.PlaySound(CWRSound.JustStrike, Projectile.Center);
                }

                Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<JusticeUnveiledExplode>(), Projectile.damage, 2, Projectile.owner, Projectile.ai[0]);
                PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center,
                        new Vector2(0, 2), 10f, 6f, 20, 1000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }
    }

    internal class JusticeUnveiledExplode : ModProjectile, ICWRLoader
    {
        public override string Texture => CWRConstant.Projectile + "JusticeUnveiledExplode";
        public const int maxFrame = 14;
        private int frameIndex = 0;
        private int time;
        public static Asset<Texture2D> MaskLaserLine;
        void ICWRLoader.LoadAsset() => MaskLaserLine = CWRUtils.GetT2DAsset(CWRConstant.Masking + "MaskLaserLine");
        void ICWRLoader.UnLoadData() => MaskLaserLine = null;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 332;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (++time < 6) {
                return;
            }
            if (++Projectile.frameCounter > 3) {
                frameIndex++;
                if (frameIndex == 4) {
                    PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center,
                        new Vector2(0, 2), 10f, 6f, 20, 1000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }
                if (frameIndex >= maxFrame) {
                    Projectile.Kill();
                    frameIndex = 0;
                }
                Projectile.frameCounter = 0;
            }
            Projectile.scale += 0.02f;

            if (Projectile.ai[1] < 4 && Projectile.ai[2] == 0) {
                Projectile.ai[1]++;
            }
            if (frameIndex > 8) {
                Projectile.ai[1] = 1;
            }
            if (Projectile.ai[2] > 0 && Projectile.ai[1] > 0) {
                Projectile.ai[1]--;
            }

            NPC npc = CWRUtils.GetNPCInstance((int)Projectile.ai[0]);
            if (npc != null) {
                Projectile.Bottom = npc.Bottom;
            }
        }

        public override bool? CanHitNPC(NPC target) {
            if (!(frameIndex == 4 || frameIndex == 8)) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (time < 6) {
                return false;
            }
            Color drawColor = Color.Gold;
            drawColor.A = 0;
            Main.EntitySpriteDraw(MaskLaserLine.Value, Projectile.Bottom - Main.screenPosition, null, drawColor
                , Projectile.rotation - MathHelper.PiOver2, MaskLaserLine.Value.Size() / 2
                , new Vector2(4000, Projectile.ai[1] * 0.04f), SpriteEffects.None, 0);

            Texture2D value = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = CWRUtils.GetRec(value, frameIndex, maxFrame);
            Main.spriteBatch.Draw(value, Projectile.Bottom - Main.screenPosition + new Vector2(0, 22 * Projectile.scale)
                , rectangle, Color.White, Projectile.rotation, new Vector2(rectangle.Width / 2, rectangle.Height)
                , Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
