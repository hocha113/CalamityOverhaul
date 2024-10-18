using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class MurasamaHeldProj : BaseHeldProj, ICWRLoader
    {
        public override string Texture => CWRConstant.Projectile_Melee + "MurasamaHeldProj";
        private Item murasama => Owner.ActiveItem();
        private ref float Time => ref Projectile.ai[0];
        private ref int risingDragon => ref Owner.CWR().RisingDragonCharged;
        private bool onFireR => DownRight && !DownLeft;
        private int level => InWorldBossPhase.Instance.Mura_Level();
        private bool initialize;
        private bool oldRisingDragonFullSet;
        private bool risingDragonFullSet;
        private bool noHasDownSkillProj;
        private bool noHasBreakOutProj;
        private bool noHasEndSkillEffectStart;
        private bool nolegendStart = true;
        private bool old_TriggerKeyDown;
        private bool triggerKeyDown;
        private bool old_FodingDownKey;
        private bool fodingDownKey;
        internal int uiFrame;
        internal int uiFrame2;
        private float uiAlape;
        private int maxFrame = 6;
        internal int noAttenuationTime;
        private static int breakOutType;
        private static Asset<Texture2D> MuraBarBottom;
        private static Asset<Texture2D> MuraBarTop;
        private static Asset<Texture2D> MuraBarFull;
        void ICWRLoader.SetupData() => breakOutType = ModContent.ProjectileType<MurasamaBreakOut>();
        void ICWRLoader.LoadAsset() {
            MuraBarBottom = CWRUtils.GetT2DAsset(CWRConstant.UI + "MuraBarBottom");
            MuraBarTop = CWRUtils.GetT2DAsset(CWRConstant.UI + "MuraBarTop");
            MuraBarFull = CWRUtils.GetT2DAsset(CWRConstant.UI + "MuraBarFull");
        }
        void ICWRLoader.UnLoadData() {
            breakOutType = 0;
            MuraBarBottom = null;
            MuraBarTop = null;
            MuraBarFull = null;
        }

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

        public override bool PreUpdate() {
            if (!initialize) {//每个玩家都拥有自己的实例映射，不要互相干扰
                if (Projectile.IsOwnedByLocalPlayer()) {
                    ((MuraChargeUI)UIHandleLoader.GetUIHandleInstance<MuraChargeUI>()).murasamaHeld = this;
                }
                initialize = true;
            }

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
            Owner.CWR().HeldMurasamaBool = true;
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

        private void CheakNoHasProj() {
            noHasDownSkillProj = Owner.ownedProjectileCounts[ModContent.ProjectileType<MurasamaDownSkill>()] == 0;
            noHasBreakOutProj = Owner.ownedProjectileCounts[ModContent.ProjectileType<MurasamaBreakOut>()] == 0;
            noHasEndSkillEffectStart = Owner.ownedProjectileCounts[ModContent.ProjectileType<EndSkillEffectStart>()] == 0;
        }

        private void UpdateRisingDragon() {
            CWRUtils.ClockFrame(ref uiFrame, 5, maxFrame - 1);
            CWRUtils.ClockFrame(ref uiFrame2, 5, 8);

            bool hasBoss = false;
            foreach (var npc in Main.npc) {
                if (!npc.active || npc.friendly) {
                    continue;
                }
                if (npc.boss) {
                    hasBoss = true;
                }
            }

            if (risingDragon > 0) {
                if (uiAlape < 1) {
                    uiAlape += 0.1f;
                }
                if (noAttenuationTime <= 0) {
                    if (!onFireR && risingDragon == 1 && noHasEndSkillEffectStart && noHasBreakOutProj && noHasDownSkillProj) {
                        SoundEngine.PlaySound(CWRSound.Retracting with { Volume = 0.35f }, Projectile.Center);
                    }
                    if (!hasBoss) {
                        risingDragon--;
                    }
                }
            }
            else {
                if (uiAlape > 0) {
                    uiAlape -= 0.1f;
                }
            }

            if (hasBoss && !DownRight && risingDragon < MurasamaEcType.GetOnRDCD) {
                noAttenuationTime = 4;
                risingDragon++;
            }

            if (noAttenuationTime > 0) {
                noAttenuationTime--;
            }

            if (risingDragon > MurasamaEcType.GetOnRDCD) {
                risingDragon = MurasamaEcType.GetOnRDCD;
            }
            else if (risingDragon < 0) {
                risingDragon = 0;
            }

            risingDragonFullSet = risingDragon >= MurasamaEcType.GetOnRDCD;

            if (risingDragonFullSet && !oldRisingDragonFullSet) {
                SoundEngine.PlaySound(CWRSound.Retracting with { Volume = 0.35f, Pitch = 0.3f }, Projectile.Center);
            }

            oldRisingDragonFullSet = risingDragonFullSet;
        }

        private void NetKeyingWork() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                triggerKeyDown = CWRKeySystem.Murasama_TriggerKey.JustPressed;
                fodingDownKey = CWRKeySystem.Murasama_DownKey.JustPressed;
                if (fodingDownKey != old_FodingDownKey || triggerKeyDown != old_TriggerKeyDown) {
                    NetUpdate();
                }
                old_TriggerKeyDown = triggerKeyDown;
                old_FodingDownKey = fodingDownKey;
            }
        }

        public override void AI() {
            CheakNoHasProj();
            NetKeyingWork();
            InOwner();
            UpdateRisingDragon();
            Time++;
        }

        public override void ReceiveBitsByte(BitsByte flags) {
            base.ReceiveBitsByte(flags);
            triggerKeyDown = flags[2];
            fodingDownKey = flags[3];
        }

        public override BitsByte SandBitsByte(BitsByte flags) {
            BitsByte bytes = base.SandBitsByte(flags);
            bytes[2] = triggerKeyDown;
            flags[3] = fodingDownKey;
            return bytes;
        }

        public void InOwner() {
            int safeGravDir = Math.Sign(Owner.gravDir);
            nolegendStart = true;
            if (!CWRServerConfig.Instance.WeaponEnhancementSystem) {
                nolegendStart = InWorldBossPhase.Instance.level11;
            }

            Projectile.Center = Owner.GetPlayerStabilityCenter() + new Vector2(0, 5) * safeGravDir;
            Projectile.timeLeft = 2;
            Projectile.scale = 0.7f;

            float heldRotSengs = Projectile.hide ? 70 : 110;
            if (safeGravDir == -1) {
                heldRotSengs = Projectile.hide ? 110 : 110;
            }
            if (Math.Abs(Owner.velocity.X) > 0 && Owner.velocity.Y == 0) {
                heldRotSengs += Owner.CWR().SpecialDrawPositionOffset.Y >= 0 ? -1 : 2;
            }
            float armRotSengsFront = Projectile.hide ? 70 : 60;
            if (safeGravDir == -1) {
                armRotSengsFront = Projectile.hide ? 60 : 70;
            }
            float armRotSengsBack = Projectile.hide ? 110 : 110 + MathF.Sin(Time * CWRUtils.atoR * 45) * 50;

            Projectile.rotation = MathHelper.ToRadians(heldRotSengs * DirSign * safeGravDir);

            if (onFireR) {//玩家按下右键触发这些技能，同时需要避免在左键按下的时候触发
                Owner.direction = Math.Sign(ToMouse.X);
                Projectile.Center += new Vector2(0, -5) * safeGravDir;
                armRotSengsFront = (ToMouseA - MathHelper.PiOver2 * safeGravDir) / CWRUtils.atoR * -DirSign;
                armRotSengsBack = 30;
                Projectile.rotation = ToMouseA + MathHelper.ToRadians(75 + (DirSign > 0 ? 20 : 0));
                if (risingDragon < MurasamaEcType.GetOnRDCD) {
                    if (risingDragon == MurasamaEcType.GetOnRDCD - 1) {
                        SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = 0.15f, Volume = 0.3f }, Projectile.Center);
                    }
                    risingDragon += 3;
                    noAttenuationTime = 10;
                }
                else {
                    noAttenuationTime = 180;
                }
            }

            if (triggerKeyDown && Owner.ownedProjectileCounts[breakOutType] == 0 && noHasDownSkillProj) {//扳机键被按下，并且升龙冷却已经完成，那么将刀发射出去
                if (nolegendStart && risingDragon >= MurasamaEcType.GetOnRDCD) {
                    SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = 0.15f, Volume = 0.3f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item38 with { Pitch = 0.1f, Volume = 0.5f }, Projectile.Center);
                    if (MurasamaEcType.NameIsVergil(Owner) && Main.rand.NextBool()) {
                        SoundStyle sound = Main.rand.NextBool() ? CWRSound.V_Kengms : CWRSound.V_Heen;
                        SoundEngine.PlaySound(sound with { Volume = 0.3f }, Projectile.Center);
                    }

                    Owner.velocity += UnitToMouseV * -3;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Projectile.NewProjectile(new EntitySource_ItemUse(Owner, murasama, "MBOut"), Projectile.Center, UnitToMouseV * (7 + level * 0.2f)
                    , breakOutType, (int)(MurasamaEcType.ActualTrueMeleeDamage * (0.35f + level * 0.05f)), 0, Owner.whoAmI, ai2: 15);
                    }

                    risingDragon = 0;

                    SpanTriggerEffDust();
                }
                else {
                    SoundEngine.PlaySound(CWRSound.Ejection with { MaxInstances = 3 }, Projectile.Center);
                }
            }
            if (fodingDownKey && MurasamaEcType.UnlockSkill2 && noHasDownSkillProj
                && noHasBreakOutProj && nolegendStart) {//下砸技能键被按下，同时技能以及解锁，那么发射执行下砸技能的弹幕
                murasama.initialize();
                if (murasama.CWR().ai[0] >= 1) {
                    SoundEngine.PlaySound(MurasamaEcType.BigSwing with { Pitch = -0.1f }, Projectile.Center);
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(0, 5)
                        , ModContent.ProjectileType<MurasamaDownSkill>(), (int)(MurasamaEcType.ActualTrueMeleeDamage * (2 + level * 1f)), 0, Owner.whoAmI);
                    }

                    murasama.CWR().ai[0] -= 1;//消耗一点能量
                }
            }

            if (Owner.ownedProjectileCounts[breakOutType] != 0) {
                armRotSengsBack = 110;
            }

            if (CWRServerConfig.Instance.WeaponHandheldDisplay || (DownLeft || DownRight)) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, MathHelper.ToRadians(armRotSengsFront) * -DirSign * safeGravDir);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, MathHelper.ToRadians(armRotSengsBack) * -DirSign * safeGravDir);
            }
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
            float scale = 1;
            if (!(risingDragon <= 0f) || uiAlape > 0) {//这是一个通用的进度条绘制，用于判断进度
                Texture2D barBG = MuraBarBottom.Value;
                Texture2D barFG = MuraBarTop.Value;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                Vector2 drawPos = Owner.GetPlayerStabilityCenter() + new Vector2(0, -90) - Main.screenPosition;
                Color color = Color.White * uiAlape;
                if (risingDragon < MurasamaEcType.GetOnRDCD) {
                    Rectangle frameCrop = new Rectangle(0, 0, (int)(risingDragon / (float)MurasamaEcType.GetOnRDCD * barFG.Width), barFG.Height);
                    Main.spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale, 0, 0f);
                    Main.spriteBatch.Draw(barFG, drawPos + new Vector2(12, 42), frameCrop, color * 0.8f, 0f, barOrigin, scale, 0, 0f);
                }
                else {
                    barBG = MuraBarFull.Value;
                    Rectangle rectangle = CWRUtils.GetRec(barBG, uiFrame, maxFrame);
                    Main.spriteBatch.Draw(barBG, drawPos
                        , rectangle, color, 0f, rectangle.Size() / 2, scale, 0, 0f);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!CWRServerConfig.Instance.WeaponHandheldDisplay && !(DownLeft || DownRight)) {
                return false;
            }
            if (!noHasDownSkillProj) {
                return false;
            }
            Texture2D value = CWRUtils.GetT2DValue(Texture + (Projectile.hide ? "" : "2"));
            Vector2 drawPos = Projectile.Center - Main.screenPosition + Owner.CWR().SpecialDrawPositionOffset;
            Main.EntitySpriteDraw(value, drawPos, null, lightColor, Projectile.rotation
                , CWRUtils.GetOrig(value), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }
}
