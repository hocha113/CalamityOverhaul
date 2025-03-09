using CalamityMod.Projectiles.Boss;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 荒芜牙猎
    /// </summary>
    internal class WastelandFang : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "WastelandFang";
        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 60;
            Item.damage = 15;
            Item.DamageType = DamageClass.MeleeNoSpeed;
            Item.useTime = 50;
            Item.useAnimation = 50;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 10f;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 0, 80, 10);
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.channel = true;
            Item.shoot = ModContent.ProjectileType<WastelandFangProj>();
            Item.shootSpeed = 22f;
        }
    }

    internal class WastelandFangProj : BaseHeldProj, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Melee + "WastelandFang";
        private bool isShoot;
        private static Asset<Texture2D> chain;
        private static Asset<Texture2D> chainAlt;
        private static Asset<Texture2D> head;
        private static Asset<Texture2D> tooth;
        private static Asset<Texture2D> toothAlt;
        void ICWRLoader.LoadAsset() {
            chain = CWRUtils.GetT2DAsset(Texture + "Chain");
            chainAlt = CWRUtils.GetT2DAsset(Texture + "ChainAlt");
            head = CWRUtils.GetT2DAsset(Texture + "Head");
            tooth = CWRUtils.GetT2DAsset(Texture + "Tooth");
            toothAlt = CWRUtils.GetT2DAsset(Texture + "ToothAlt");
        }
        void ICWRLoader.UnLoadData() {
            chain = null;
            chainAlt = null;
            head = null;
            tooth = null;
        }
        public override void SetDefaults() {
            Projectile.width = 52;
            Projectile.height = 52;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI() {
            Vector2 ownerToDistance = Owner.Center.To(Projectile.Center);
            Owner.ChangeDir(Math.Sign(ownerToDistance.X));
            Projectile.rotation = ownerToDistance.ToRotation() - MathHelper.PiOver2;
            if (Projectile.ai[0] < 30 && Projectile.ai[0] > 10) {
                Projectile.velocity *= 0.9f;
                if (Projectile.ai[0] == 16) {
                    SoundEngine.PlaySound(SoundID.NPCDeath13, Projectile.Center);
                    for (int i = 0; i < 4; i++) {
                        Vector2 velocity = new Vector2(Projectile.velocity.X * (0.6f + i * 0.1f), Projectile.velocity.Y);
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, velocity
                        , ModContent.ProjectileType<DesertScourgeSpit>(), Projectile.damage / 2, Projectile.knockBack, Projectile.owner);
                        Main.projectile[proj].hostile = false;
                        Main.projectile[proj].friendly = true;
                        NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, proj);
                    }
                    isShoot = true;
                }
            }

            if (Projectile.ai[0] > 36) {
                if (Projectile.ai[0] > 80) {//如果时间太长了就设置弹幕可以穿墙收回防止卡墙角
                    Projectile.tileCollide = false;
                }
                if (isShoot) {
                    Projectile.ChasingBehavior(Owner.Center, 18);
                    if (ownerToDistance.Length() < Projectile.width) {
                        Projectile.Kill();
                    }
                }
                else if (Projectile.ai[0] > 60) {
                    Projectile.ChasingBehavior(Owner.Center, 18);
                    if (ownerToDistance.Length() < Projectile.width) {
                        Projectile.Kill();
                    }
                }
            }

            Owner.itemTime = 10;
            Owner.itemAnimation = 10;

            float targetNum = 0;
            if (Projectile.ai[0] > 10 && Projectile.ai[0] < 36) {
                targetNum = 60 * CWRUtils.atoR;
            }
            Projectile.ai[1] = MathHelper.Lerp(Projectile.ai[1], targetNum, 0.2f);

            Projectile.ai[0]++;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.ai[0] < 40) {
                if (!Main.dedServ) {
                    SoundStyle sound = SoundID.DD2_MonkStaffGroundImpact;
                    sound.Pitch = -0.3f;
                    SoundEngine.PlaySound(sound, Projectile.position);
                    Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
                }

                Projectile.velocity = new Vector2(Projectile.velocity.X / 2, Projectile.velocity.Y / -2);
                Projectile.ai[0] = 40;
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 origPos = Owner.GetPlayerStabilityCenter();
            Vector2 endPos = Projectile.Center + new Vector2(0, Projectile.gfxOffY);
            Vector2 toPos = origPos.To(endPos);
            Vector2 drawChainOrig = chain.Size() / 2;
            Vector2 drawChainAltOrig = chainAlt.Size() / 2;
            float drawChainRot = toPos.ToRotation() + MathHelper.PiOver2;
            float lengs = Owner.Distance(endPos);
            float chainBodyTrs = 14;
            float num = lengs / chainBodyTrs;
            for (int i = 0; i < num; i++) {
                Texture2D chainValue = chain.Value;
                Vector2 newOrig = drawChainOrig;
                if (i % 2 == 0 && i > 0) {
                    chainValue = chainAlt.Value;
                    newOrig = drawChainAltOrig;
                }
                Vector2 drawChainPos = origPos + toPos.UnitVector() * i * chainBodyTrs - Main.screenPosition;
                Color chainLightColor = Lighting.GetColor(CWRUtils.WEPosToTilePos(drawChainPos + Main.screenPosition).ToPoint());
                Main.EntitySpriteDraw(chainValue, drawChainPos, null, chainLightColor
                , drawChainRot, newOrig, Projectile.scale, SpriteEffects.None, 0);
            }

            Vector2 drawToothPos = endPos - Main.screenPosition + Projectile.velocity.UnitVector() * Projectile.ai[1] * 10;

            Vector2 drawToothOrig = new Vector2(22, 2);
            float drawToothRot = Projectile.rotation + Projectile.ai[1];
            Main.EntitySpriteDraw(tooth.Value, drawToothPos, null, lightColor
                , drawToothRot, drawToothOrig, Projectile.scale, SpriteEffects.None, 0);

            Vector2 drawToothOrig2 = new Vector2(4, 2);
            float drawToothRot2 = Projectile.rotation - Projectile.ai[1];
            Main.EntitySpriteDraw(toothAlt.Value, drawToothPos, null, lightColor
                , drawToothRot2, drawToothOrig2, Projectile.scale, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(head.Value, endPos - Main.screenPosition, null, lightColor
                , Projectile.rotation, head.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
