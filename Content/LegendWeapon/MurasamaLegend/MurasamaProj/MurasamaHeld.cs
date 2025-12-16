using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.UI;
using InnoVault.GameContent.BaseEntity;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    internal class MurasamaHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Projectile_Melee + "MurasamaHeld";
        private ref float Time => ref Projectile.ai[0];
        private ref int RisingDragon => ref Owner.CWR().RisingDragonCharged;
        private bool OnFireR => DownRight && !DownLeft;
        private int Level => MurasamaOverride.GetLevel(Item);
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
        internal int noAttenuationTime;
        private const int maxFrame = 6;
        private static int breakOutType;
        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
            breakOutType = ModContent.ProjectileType<MuraTriggerDash>();
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

            if (Item.type != MurasamaOverride.ID) {//只需要判断原版的物品
                Projectile.Kill();
                return false;
            }

            Owner.CWR().HeldMurasamaBool = true;
            if (base.Owner.ownedProjectileCounts[ModContent.ProjectileType<MuraSlashDefault>()] != 0
                || base.Owner.ownedProjectileCounts[ModContent.ProjectileType<MuraTriggerDash>()] != 0) {
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
            noHasDownSkillProj = Owner.ownedProjectileCounts[ModContent.ProjectileType<MuraGroundSmash>()] == 0;
            noHasBreakOutProj = Owner.ownedProjectileCounts[ModContent.ProjectileType<MuraTriggerDash>()] == 0;
            noHasEndSkillEffectStart = Owner.ownedProjectileCounts[ModContent.ProjectileType<EndSkillEffectStart>()] == 0;
        }

        private void UpdateRisingDragon() {
            VaultUtils.ClockFrame(ref uiFrame, 5, maxFrame - 1);
            VaultUtils.ClockFrame(ref uiFrame2, 5, 8);

            bool hasBoss = false;
            foreach (var npc in Main.npc) {
                if (!npc.active || npc.friendly) {
                    continue;
                }
                if (npc.boss) {
                    hasBoss = true;
                }
            }

            if (RisingDragon > 0) {
                if (uiAlape < 1) {
                    uiAlape += 0.1f;
                }
                if (noAttenuationTime <= 0) {
                    if (!OnFireR && RisingDragon == 1 && noHasEndSkillEffectStart && noHasBreakOutProj && noHasDownSkillProj) {
                        SoundEngine.PlaySound(CWRSound.Retracting with { Volume = 0.35f }, Projectile.Center);
                    }
                    if (!hasBoss) {
                        RisingDragon--;
                    }
                }
            }
            else {
                if (uiAlape > 0) {
                    uiAlape -= 0.1f;
                }
            }

            if (hasBoss && !DownRight && RisingDragon < MurasamaOverride.GetOnRDCD(Item)) {
                noAttenuationTime = 4;
                RisingDragon++;
            }

            if (noAttenuationTime > 0) {
                noAttenuationTime--;
            }

            if (RisingDragon > MurasamaOverride.GetOnRDCD(Item)) {
                RisingDragon = MurasamaOverride.GetOnRDCD(Item);
            }
            else if (RisingDragon < 0) {
                RisingDragon = 0;
            }

            risingDragonFullSet = RisingDragon >= MurasamaOverride.GetOnRDCD(Item);

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

        public override BitsByte SendBitsByte(BitsByte flags) {
            BitsByte bytes = base.SendBitsByte(flags);
            bytes[2] = triggerKeyDown;
            flags[3] = fodingDownKey;
            return bytes;
        }

        private void SetDirByBreakerSlash() {
            int id = ModContent.ProjectileType<MuraBreakerSlash>();
            if (Owner.ownedProjectileCounts[id] == 0) {
                return;
            }

            Projectile targetMBSProj = null;
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != id || proj.owner != Owner.whoAmI) {
                    continue;
                }
                targetMBSProj = proj;
            }
            if (targetMBSProj != null) {
                Owner.direction = Math.Sign(targetMBSProj.velocity.X);
            }
        }

        public void InOwner() {
            int safeGravDir = Math.Sign(Owner.gravDir);
            nolegendStart = true;

            //设置有升龙斩的特殊情况，这种情况可能出现朝向设置的延后冲突，所以这里实时更新一次
            SetDirByBreakerSlash();

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

            Projectile.rotation = MathHelper.ToRadians(heldRotSengs * DirSign * safeGravDir) + Owner.fullRotation;

            if (OnFireR) {//玩家按下右键触发这些技能，同时需要避免在左键按下的时候触发
                Owner.direction = Math.Sign(ToMouse.X);
                Projectile.Center += new Vector2(0, -5) * safeGravDir;
                armRotSengsFront = (ToMouseA - MathHelper.PiOver2 * safeGravDir) / CWRUtils.atoR * -DirSign;
                armRotSengsBack = 30;
                Projectile.rotation = ToMouseA + MathHelper.ToRadians(75 + (DirSign > 0 ? 20 : 0));
                if (RisingDragon < MurasamaOverride.GetOnRDCD(Item)) {
                    if (RisingDragon == MurasamaOverride.GetOnRDCD(Item) - 1) {
                        SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = 0.15f, Volume = 0.3f }, Projectile.Center);
                    }
                    RisingDragon += 3;
                    noAttenuationTime = 10;
                }
                else {
                    noAttenuationTime = 180;
                }
            }

            if (triggerKeyDown && Owner.ownedProjectileCounts[breakOutType] == 0 && noHasDownSkillProj) {//扳机键被按下，并且升龙冷却已经完成，那么将刀发射出去
                if (nolegendStart && RisingDragon >= MurasamaOverride.GetOnRDCD(Item)) {
                    SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = 0.15f, Volume = 0.3f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item38 with { Pitch = 0.1f, Volume = 0.5f }, Projectile.Center);
                    if (MurasamaOverride.NameIsVergil(Owner) && Main.rand.NextBool()) {
                        SoundStyle sound = Main.rand.NextBool() ? CWRSound.V_Kengms : CWRSound.V_Heen;
                        SoundEngine.PlaySound(sound with { Volume = 0.3f }, Projectile.Center);
                    }

                    Owner.velocity += UnitToMouseV * -3;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Projectile.NewProjectile(new EntitySource_ItemUse(Owner, Item, "MBOut"), Projectile.Center, UnitToMouseV * (7 + Level * 0.2f)
                    , breakOutType, (int)(MurasamaOverride.ActualTrueMeleeDamage(Item) * (0.35f + Level * 0.05f)), 0, Owner.whoAmI, ai2: 15);
                    }

                    RisingDragon = 0;

                    SpanTriggerEffDust();
                }
                else {
                    SoundEngine.PlaySound(CWRSound.Ejection with { MaxInstances = 3 }, Projectile.Center);
                }
            }

            bool tryGroundSmash = false;
            //专用键触发（向下键）
            if (fodingDownKey && MurasamaOverride.UnlockSkill2(Item) && noHasDownSkillProj
                && noHasBreakOutProj && nolegendStart) {
                tryGroundSmash = true;
            }

            //执行下砸
            if (tryGroundSmash) {
                Item.Initialize();
                if (Item.CWR().ai[0] >= 1) {
                    SoundEngine.PlaySound("CalamityMod/Sounds/Item/MurasamaBigSwing".GetSound() with { Pitch = -0.1f, Volume = 0.25f }, Projectile.Center);
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero
                        , ModContent.ProjectileType<MuraGroundSmash>(), (int)(MurasamaOverride.ActualTrueMeleeDamage(Item) * (2 + Level * 1f)), 0, Owner.whoAmI);
                    }

                    Item.CWR().ai[0] -= 1;//消耗一点能量
                }
            }

            if (Owner.ownedProjectileCounts[breakOutType] != 0) {
                armRotSengsBack = 110;
            }

            if (CWRServerConfig.Instance.WeaponHandheldDisplay || DownLeft || DownRight) {
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
                var dustID = Main.rand.Next(6) switch {
                    0 => 262,
                    1 or 2 => 54,
                    _ => 53,
                };
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

        public override void PostDraw(Color lightColor) => MuraChargeUI.Instance.DrawOverheadSorwdBar(Owner, RisingDragon, uiFrame, maxFrame);

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
                , value.GetOrig(), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }
}
