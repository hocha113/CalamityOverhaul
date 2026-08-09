using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    internal class LonginusHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item + "Melee/Longinus";

        private NPC markNPC;
        private float markReveal;
        private float levelFlash;
        private float sheathSpin;
        private bool fullCharge;
        private Vector2[] shaftPoints;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => false;

        private bool isProj() => Main.projectile.Count((p)
                => p.type == ModContent.ProjectileType<LonginusThrow>()
                && p.Center.To(Owner.Center).LengthSquared() < 9000) == 0;

        public override void AI() {
            if (Item.type != SpearOfLonginus.ID || !isProj()) {
                Projectile.Kill();
                return;
            }
            Projectile.velocity = Projectile.rotation.ToRotationVector2();
            fullCharge = Owner.HeldItem?.ModItem is SpearOfLonginus held && held.ChargeGrade >= SpearOfLonginus.MaxChargeGrade;
            if (Projectile.IsOwnedByLocalPlayer()) {
                StickToOwner();
                Charge();
                SpawnHolyCross();
            }
            NPC npc = Projectile.Center.FindClosestNPC(1900);
            if (npc != null) {
                npc.CWR().LonginusSign = true;
                foreach (NPC overNPC in Main.npc) {
                    if (overNPC.whoAmI != npc.whoAmI && overNPC.type != NPCID.None) {
                        overNPC.CWR().LonginusSign = false;
                    }
                }
            }
            //标记光轮显隐，换目标重新显现
            if (npc != markNPC) {
                markNPC = npc;
                markReveal = 0f;
            }
            if (markNPC != null && markNPC.active) {
                markReveal = MathHelper.Clamp(markReveal + 0.06f, 0f, 1f);
            }
            if (levelFlash > 0f) {
                levelFlash -= 0.04f;
            }
            //能量鞘缠绕滚动，充能越高越快
            float gradeProg = Owner.HeldItem?.ModItem is SpearOfLonginus sol
                ? sol.ChargeGrade / (float)SpearOfLonginus.MaxChargeGrade : 0f;
            sheathSpin += 0.14f + gradeProg * 0.12f;
            Projectile.ai[0]++;
        }

        /// 持续注入圣神能量，满条转一次立场充能(最高 <see cref="SpearOfLonginus.MaxChargeGrade"/> 层)
        /// 与盗贼潜伏解耦，仅依赖持枪时间
        public void Charge() {
            SpearOfLonginus longinus = (SpearOfLonginus)Owner.HeldItem.ModItem;

            //满层停累积
            if (longinus.ChargeGrade >= SpearOfLonginus.MaxChargeGrade) {
                longinus.HolyEnergy = SpearOfLonginus.HolyEnergyMax;
                return;
            }

            //每帧累积一次能量；玩家持枪不动时也会涨
            //充能可视化走 LonginusCharge 吸入场，不再喷星屑粒子

            longinus.HolyEnergy++;

            //能量满升层
            if (longinus.HolyEnergy >= SpearOfLonginus.HolyEnergyMax) {
                longinus.HolyEnergy = 0;
                longinus.ChargeGrade++;
                if (longinus.ChargeGrade > SpearOfLonginus.MaxChargeGrade) {
                    longinus.ChargeGrade = SpearOfLonginus.MaxChargeGrade;
                }

                SoundStyle lightningStrikeSound = "CalamityMod/Sounds/Custom/HeavenlyGaleLightningStrike".GetSound();
                lightningStrikeSound.Volume = 0.25f;
                SoundEngine.PlaySound(lightningStrikeSound, Projectile.Center);
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/HeavenlyGaleFire".GetSound(), Projectile.Center);

                //升层演出：枪前展开一层AT力场薄膜
                levelFlash = 1f;
            }
        }

        /// <summary>维持身下HolyCross，绘制能量条与立场</summary>
        private void SpawnHolyCross() {
            if (Owner.CountProjectilesOfID<HolyCross>() == 0) {
                Projectile.NewProjectile(Owner.GetSource_FromThis(), Owner.Center, Vector2.Zero
                    , ModContent.ProjectileType<HolyCross>(), 0, 0, Owner.whoAmI);
            }
        }

        public void StickToOwner() {
            Owner.heldProj = Projectile.whoAmI;
            Projectile.rotation = ToMouseA;
            Owner.direction = Math.Sign(ToMouse.X);

            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);

            Projectile.Center = Owner.GetPlayerStabilityCenter() + UnitToMouseV * 70;
            //满层微震颤
            if (fullCharge) {
                Projectile.Center += Main.rand.NextVector2Circular(1.3f, 1.3f);
            }
            Projectile.timeLeft = 2;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            //枪身双螺旋能量鞘 + 枪尖吸入场，随充能进度增强
            if (Owner.HeldItem?.ModItem is SpearOfLonginus sp) {
                float fillProg = sp.ChargeGrade >= SpearOfLonginus.MaxChargeGrade
                    ? 1f : sp.HolyEnergy / (float)SpearOfLonginus.HolyEnergyMax;
                float gradeProg = sp.ChargeGrade / (float)SpearOfLonginus.MaxChargeGrade;

                shaftPoints ??= new Vector2[14];
                Vector2 tip = Projectile.Center + Projectile.velocity * 66f;
                Vector2 tail = Projectile.Center - Projectile.velocity * 38f;
                for (int i = 0; i < shaftPoints.Length; i++) {
                    shaftPoints[i] = Vector2.Lerp(tip, tail, i / (shaftPoints.Length - 1f));
                }
                LonginusVFX.DrawHelixTrail(shaftPoints, shaftPoints.Length, 4.5f + gradeProg * 2.5f
                    , 7f + gradeProg * 3f, sheathSpin, 0f, 0.30f + gradeProg * 0.45f
                    , 0.15f + gradeProg * 0.45f, 3.1f);

                LonginusVFX.DrawChargeIntake(tip, 54f + gradeProg * 16f
                    , fullCharge ? 1f : 0.30f + fillProg * 0.70f, fullCharge ? 1f : 0f
                    , 0.85f, Projectile.whoAmI * 0.137f);
            }

            //标记目标头顶的倾斜光轮
            if (markNPC != null && markNPC.active && markReveal > 0.01f) {
                float haloR = MathHelper.Clamp(markNPC.width * 0.5f + 26f, 34f, 120f);
                Vector2 haloPos = markNPC.Top + new Vector2(0, -30f - haloR * 0.18f);
                float breathe = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.1f);
                LonginusVFX.DrawHalo(haloPos, haloR, 0.30f, markReveal, breathe * 0.5f, 0.75f);
            }
            //升层立场薄膜闪现
            if (levelFlash > 0.01f && Owner.HeldItem?.ModItem is SpearOfLonginus longinus) {
                float t = 1f - levelFlash;
                LonginusVFX.DrawATField(Projectile.Center + Projectile.velocity * 60f, Projectile.velocity
                    , 110f + longinus.ChargeGrade * 10f, MathHelper.Clamp(t * 2.6f, 0f, 1f), 0f
                    , levelFlash * 0.65f, 1, Projectile.whoAmI * 0.211f, 0.55f);
            }
            //满层枪尖小光轮
            if (fullCharge) {
                Vector2 tip = Projectile.Center + Projectile.velocity * 66f;
                float breathe = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5.3f);
                LonginusVFX.DrawHalo(tip, 24f, 0.85f, 1f, breathe, 0.8f, LonginusVFX.HolyGold);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Item[SpearOfLonginus.ID].Value;
            int dir = Owner.direction * (int)Owner.gravDir;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition + Owner.CWR().SpecialDrawPositionOffset, null, lightColor
                , Projectile.rotation + MathHelper.PiOver4 + (dir > 0 ? MathHelper.PiOver2 : 0)
                , value.Size() / 2, Projectile.scale * 0.9f, dir > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
