using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class MurasamaHeldProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Projectile_Melee + "MurasamaHeldProj";
        private Item murasama => Owner.ActiveItem();
        private ref float Time => ref Projectile.ai[0];
        private ref int risingDragon => ref Owner.CWR().RisingDragonCoolDownTime;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.timeLeft = 300;
            Projectile.hide = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override bool PreAI() {
            bool heldBool1 = murasama.type != ModContent.ItemType<CalamityMod.Items.Weapons.Melee.Murasama>();
            bool heldBool2 = murasama.type != ModContent.ItemType<MurasamaEcType>();
            if (CWRServerConfig.Instance.ForceReplaceResetContent) {//如果开启了强制替换
                if (heldBool1) {//只需要判断原版的物品
                    Projectile.Kill();
                    return false;
                }
            }
            else {//如果没有开启强制替换
                if (heldBool2) {
                    Projectile.Kill();
                    return false;
                }
            }

            if (base.Owner.ownedProjectileCounts[ModContent.ProjectileType<MurasamaRSlash>()] != 0  
                || base.Owner.ownedProjectileCounts[ModContent.ProjectileType<CalamityMod.Projectiles.Melee.MurasamaSlash>()] != 0 
                || base.Owner.ownedProjectileCounts[ModContent.ProjectileType<MurasamaBreakOut>()] != 0) {
                Projectile.hide = false;
                return true;
            }
            else {
                Projectile.hide = true;
                SetHeld();
            }

            return true;
        }

        public override void AI() {
            InOwner();
            risingDragon--;
            if (risingDragon < 0) {
                risingDragon = 0;
            }
            Time++;
        }

        private int breakOutType => ModContent.ProjectileType<MurasamaBreakOut>();

        public void InOwner() {
            bool noHasDownSkillProj = Owner.ownedProjectileCounts[ModContent.ProjectileType<MurasamaDownSkill>()] == 0;
            bool noHasBreakOutProj = Owner.ownedProjectileCounts[ModContent.ProjectileType<MurasamaBreakOut>()] == 0;
            int level = InWorldBossPhase.Instance.Level();
            Projectile.Center = Owner.Center + new Vector2(0, 5);
            Projectile.timeLeft = 2;
            Projectile.scale = 0.7f;

            float heldRotSengs = Projectile.hide ? 70 : 110;
            if (Math.Abs(Owner.velocity.X) > 0 && Owner.velocity.Y == 0) {
                heldRotSengs += MathF.Sin(Time * CWRUtils.atoR * 35);
            }
            float armRotSengsFront = Projectile.hide ? 70 : 60;
            float armRotSengsBack = Projectile.hide ? 110 : 110 + MathF.Sin(Time * CWRUtils.atoR * 45) * 50;
            Projectile.rotation = MathHelper.ToRadians(heldRotSengs * DirSign);

            if (Owner.PressKey(false) && !Owner.PressKey()) {//玩家按下右键触发这些技能，同时需要避免在左键按下的时候触发
                Owner.direction = Math.Sign(ToMouse.X);
                Projectile.Center += new Vector2(0, -5);
                armRotSengsFront = (ToMouseA - MathHelper.PiOver2) / CWRUtils.atoR * -DirSign;
                armRotSengsBack = 30;
                Projectile.rotation = ToMouseA + MathHelper.ToRadians(75 + (DirSign > 0 ? 20 : 0));

                if (Owner.ownedProjectileCounts[breakOutType] == 0) {
                    if (CWRKeySystem.Murasama_TriggerKey.JustPressed && risingDragon <= 0 && noHasDownSkillProj) {//扳机键被按下，并且升龙冷却已经完成，那么将刀发射出去
                        SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = 0.15f, Volume = 0.3f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Item38 with { Pitch = 0.1f, Volume = 0.5f }, Projectile.Center);
                        if (MurasamaEcType.NameIsVergil(Owner) && Main.rand.NextBool()) {
                            SoundStyle sound = Main.rand.NextBool() ? CWRSound.V_Kengms : CWRSound.V_Heen;
                            SoundEngine.PlaySound(sound with { Volume = 0.5f }, Projectile.Center);
                        }

                        if (Projectile.IsOwnedByLocalPlayer()) {
                            Owner.velocity += UnitToMouseV * -3;
                            Projectile.NewProjectile(Owner.parent(), Projectile.Center, UnitToMouseV * (7 + level * 0.2f)
                            , breakOutType, (int)(MurasamaEcType.ActualTrueMeleeDamage * (0.45f + level * 0.05f)), 0, Owner.whoAmI);
                        }

                        SpanTriggerEffDust();
                    }

                    if (CWRKeySystem.Murasama_TriggerKey.JustPressed && risingDragon > 0) {
                        SoundEngine.PlaySound(CWRSound.Ejection with { MaxInstances = 3 }, Projectile.Center);
                    }
                }
            }

            if (CWRKeySystem.Murasama_DownKey.JustPressed && MurasamaEcType.UnlockSkill2 && noHasDownSkillProj && noHasBreakOutProj) {//下砸技能键被按下，同时技能以及解锁，那么发射执行下砸技能的弹幕
                murasama.initialize();
                if (murasama.CWR().ai[0] >= 1) {
                    SoundEngine.PlaySound(MurasamaEcType.BigSwing with { Pitch = -0.1f }, Projectile.Center);

                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Projectile.NewProjectile(Owner.parent(), Projectile.Center, new Vector2(0, 5)
                        , ModContent.ProjectileType<MurasamaDownSkill>(), (int)(MurasamaEcType.ActualTrueMeleeDamage * (2 + level * 1f)), 0, Owner.whoAmI);

                        murasama.CWR().ai[0] -= 1;//消耗一点能量
                    }
                }
            }

            if (Owner.ownedProjectileCounts[breakOutType] != 0) {
                armRotSengsBack = 110;
            }

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, MathHelper.ToRadians(armRotSengsFront) * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, MathHelper.ToRadians(armRotSengsBack) * -DirSign);
        }

        private void SpanTriggerEffDust() {
            Vector2 dustSpanPos = Projectile.Center + UnitToMouseV * 13;
            Dust.NewDust(dustSpanPos, 16, 16, DustID.Smoke);
            for (int i = 0; i < 6; i++) {
                Vector2 vr = (UnitToMouseV.ToRotation() + MathHelper.ToRadians(Main.rand.NextFloat(-15, 15))).ToRotationVector2() * Main.rand.Next(3, 16);
                Dust.NewDust(dustSpanPos, 3, 3, DustID.Smoke, vr.X, vr.Y, 15);
                int dust2 = Dust.NewDust(dustSpanPos, 3, 3, DustID.AmberBolt, vr.X, vr.Y, 15);
                Main.dust[dust2].noGravity = true;
            }

            dustSpanPos += UnitToMouseV * Projectile.width * Projectile.scale * 0.71f;
            for (int i = 0; i < 30; i++) {
                int dustID;
                switch (Main.rand.Next(6)) {
                    case 0:
                        dustID = 262;
                        break;
                    case 1:
                    case 2:
                        dustID = 54;
                        break;
                    default:
                        dustID = 53;
                        break;
                }
                float num = Main.rand.NextFloat(3f, 13f);
                float angleRandom = 0.06f;
                Vector2 dustVel = new Vector2(num, 0f).RotatedBy((double)UnitToMouseV.ToRotation(), default);
                dustVel = dustVel.RotatedBy(0f - angleRandom);
                dustVel = dustVel.RotatedByRandom(2f * angleRandom);
                if (Main.rand.NextBool(4)) {
                    dustVel = Vector2.Lerp(dustVel, -Vector2.UnitY * dustVel.Length(), Main.rand.NextFloat(0.6f, 0.85f)) * 0.9f;
                }
                float scale = Main.rand.NextFloat(0.5f, 1.5f);
                int idx = Dust.NewDust(dustSpanPos, 1, 1, dustID, dustVel.X, dustVel.Y, 0, default, scale);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].position = dustSpanPos;
            }
        }

        public override void PostDraw(Color lightColor) {
            float scale = 0.5f * CWRServerConfig.Instance.MurasamaRisingDragonCoolDownBarSize;//综合计算UI大小
            if (!(risingDragon <= 0f)) {//这是一个通用的进度条绘制，用于判断冷却进度
                Texture2D barBG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarBack", (AssetRequestMode)2).Value;
                Texture2D barFG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarFront", (AssetRequestMode)2).Value;
                float barScale = 3f;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                float yOffset = 50f;
                Vector2 drawPos = Projectile.Center + Vector2.UnitY * scale * (barFG.Height - yOffset) - Main.screenPosition + new Vector2(0, -33) * scale * 2;
                Rectangle frameCrop = new Rectangle(0, 0, (int)(risingDragon / (float)MurasamaEcType.GetOnRDCD * barFG.Width), barFG.Height);
                Color color = CWRUtils.MultiStepColorLerp(Main.GameUpdateCount % 120 / 120f, Color.Red, Color.IndianRed, Color.Gold, Color.OrangeRed, Color.DarkRed);
                Main.spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
                Main.spriteBatch.Draw(barFG, drawPos, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture + (Projectile.hide ? "" : "2"));
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation
                , CWRUtils.GetOrig(value), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }
}
