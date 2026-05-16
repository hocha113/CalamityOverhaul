using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
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
    internal class LonginusHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item + "Rogue/Longinus";
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
            if (Projectile.IsOwnedByLocalPlayer()) {
                StickToOwner();
                Charge();
                SpawnHolyCross();
            }
            NPC npc = Projectile.Center.FindClosestNPC(1900);
            float slp = Projectile.ai[0];
            if (slp > 600)
                slp = 600;
            if (npc != null) {
                if (Projectile.ai[0] % 30 == 0) {
                    Vector2 vr = new Vector2(0, 13);
                    PRT_LonginusWave pulse = new PRT_LonginusWave(npc.Center + new Vector2(0, -360), vr, Color.Red, new Vector2(1.2f, 3f) * 0.6f, vr.ToRotation(), 0.32f, 0.82f + slp * 0.001f, 180, npc);
                    PRTLoader.AddParticle(pulse);
                    Vector2 vr2 = new Vector2(0, -13);
                    PRT_LonginusWave pulse2 = new PRT_LonginusWave(npc.Center + new Vector2(0, 360), vr2, Color.Red, new Vector2(1.2f, 3f) * 0.6f, vr2.ToRotation(), 0.32f, 0.82f + slp * 0.001f, 180, npc);
                    PRTLoader.AddParticle(pulse2);
                }
                npc.CWR().LonginusSign = true;
                foreach (NPC overNPC in Main.npc) {
                    if (overNPC.whoAmI != npc.whoAmI && overNPC.type != NPCID.None) {
                        overNPC.CWR().LonginusSign = false;
                    }
                }
            }
            Projectile.ai[0]++;
        }

        /// <summary>
        /// 持续注入圣神能量。能量条充满后自动转化为一次立场充能（最高 <see cref="SpearOfLonginus.MaxChargeGrade"/> 层）。
        /// 与盗贼潜伏完全解耦，仅依赖玩家持枪时间。
        /// </summary>
        public void Charge() {
            SpearOfLonginus longinus = (SpearOfLonginus)Owner.HeldItem.ModItem;

            //已经达到最大立场层数时停止累积，避免不必要的能量浪费
            if (longinus.ChargeGrade >= SpearOfLonginus.MaxChargeGrade) {
                longinus.HolyEnergy = SpearOfLonginus.HolyEnergyMax;
                return;
            }

            //每帧累积一次能量；玩家持枪不动时也会涨
            longinus.HolyEnergy++;

            //充能粒子效果（仅在视觉开关开启时绘制）
            if (CWRServerConfig.Instance.WeaponHandheldDisplay && Main.rand.NextBool(2)) {
                Vector2 spanStarPos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(33) + Projectile.velocity * 55;
                Vector2 vr = spanStarPos.To(Projectile.velocity * 198 + Projectile.Center).UnitVector() * 3;
                PRT_LonginusStar spark = new PRT_LonginusStar(spanStarPos, vr, false, Main.rand.Next(17, 25), Main.rand.NextFloat(0.9f, 1.1f), Color.Gold, Projectile);
                PRTLoader.AddParticle(spark);
            }

            //能量满了：升一层立场
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

                for (int i = 0; i < longinus.ChargeGrade; i++) {
                    PRT_LonginusWave pulse = new PRT_LonginusWave(Projectile.Center + Projectile.velocity * (-0.52f + i * 23)
                        , Projectile.velocity / 1.5f, Color.Red, new Vector2(1.5f, 3f) * (0.8f - i * 0.1f)
                        , Projectile.velocity.ToRotation(), 0.82f, 0.32f, 60, Projectile);
                    PRTLoader.AddParticle(pulse);
                }
            }
        }

        /// <summary>
        /// 确保玩家身下始终有一个圣神十字架弹幕负责绘制能量条与立场指示
        /// </summary>
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

            if (CWRServerConfig.Instance.WeaponHandheldDisplay) {
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
            }

            Projectile.Center = Owner.GetPlayerStabilityCenter() + UnitToMouseV * 70;
            Projectile.timeLeft = 2;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!CWRServerConfig.Instance.WeaponHandheldDisplay) {
                return false;
            }

            Texture2D value = TextureAssets.Item[SpearOfLonginus.ID].Value;
            int dir = Owner.direction * (int)Owner.gravDir;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition + Owner.CWR().SpecialDrawPositionOffset, null, lightColor
                , Projectile.rotation + MathHelper.PiOver4 + (dir > 0 ? MathHelper.PiOver2 : 0)
                , value.Size() / 2, Projectile.scale * 0.9f, dir > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
