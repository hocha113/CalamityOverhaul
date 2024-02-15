using CalamityMod;
using CalamityMod.Projectiles.BaseProjectiles;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RGildedProboscisProj : BaseSpearProjectile
    {
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<Items.Melee.GildedProboscis>();

        public override float InitialSpeed => 3f;

        public override float ReelbackSpeed => 2.4f;

        public override float ForwardSpeed => 0.95f;

        public override string Texture => CWRConstant.Projectile_Melee + "GildedProboscisProj";

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 4;
        }

        public override void OnSpawn(IEntitySource source) {
            if (Projectile.IsOwnedByLocalPlayer())
                gildedProboscis.CWR().MeleeCharge = 0;
        }

        public override void OnKill(int timeLeft) {
            Projectile projectile = CWRUtils.GetProjectileInstance(projIndex);
            projectile?.Kill();
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(projIndex);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            projIndex = reader.ReadInt32();
        }

        Player Owner => CWRUtils.GetPlayerInstance(Projectile.owner);
        Item gildedProboscis => Owner.HeldItem;
        int projIndex = -1;
        int drawUIalp = 0;
        public override void AI() {
            if (Projectile.ai[1] == 0) {
                base.AI();
                if (Owner != null) {
                    if (Owner.itemAnimation == Owner.itemAnimationMax / 2 && Projectile.IsOwnedByLocalPlayer()) {
                        for (int i = 0; i < 3; i++) {
                            Vector2 vr = Projectile.velocity.UnitVector().RotatedBy(MathHelper.ToRadians(-10 + 10 * i)) * 25f;
                            Projectile.NewProjectile(
                                Projectile.parent(),
                                Projectile.Center,
                                vr,
                                ModContent.ProjectileType<RedLightningFeather>(),
                                Projectile.damage / 2,
                                Projectile.knockBack,
                                Main.myPlayer
                                );
                        }
                    }
                }
            }//如果是左键弹幕，执行原有的基类行为
            if (Projectile.ai[1] == 1) {
                drawUIalp += 5;
                if (drawUIalp > 255) drawUIalp = 255;//在此处控制充能UI的透明度参数

                if (Projectile.IsOwnedByLocalPlayer())//当主人按住右键时锁定弹幕的存在时间
                {
                    if (PlayerInput.Triggers.Current.MouseRight)
                        Projectile.timeLeft = 2;
                }

                Projectile.velocity = Vector2.Zero;
                if (Owner == null) {
                    Projectile.Kill();
                    return;
                }//防御性代码，任何时候都不希望后续代码访问null值，或者对无效对象进行操作
                if (gildedProboscis == null) {
                    Projectile.Kill();
                    return;
                }
                Projectile.ai[0]++;
                if (Projectile.IsOwnedByLocalPlayer())//让玩家朝向正确的方向
                    Owner.direction = Owner.Center.To(Main.MouseWorld).X > 0 ? 1 : -1;

                if (Projectile.ai[2] == 0) {
                    Projectile.Center = Owner.Center;
                    Projectile.rotation += MathHelper.ToRadians(25);//旋转长矛

                    float frontArmRotation = (MathHelper.PiOver2 - 0.31f) * -Owner.direction;
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, frontArmRotation);

                    if (Projectile.IsOwnedByLocalPlayer()) {
                        if (Projectile.ai[0] % 20 == 0)//周期性发射弹幕
                        {
                            SoundEngine.PlaySound(SoundID.Item102, Projectile.Center);
                            for (int i = 0; i < 6; i++) {
                                Vector2 vr = CWRUtils.GetRandomVevtor(0, 360, 15);
                                Projectile.NewProjectile(
                                    Owner.parent(),
                                    Owner.Center,
                                    vr,
                                    ModContent.ProjectileType<RedLightningFeather>(),
                                    Projectile.damage,
                                    0,
                                    Owner.whoAmI
                                    );
                            }
                            gildedProboscis.CWR().MeleeCharge += 500 / 3;
                        }
                        if (Projectile.ai[0] > 60)//当旋转时间超过60tike时切换下一个状态
                        {
                            gildedProboscis.CWR().MeleeCharge = 500;
                            Projectile.ai[2] = 1;
                            Projectile.ai[0] = 0;
                            Projectile.netUpdate = true;
                        }
                    }
                }
                if (Projectile.ai[2] == 1) {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Vector2 toMous = Owner.Center.To(Main.MouseWorld).UnitVector();
                        Vector2 topos = toMous * 56 + Owner.Center;
                        Projectile.Center = Vector2.Lerp(topos, Projectile.Center, 0.01f);
                        Projectile.rotation = toMous.ToRotation() + MathHelper.PiOver4;

                        gildedProboscis.CWR().MeleeCharge = 500 - (int)Projectile.ai[0];//同步主人玩家的特斯拉充能值，后续将应用于UI绘制

                        if (Projectile.ai[0] > 10 && Owner.ownedProjectileCounts[ModContent.ProjectileType<GildedProboscisKevinLightning>()] == 0) {
                            projIndex = Projectile.NewProjectile(
                                    Owner.parent(),
                                    Owner.Center,
                                    Owner.Center.To(Main.MouseWorld).UnitVector() * 15f,
                                    ModContent.ProjectileType<GildedProboscisKevinLightning>(),
                                    Projectile.damage / 3,
                                    0,
                                    Owner.whoAmI
                                    );
                            Projectile.netUpdate = true;
                        }
                        Projectile kevin = CWRUtils.GetProjectileInstance(projIndex);
                        if (kevin != null) {
                            Vector2 pos = Projectile.Center + toMous.UnitVector() * 85;
                            kevin.Center = pos;

                            if (Projectile.ai[0] > 500) {
                                kevin.Kill();
                                kevin.netUpdate = true;
                                gildedProboscis.CWR().MeleeCharge = 0;
                                Projectile.ai[2] = 0;
                                Projectile.ai[0] = 0;
                                Projectile.netUpdate = true;
                                SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, Projectile.Center);
                            }//时长够了后切换回旋转阶段
                        }
                    }//以下行为只能由主人来运行
                }//在这个状态下将发射特斯拉闪电
            }//如果是右键弹幕，执行特定行为
        }

        public override void ExtraBehavior() {
            if (Main.rand.NextBool(4)) {
                int num = Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, DustID.RedTorch, Projectile.direction * 2, 0f, 150);
                Main.dust[num].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.canGhostHeal && !Main.player[Projectile.owner].moonLeech) {
                Player obj = Main.player[Projectile.owner];
                obj.statLife++;
                obj.HealEffect(1);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture2D = CWRUtils.GetT2DValue(Texture);

            if (Projectile.ai[1] == 0) {
                return base.PreDraw(ref lightColor);
            }
            else {
                Main.EntitySpriteDraw(
                    texture2D, CWRUtils.WDEpos(Projectile.Center), null, lightColor,
                    Projectile.rotation + MathHelper.PiOver2, CWRUtils.GetOrig(texture2D),
                    Projectile.scale, SpriteEffects.None);
                return false;
            }
        }

        public override void PostDraw(Color lightColor) {
            DrawKevinChargeBar();
        }

        int barsFrame = 0;
        public void DrawKevinChargeBar() {
            if (Owner == null || Projectile.ai[1] != 1) return;
            Texture2D kevinChargeBack = CWRUtils.GetT2DValue(CWRConstant.UI + "KevinChargeBack");
            Texture2D kevinChargeBars = CWRUtils.GetT2DValue(CWRConstant.UI + "KevinChargeBars");
            Texture2D kevinChargeTop = CWRUtils.GetT2DValue(CWRConstant.UI + "KevinChargeTop");
            float slp = 3;
            int offsetwid = 4;
            Vector2 drawPos = CWRUtils.WDEpos(Owner.Center + new Vector2(kevinChargeBack.Width / -2 * slp, 135));
            float alp = drawUIalp / 255f;
            CWRUtils.ClockFrame(ref barsFrame, 5, 3);
            Rectangle backRec = new Rectangle(offsetwid, barsFrame * kevinChargeBack.Height, (int)((kevinChargeBack.Width - offsetwid * 2) * (gildedProboscis.CWR().MeleeCharge / 500f)), kevinChargeBack.Height);

            Main.EntitySpriteDraw(
                kevinChargeBack,
                drawPos,
                null,
                Color.White * alp,
                0,
                Vector2.Zero,
                slp,
                SpriteEffects.None,
                0
                );

            Main.EntitySpriteDraw(
                kevinChargeBars,
                drawPos + new Vector2(offsetwid, 0) * slp,
                backRec,
                Color.White * alp,
                0,
                Vector2.Zero,
                slp,
                SpriteEffects.None,
                0
                );

            Main.EntitySpriteDraw(
                kevinChargeTop,
                drawPos,
                null,
                Color.White * alp,
                0,
                Vector2.Zero,
                slp,
                SpriteEffects.None,
                0
                );
        }
    }
}
