using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal abstract class RMagicStaff<TItem> : ItemOverride where TItem : ModItem
    {
        public override bool DrawingInfo => false;
        public override bool FormulaSubstitution => true;
        public override bool CanLoadLocalization => false;
        public override int TargetID => ModContent.ItemType<TItem>();
        public override void SetDefaults(Item item) {
            if (RMagicStaff.CanLoadFunc(this)) {
                item.SetHeldProj(CWRMod.Instance.Find<ModProjectile>(typeof(TItem).Name + "Held").Type);
            }
        }
    }

    internal abstract class RMagicStaff : ItemOverride
    {
        public override bool DrawingInfo => false;
        public override bool FormulaSubstitution => true;
        public override bool CanLoadLocalization => false;
        public override int TargetID => ItemID.None;
        public virtual string HeldProjName => "";
        public static bool CanLoadFunc(ItemOverride itemOverride) {
            if (itemOverride.DrawingInfo) {
                return true;
            }
            if (!CWRServerConfig.Instance.WeaponHandheldDisplay) {
                return false;//对于不重要的修改，手持选项便可以将其覆盖
            }
            if (itemOverride.TargetID >= ItemID.Count) {
                return true;//模组物品不受其他模组的影响
            }
            return !ModLoader.HasMod("DDmod");
        }
        public override void SetDefaults(Item item) {
            if (CanLoadFunc(this)) {
                item.SetHeldProj(CWRMod.Instance.Find<ModProjectile>(HeldProjName + "Held").Type);
            }
        }
    }

    //internal class ArtAttackHeld : BaseMagicStaff<ArtAttack> { }
    //internal class ArtAttackRItem : RMagicStaff<ArtAttack> { }

    internal class AsteroidStaffHeld : BaseMagicStaff<AsteroidStaff> { }
    internal class AsteroidStaffRItem : RMagicStaff<AsteroidStaff> { }

    internal class AstralachneaStaffHeld : BaseMagicStaff<AstralachneaStaff> { }
    internal class AstralachneaStaffRItem : RMagicStaff<AstralachneaStaff> { }

    internal class AstralStaffHeld : BaseMagicStaff<AstralStaff> { }
    internal class AstralStaffRItem : RMagicStaff<AstralStaff> { }

    internal class AtlantisHeld : BaseMagicStaff<Atlantis> { }
    internal class AtlantisRItem : RMagicStaff<Atlantis> { }

    internal class BloodBathHeld : BaseMagicStaff<BloodBath> { }
    internal class BloodBathRItem : RMagicStaff<BloodBath> { }

    internal class BrimroseStaffHeld : BaseMagicStaff<BrimroseStaff> { }
    internal class BrimroseStaffRItem : RMagicStaff<BrimroseStaff> { }
    //这个东西暂时没有被制作进游戏
    //internal class CarnageRayHeld : BaseMagicStaff<CarnageRay> { }
    //internal class CarnageRayRItem : RMagicStaff<CarnageRay> { }

    internal class ClamorNoctusHeld : BaseMagicStaff<ClamorNoctus> { }
    internal class ClamorNoctusRItem : RMagicStaff<ClamorNoctus> { }

    internal class DeathhailStaffHeld : BaseMagicStaff<DeathhailStaff> { }
    internal class DeathhailStaffRItem : RMagicStaff<DeathhailStaff> { }

    internal class DivineRetributionHeld : BaseMagicStaff<DivineRetribution> { }
    internal class DivineRetributionRItem : RMagicStaff<DivineRetribution> { }

    internal class DownpourHeld : BaseMagicStaff<Downpour> { }
    internal class DownpourRItem : RMagicStaff<Downpour> { }

    internal class EidolonStaffHeld : BaseMagicStaff<EidolonStaff> { }
    internal class EidolonStaffRItem : RMagicStaff<EidolonStaff> { }

    internal class ElementalRayHeld : BaseMagicStaff<ElementalRay>, ICWRLoader
    {
        private static int[] projectileTypes;
        void ICWRLoader.SetupData() {
            projectileTypes = [
                ModContent.ProjectileType<SolarElementalBeam>(),
                ModContent.ProjectileType<NebulaElementalBeam>(),
                ModContent.ProjectileType<VortexElementalBeam>(),
                ModContent.ProjectileType<StardustElementalBeam>()
            ];
        }
        void ICWRLoader.UnLoadData() => projectileTypes = null;
        public override void PostSetRangedProperty() => ShootPosToMouLengValue = 60;
        public override void FiringShoot() {
            float offsetAngle = MathHelper.TwoPi * useAnimation / Item.useAnimation + Main.rand.NextFloat(0f, 1.3f);
            float shootSpeed = 1f;
            int index = (Item.useAnimation - useAnimation) / Item.useTime;
            index = Math.Clamp(index, 0, projectileTypes.Length - 1);

            AmmoTypes = projectileTypes[index];

            if (AmmoTypes == ModContent.ProjectileType<NebulaElementalBeam>()) {
                offsetAngle -= NebulaElementalBeam.UniversalAngularSpeed * 0.5f;
            }
            else if (AmmoTypes == ModContent.ProjectileType<VortexElementalBeam>()) {
                shootSpeed = 2f;
            }

            Vector2 spawnOffset = Owner.Center.To(InMousePos).UnitVector().RotatedBy(offsetAngle) * -Main.rand.NextFloat(40f, 96f);
            Vector2 shootDirection = (InMousePos - (ShootPos + spawnOffset)).SafeNormalize(Vector2.UnitX * Owner.direction);

            int beam = Projectile.NewProjectile(Source, ShootPos + spawnOffset, shootDirection * shootSpeed, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI);
            if (AmmoTypes == ModContent.ProjectileType<VortexElementalBeam>()) {
                Main.projectile[beam].ai[0] = shootDirection.ToRotation();
                Main.projectile[beam].ai[1] = Main.rand.Next(100);
            }
        }
    }
    internal class ElementalRayRItem : RMagicStaff<ElementalRay> { }

    internal class FabstaffHeld : BaseMagicStaff<Fabstaff> { }
    internal class FabstaffRItem : RMagicStaff<Fabstaff> { }

    internal class GleamingMagnoliaHeld : BaseMagicStaff<GleamingMagnolia> { }
    internal class GleamingMagnoliaRItem : RMagicStaff<GleamingMagnolia> { }

    internal class HeliumFlashHeld : BaseMagicStaff<HeliumFlash> { }
    internal class HeliumFlashRItem : RMagicStaff<HeliumFlash> { }

    internal class HellwingStaffHeld : BaseMagicStaff<HellwingStaff> { }
    internal class HellwingStaffRItem : RMagicStaff<HellwingStaff> { }

    internal class HematemesisHeld : BaseMagicStaff<Hematemesis> { }
    internal class HematemesisRItem : RMagicStaff<Hematemesis> { }

    internal class HyphaeRodHeld : BaseMagicStaff<HyphaeRod> { }
    internal class HyphaeRodRItem : RMagicStaff<HyphaeRod> { }

    //internal class IceBarrageHeld : BaseMagicStaff<IceBarrage> { }
    //internal class IceBarrageRItem : RMagicStaff<IceBarrage> { }

    internal class IcicleStaffHeld : BaseMagicStaff<IcicleStaff> { }
    internal class IcicleStaffRItem : RMagicStaff<IcicleStaff> { }

    internal class IcicleTridentHeld : BaseMagicStaff<IcicleTrident> { }
    internal class IcicleTridentRItem : RMagicStaff<IcicleTrident> { }

    //internal class InfernalRiftHeld : BaseMagicStaff<InfernalRift> { }
    //internal class InfernalRiftRItem : RMagicStaff<InfernalRift> { }

    internal class KeelhaulHeld : BaseMagicStaff<Keelhaul> { }
    internal class KeelhaulRItem : RMagicStaff<Keelhaul> { }

    internal class ManaRoseHeld : BaseMagicStaff<ManaRose> { }
    internal class ManaRoseRItem : RMagicStaff<ManaRose> { }

    internal class MiasmaHeld : BaseMagicStaff<Miasma> { }
    internal class MiasmaRItem : RMagicStaff<Miasma> { }

    internal class MistlestormHeld : BaseMagicStaff<Mistlestorm> { }
    internal class MistlestormRItem : RMagicStaff<Mistlestorm> { }

    internal class NightsRayHeld : BaseMagicStaff<NightsRay> { }
    internal class NightsRayRItem : RMagicStaff<NightsRay> { }

    internal class ParasiticSceptorHeld : BaseMagicStaff<ParasiticSceptor> { }
    internal class ParasiticSceptorRItem : RMagicStaff<ParasiticSceptor> { }

    internal class PhantasmalFuryHeld : BaseMagicStaff<PhantasmalFury> { }
    internal class PhantasmalFuryRItem : RMagicStaff<PhantasmalFury> { }

    internal class PhotosynthesisHeld : BaseMagicStaff<Photosynthesis> { }
    internal class PhotosynthesisRItem : RMagicStaff<Photosynthesis> { }

    internal class PlagueStaffHeld : BaseMagicStaff<PlagueStaff> { }
    internal class PlagueStaffRItem : RMagicStaff<PlagueStaff> { }

    internal class PlasmaRodHeld : BaseMagicStaff<PlasmaRod> { }
    internal class PlasmaRodRItem : RMagicStaff<PlasmaRod> { }

    internal class SandstreamScepterHeld : BaseMagicStaff<SandstreamScepter> { }
    internal class SandstreamScepterRItem : RMagicStaff<SandstreamScepter> { }

    internal class SanguineFlareHeld : BaseMagicStaff<SanguineFlare> { }
    internal class SanguineFlareRItem : RMagicStaff<SanguineFlare> { }

    internal class ShaderainStaffHeld : BaseMagicStaff<ShaderainStaff> { }
    internal class ShaderainStaffRItem : RMagicStaff<ShaderainStaff> { }

    internal class ShadowboltStaffHeld : BaseMagicStaff<ShadowboltStaff> { }
    internal class ShadowboltStaffRItem : RMagicStaff<ShadowboltStaff> { }

    //internal class ShiftingSandsHeld : BaseMagicStaff<ShiftingSands> { }
    //internal class ShiftingSandsRItem : RMagicStaff<ShiftingSands> { }

    internal class SkyGlazeHeld : BaseMagicStaff<SkyGlaze> { }
    internal class SkyGlazeRItem : RMagicStaff<SkyGlaze> { }

    //internal class SnowstormStaffHeld : BaseMagicStaff<SnowstormStaff> { }
    //internal class SnowstormStaffRItem : RMagicStaff<SnowstormStaff> { }

    internal class SoulPiercerHeld : BaseMagicStaff<SoulPiercer> { }
    internal class SoulPiercerRItem : RMagicStaff<SoulPiercer> { }

    //internal class StaffofBlushieHeld : BaseMagicStaff<StaffofBlushie> { }
    //internal class StaffofBlushieRItem : RMagicStaff<StaffofBlushie> { }

    internal class TacticiansTrumpCardHeld : BaseMagicStaff<TacticiansTrumpCard> { }
    internal class TacticiansTrumpCardRItem : RMagicStaff<TacticiansTrumpCard> { }

    internal class TeslastaffHeld : BaseMagicStaff<Teslastaff> { }
    internal class TeslastaffRItem : RMagicStaff<Teslastaff> { }

    internal class TheWandHeld : BaseMagicStaff<TheWand> { }
    internal class TheWandRItem : RMagicStaff<TheWand> { }

    internal class ThornBlossomHeld : BaseMagicStaff<ThornBlossom> { }
    internal class ThornBlossomRItem : RMagicStaff<ThornBlossom> { }

    internal class UltraLiquidatorHeld : BaseMagicStaff<UltraLiquidator> { }
    internal class UltraLiquidatorRItem : RMagicStaff<UltraLiquidator> { }

    internal class UndinesRetributionHeld : BaseMagicStaff<UndinesRetribution> { }
    internal class UndinesRetributionRItem : RMagicStaff<UndinesRetribution> { }

    internal class ValkyrieRayHeld : BaseMagicStaff<ValkyrieRay> { }
    internal class ValkyrieRayRItem : RMagicStaff<ValkyrieRay> { }

    //internal class VehemenceHeld : BaseMagicStaff<Vehemence> { }
    //internal class VehemenceRItem : RMagicStaff<Vehemence> { }

    internal class VenusianTridentHeld : BaseMagicStaff<VenusianTrident> { }
    internal class VenusianTridentRItem : RMagicStaff<VenusianTrident> { }

    internal class VisceraHeld : BaseMagicStaff<Viscera> { }
    internal class VisceraRItem : RMagicStaff<Viscera> { }

    internal class VitriolicViperHeld : BaseMagicStaff<VitriolicViper> { }
    internal class VitriolicViperRItem : RMagicStaff<VitriolicViper> { }

    internal class WyvernsCallHeld : BaseMagicStaff<WyvernsCall> { }
    internal class WyvernsCallRItem : RMagicStaff<WyvernsCall> { }

    internal class FrostStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.FrostStaff; }
    internal class FrostStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.FrostStaff;
        public override string HeldProjName => "FrostStaff";
    }

    internal class AmethystHeld() : BaseMagicStaff { public override int TargetID => ItemID.Amethyst; }
    internal class AmethystRItem : RMagicStaff
    {
        public override int TargetID => ItemID.Amethyst;
        public override string HeldProjName => "Amethyst";
    }

    internal class TopazStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.TopazStaff; }
    internal class TopazStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.TopazStaff;
        public override string HeldProjName => "TopazStaff";
    }

    internal class SapphireStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.SapphireStaff; }
    internal class SapphireStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.SapphireStaff;
        public override string HeldProjName => "SapphireStaff";
    }

    internal class EmeraldStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.EmeraldStaff; }
    internal class EmeraldStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.EmeraldStaff;
        public override string HeldProjName => "EmeraldStaff";
    }

    internal class RubyStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.RubyStaff; }
    internal class RubyStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.RubyStaff;
        public override string HeldProjName => "RubyStaff";
    }

    internal class DiamondStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.DiamondStaff; }
    internal class DiamondStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.DiamondStaff;
        public override string HeldProjName => "DiamondStaff";
    }

    internal class PoisonStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.PoisonStaff; }
    internal class PoisonStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.PoisonStaff;
        public override string HeldProjName => "PoisonStaff";
    }

    internal class ShadowbeamStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.ShadowbeamStaff; }
    internal class ShadowbeamStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.ShadowbeamStaff;
        public override string HeldProjName => "ShadowbeamStaff";
    }

    internal class SpectreStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.SpectreStaff; }
    internal class SpectreStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.SpectreStaff;
        public override string HeldProjName => "SpectreStaff";
    }

    internal class BlizzardStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.BlizzardStaff; }
    internal class BlizzardStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.BlizzardStaff;
        public override string HeldProjName => "BlizzardStaff";
    }

    internal class VenomStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.VenomStaff; }
    internal class VenomStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.VenomStaff;
        public override string HeldProjName => "VenomStaff";
    }

    internal class MeteorStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.MeteorStaff; }
    internal class MeteorStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.MeteorStaff;
        public override string HeldProjName => "MeteorStaff";
    }

    internal class ClingerStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.ClingerStaff; }
    internal class ClingerStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.ClingerStaff;
        public override string HeldProjName => "ClingerStaff";
    }

    internal class AmberStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.AmberStaff; }
    internal class AmberStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.AmberStaff;
        public override string HeldProjName => "AmberStaff";
    }

    internal class ApprenticeStaffT3Held() : BaseMagicStaff { public override int TargetID => ItemID.ApprenticeStaffT3; }
    internal class ApprenticeStaffT3RItem : RMagicStaff
    {
        public override int TargetID => ItemID.ApprenticeStaffT3;
        public override string HeldProjName => "ApprenticeStaffT3";
    }

    internal class RazorpineHeld() : BaseMagicStaff { public override int TargetID => ItemID.Razorpine; }
    internal class RazorpineRItem : RMagicStaff
    {
        public override int TargetID => ItemID.Razorpine;
        public override string HeldProjName => "Razorpine";
    }

    internal class StaffofEarthHeld() : BaseMagicStaff { public override int TargetID => ItemID.StaffofEarth; }
    internal class StaffofEarthRItem : RMagicStaff
    {
        public override int TargetID => ItemID.StaffofEarth;
        public override string HeldProjName => "StaffofEarth";
    }

    internal class SoulDrainHeld() : BaseMagicStaff { public override int TargetID => ItemID.SoulDrain; }
    internal class SoulDrainRItem : RMagicStaff
    {
        public override int TargetID => ItemID.SoulDrain;
        public override string HeldProjName => "SoulDrain";
    }

    internal class CrystalSerpentHeld() : BaseMagicStaff { public override int TargetID => ItemID.CrystalSerpent; }
    internal class CrystalSerpentRItem : RMagicStaff
    {
        public override int TargetID => ItemID.CrystalSerpent;
        public override string HeldProjName => "CrystalSerpent";
    }

    //internal class AquaScepterHeld() : BaseMagicStaff { public override int targetCayItem => ItemID.AquaScepter; }
    //internal class AquaScepterRItem : RMagicStaff
    //{
    //    public override int TargetID => ItemID.AquaScepter;
    //    public override string HeldProjName => "AquaScepter";
    //}

    internal class NettleBurstHeld() : BaseMagicStaff { public override int TargetID => ItemID.NettleBurst; }
    internal class NettleBurstRItem : RMagicStaff
    {
        public override int TargetID => ItemID.NettleBurst;
        public override string HeldProjName => "NettleBurst";
    }

    internal class InfernoForkHeld() : BaseMagicStaff { public override int TargetID => ItemID.InfernoFork; }
    internal class InfernoForkRItem : RMagicStaff
    {
        public override int TargetID => ItemID.InfernoFork;
        public override string HeldProjName => "InfernoFork";
    }

    internal class AmethystStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.AmethystStaff; }
    internal class AmethystStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.AmethystStaff;
        public override string HeldProjName => "AmethystStaff";
    }

    internal class BatScepterHeld() : BaseMagicStaff { public override int TargetID => ItemID.BatScepter; }
    internal class BatScepterRItem : RMagicStaff
    {
        public override int TargetID => ItemID.BatScepter;
        public override string HeldProjName => "BatScepter";
    }

    internal class UnholyTridentHeld() : BaseMagicStaff { public override int TargetID => ItemID.UnholyTrident; }
    internal class UnholyTridentRItem : RMagicStaff
    {
        public override int TargetID => ItemID.UnholyTrident;
        public override string HeldProjName => "UnholyTrident";
    }

    internal class ThunderStaffHeld() : BaseMagicStaff { public override int TargetID => ItemID.ThunderStaff; }
    internal class ThunderStaffRItem : RMagicStaff
    {
        public override int TargetID => ItemID.ThunderStaff;
        public override string HeldProjName => "ThunderStaff";
    }

    internal class VilethornHeld() : BaseMagicStaff { public override int TargetID => ItemID.Vilethorn; }
    internal class VilethornRItem : RMagicStaff
    {
        public override int TargetID => ItemID.Vilethorn;
        public override string HeldProjName => "Vilethorn";
    }

    internal class CrystalVileShardHeld() : BaseMagicStaff { public override int TargetID => ItemID.CrystalVileShard; }
    internal class CrystalVileShardRItem : RMagicStaff
    {
        public override int TargetID => ItemID.CrystalVileShard;
        public override string HeldProjName => "CrystalVileShard";
    }
}
