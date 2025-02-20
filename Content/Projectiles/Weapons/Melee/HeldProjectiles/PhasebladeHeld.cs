using CalamityMod;
using CalamityOverhaul.Content.MeleeModify;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class PhasebladeHeld : BaseKnife
    {
        public override int TargetID => Item.type;
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 46;
            Projectile.usesLocalNPCImmunity = false;
            canDrawSlashTrail = true;
            Incandescence = true;
            drawTrailCount = 8;
            distanceToOwner = 0;
            drawTrailTopWidth = 30;
            drawTrailBtommWidth = 30;
            SwingData.baseSwingSpeed = 4;
            SwingData.starArg = 46;
            SwingData.ler1_UpSizeSengs = 0.056f;
            SwingData.minClampLength = 70;
            SwingData.maxClampLength = 80;
            Length = 60;
        }

        internal static void Set(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<PhasebladeHeld>();
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.1f, phase1SwingSpeed: 3.2f, phase2SwingSpeed: 6f, swingSound: SoundID.Item15);
            return base.PreInOwner();
        }

        public override void SwingModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (modifiers.SuperArmor || target.defense > 999
                || target.Calamity().DR >= 0.95f || target.Calamity().unbreakableDR) {
                return;
            }
            modifiers.DefenseEffectiveness *= 0f;
        }

        private void LoadGradientTex() {
            if (!Main.dedServ) {
                string path = CWRConstant.Placeholder;
                if (TargetID == ItemID.WhitePhaseblade || TargetID == ItemID.WhitePhasesaber) {
                    path = CWRConstant.ColorBar + "Phaseblade1_Bar";
                }
                else if (TargetID == ItemID.PurplePhaseblade || TargetID == ItemID.PurplePhasesaber) {
                    path = CWRConstant.ColorBar + "Phaseblade2_Bar";
                }
                else if (TargetID == ItemID.OrangePhaseblade || TargetID == ItemID.OrangePhasesaber) {
                    path = CWRConstant.ColorBar + "Phaseblade3_Bar";
                }
                else if (TargetID == ItemID.BluePhaseblade || TargetID == ItemID.BluePhasesaber) {
                    path = CWRConstant.ColorBar + "Phaseblade4_Bar";
                }
                else if (TargetID == ItemID.GreenPhaseblade || TargetID == ItemID.GreenPhasesaber) {
                    path = CWRConstant.ColorBar + "Phaseblade5_Bar";
                }
                else if (TargetID == ItemID.RedPhaseblade || TargetID == ItemID.RedPhasesaber) {
                    path = CWRConstant.ColorBar + "Phaseblade6_Bar";
                }
                else if (TargetID == ItemID.YellowPhaseblade || TargetID == ItemID.YellowPhasesaber) {
                    path = CWRConstant.ColorBar + "Phaseblade7_Bar";
                }
                SwingSystem.gradientTextures[Type] = CWRUtils.GetT2DAsset(path);
            }
        }

        private void AddLight() {
            Color color = Color.White;
            float slp = 1f;
            if (TargetID == ItemID.WhitePhaseblade || TargetID == ItemID.WhitePhasesaber) {
                color = Color.White;
            }
            else if (TargetID == ItemID.PurplePhaseblade || TargetID == ItemID.PurplePhasesaber) {
                color = Color.Purple;
            }
            else if (TargetID == ItemID.OrangePhaseblade || TargetID == ItemID.OrangePhasesaber) {
                color = Color.Orange;
            }
            else if (TargetID == ItemID.BluePhaseblade || TargetID == ItemID.BluePhasesaber) {
                color = Color.Blue;
            }
            else if (TargetID == ItemID.GreenPhaseblade || TargetID == ItemID.GreenPhasesaber) {
                color = Color.Green;
            }
            else if (TargetID == ItemID.RedPhaseblade || TargetID == ItemID.RedPhasesaber) {
                color = Color.Red;
            }
            else if (TargetID == ItemID.YellowPhaseblade || TargetID == ItemID.YellowPhasesaber) {
                color = Color.Yellow;
            }

            if (TargetID == ItemID.WhitePhasesaber
                || TargetID == ItemID.PurplePhasesaber
                || TargetID == ItemID.OrangePhasesaber
                || TargetID == ItemID.BluePhasesaber
                || TargetID == ItemID.GreenPhasesaber
                || TargetID == ItemID.RedPhasesaber
                || TargetID == ItemID.YellowPhasesaber) {
                slp = 1.5f;
            }

            Lighting.AddLight(Owner.Center, color.ToVector3() * slp);
        }

        public override void KnifeInitialize() {
            if (TargetID == ItemID.WhitePhasesaber
                || TargetID == ItemID.PurplePhasesaber
                || TargetID == ItemID.OrangePhasesaber
                || TargetID == ItemID.BluePhasesaber
                || TargetID == ItemID.GreenPhasesaber
                || TargetID == ItemID.RedPhasesaber
                || TargetID == ItemID.YellowPhasesaber) {
                Projectile.width = Projectile.height = 66;
                drawTrailCount = 16;
                distanceToOwner = -30;
                drawTrailTopWidth = 50;
                drawTrailBtommWidth = 40;
                SwingAIType = SwingAITypeEnum.UpAndDown;
                SwingData.baseSwingSpeed = 5.6f;
                if (Time == 0) {
                    LoadTrailCountData();
                }
            }

            LoadGradientTex();
        }

        public override void PostInOwner() => AddLight();
    }
}
