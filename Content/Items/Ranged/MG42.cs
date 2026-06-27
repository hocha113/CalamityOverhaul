using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class MG42 : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "MG42";
        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.damage = 882;
            Item.useAnimation = Item.useTime = 5;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 1.5f;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 12;
            Item.useAmmo = AmmoID.Bullet;
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.DamageType = DamageClass.Ranged;
            Item.value = Terraria.Item.buyPrice(3, 53, 5, 0);
            Item.crit = 2;
        }

        //物品使用本身不消耗子弹，由手持弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<MG42Held>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<MG42Held>(player, source);
    }

    internal class MG42Held : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "MG42";
        public override int TargetID => ModContent.ItemType<MG42>();
        public override SoundStyle? ShootSound => CWRSound.Gun_AWP_Shoot with { Pitch = -0.1f, Volume = 0.15f };
        [VaultLoaden(CWRConstant.Item_Ranged + "MG42_Masking")]
        private static Asset<Texture2D> masking = null;
        private float randomShootRotset;
        private float shootValue;
        public override void SetGunProperty() {
            HandIdleDistanceX = 36;
            HandIdleDistanceY = -4;
            HandFireDistanceX = 36;
            HandFireDistanceY = -10;
            MuzzleForwardOffset = 46;
        }

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write(randomShootRotset);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            randomShootRotset = reader.ReadSingle();
        }

        //开火旋转角附加散布抖动
        public override float GetAimGunRot() => base.GetAimGunRot() + randomShootRotset;

        public override void AI() {
            UpdateHeldPose(WantsFireLeft);

            //枪管过热的冷却与冒烟
            if (shootValue > 0) {
                shootValue -= 0.02f;
            }
            if (shootValue > 16) {
                shootValue = 16;
            }
            if (shootValue > 10) {
                if (Main.rand.NextBool(6)) {
                    Vector2 spanPos = ShootPos + ShootVelocityInProjRot.UnitVector() * Main.rand.NextFloat(-33f, 42f);
                    Dust.NewDust(spanPos, 3, 3, DustID.Smoke, 0, -3, 55, Scale: Main.rand.NextFloat(1, 3));
                }
            }

            if (WantsFireLeft && FireCooldown <= 0 && HasAmmo) {
                Fire();
                SetFireCooldown();
            }
            Time++;
        }

        private void Fire() {
            //每发刷新一次散布抖动
            if (Projectile.IsOwnedByLocalPlayer()) {
                randomShootRotset = Main.rand.NextFloat(-0.06f, 0.06f);
                NetUpdate();
            }
            shootValue += 0.4f;

            SnapToAimPose();
            PlayShootSound();
            CreateRecoil();
            CreateFireLight();

            if (Projectile.IsOwnedByLocalPlayer()) {
                //普通子弹会被转化为硝化弹
                int shootType = AmmoTypes == ProjectileID.Bullet ? CWRID.Proj_NitroShot : AmmoTypes;
                Projectile bullet = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocityInProjRot
                    , shootType, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                bullet.ArmorPenetration = 20;
                if (shootValue > 10) {
                    bullet.scale *= 2;
                }
                bullet.netUpdate = true;
            }
            ConsumeAmmo();
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            base.GunDraw(drawPos, ref lightColor);

            //过热发红的枪管遮罩
            Color maskingColor = lightColor;
            if (shootValue > 0) {
                maskingColor = VaultUtils.MultiStepColorLerp(shootValue / 16f, lightColor, Color.Red);
            }
            Main.EntitySpriteDraw(masking.Value, drawPos, null, maskingColor
                , Projectile.rotation, masking.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
