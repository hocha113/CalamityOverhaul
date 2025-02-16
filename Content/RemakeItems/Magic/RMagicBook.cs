using CalamityMod.Items.Weapons.Magic;
using CalamityMod.NPCs.Providence;
using CalamityMod.Projectiles.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal abstract class RMagicBook<TItem> : ItemOverride where TItem : ModItem
    {
        public override bool DrawingInfo => false;
        public override bool FormulaSubstitution => true;
        public override int TargetID => ModContent.ItemType<TItem>();
        public override bool CanLoad() => CWRServerConfig.Instance.WeaponHandheldDisplay;
        public override void SetDefaults(Item item) => item.SetHeldProj(CWRMod.Instance.Find<ModProjectile>(typeof(TItem).Name + "Held").Type);
    }

    internal abstract class RMagicBook : ItemOverride
    {
        public override bool DrawingInfo => false;
        public override bool FormulaSubstitution => true;
        public override int TargetID => ItemID.None;
        public virtual string HeldProjName => "";
        public override bool CanLoad() => CWRServerConfig.Instance.WeaponHandheldDisplay;
        public override void SetDefaults(Item item) => item.SetHeldProj(CWRMod.Instance.Find<ModProjectile>(HeldProjName + "Held").Type);
    }

    internal class BurningSeaHeld : BaseMagicBook<BurningSea> { }
    internal class BurningSeaRItem : RMagicBook<BurningSea> { }

    internal class EventHorizonHeld : BaseMagicBook<EventHorizon> { }
    internal class EventHorizonRItem : RMagicBook<EventHorizon> { }

    internal class ForbiddenSunHeld : BaseMagicBook<ForbiddenSun> { }
    internal class ForbiddenSunRItem : RMagicBook<ForbiddenSun> { }

    internal class FlareBoltHeld : BaseMagicBook<FlareBolt> { }
    internal class FlareBoltRItem : RMagicBook<FlareBolt> { }

    internal class FrigidflashBoltHeld : BaseMagicBook<FrigidflashBolt> { }
    internal class FrigidflashBoltRItem : RMagicBook<FrigidflashBolt> { }

    internal class EternityHeld : BaseMagicBook<Eternity>
    {
        private NPC target;
        private List<NPC> onNPCs = [];
        public override bool CanSpanProj() {
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<EternityHex>()] > 0) {
                return false;
            }
            target = Main.MouseWorld.FindClosestNPC(1600, true, true);
            if (target == null) {
                return false;
            }
            return base.CanSpanProj();
        }
        public override void FiringShoot() {
            SoundEngine.PlaySound(Providence.HolyRaySound);
            Projectile hex = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero
                , ModContent.ProjectileType<EternityHex>(), Projectile.damage, 0f, Owner.whoAmI, target.whoAmI);
            hex.localAI[1] = Projectile.whoAmI;

            for (int i = 0; i < 5; i++) {
                float crystalAngleOffset = MathHelper.TwoPi / 5f * i;
                Projectile crystal = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<EternityCrystal>(), 0, 0f, Owner.whoAmI, target.whoAmI, crystalAngleOffset);
                crystal.frame = i % 2;
                crystal.localAI[1] = Projectile.whoAmI;
            }
            for (int i = 0; i < 10; i++) {
                float circleOffset = MathHelper.TwoPi / 10f * i;
                Projectile circleSpell = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<EternityCircle>(), 0, 0f, Owner.whoAmI, target.whoAmI, circleOffset);
                circleSpell.localAI[1] = Projectile.whoAmI;
            }
        }
    }
    internal class EternityRItem : RMagicBook<Eternity> { }

    internal class EldritchTomeHeld : BaseMagicBook<EldritchTome> { }
    internal class EldritchTomeRItem : RMagicBook<EldritchTome> { }

    internal class DeathValleyDusterHeld : BaseMagicBook<DeathValleyDuster> { }
    internal class DeathValleyDusterRItem : RMagicBook<DeathValleyDuster> { }

    internal class ClothiersWrathHeld : BaseMagicBook<ClothiersWrath> { }
    internal class ClothiersWrathRItem : RMagicBook<ClothiersWrath> { }

    internal class BiofusilladeHeld : BaseMagicBook<Biofusillade> { }
    internal class BiofusilladeRItem : RMagicBook<Biofusillade> { }

    internal class AuguroftheElementsHeld : BaseMagicBook<AuguroftheElements> { }
    internal class AuguroftheElementsRItem : RMagicBook<AuguroftheElements> { }

    internal class ApotheosisHeld : BaseMagicBook<Apotheosis> { }
    internal class ApotheosisRItem : RMagicBook<Apotheosis> { }

    internal class AbyssalTomeHeld : BaseMagicBook<AbyssalTome> { }
    internal class AbyssalTomeRItem : RMagicBook<AbyssalTome> { }

    internal class EvergladeSprayHeld : BaseMagicBook<EvergladeSpray> { }
    internal class EvergladeSprayRItem : RMagicBook<EvergladeSpray> { }

    internal class FrostBoltHeld : BaseMagicBook<FrostBolt> { }
    internal class FrostBoltRItem : RMagicBook<FrostBolt> { }

    internal class LashesofChaosHeld : BaseMagicBook<LashesofChaos> { }
    internal class LashesofChaosRItem : RMagicBook<LashesofChaos> { }

    internal class LightGodsBrillianceHeld : BaseMagicBook<LightGodsBrilliance> { }
    internal class LightGodsBrillianceRItem : RMagicBook<LightGodsBrilliance> { }

    internal class NuclearFuryHeld : BaseMagicBook<NuclearFury> { }
    internal class NuclearFuryRItem : RMagicBook<NuclearFury> { }

    internal class PoseidonHeld : BaseMagicBook<Poseidon> { }
    internal class PoseidonRItem : RMagicBook<Poseidon> { }

    internal class PrimordialAncientHeld : BaseMagicBook<PrimordialAncient> { }
    internal class PrimordialAncientRItem : RMagicBook<PrimordialAncient> { }

    internal class PrimordialEarthHeld : BaseMagicBook<PrimordialEarth> { }
    internal class PrimordialEarthRItem : RMagicBook<PrimordialEarth> { }

    internal class RecitationoftheBeastHeld : BaseMagicBook<RecitationoftheBeast> { }
    internal class RecitationoftheBeastRItem : RMagicBook<RecitationoftheBeast> { }

    internal class RelicofRuinHeld : BaseMagicBook<RelicofRuin> { }
    internal class RelicofRuinRItem : RMagicBook<RelicofRuin> { }

    internal class RougeSlashHeld : BaseMagicBook<RougeSlash> { }
    internal class RougeSlashRItem : RMagicBook<RougeSlash> { }

    internal class SeethingDischargeHeld : BaseMagicBook<SeethingDischarge> { }
    internal class SeethingDischargeRItem : RMagicBook<SeethingDischarge> { }

    internal class SerpentineHeld : BaseMagicBook<Serpentine> { }
    internal class SerpentineRItem : RMagicBook<Serpentine> { }

    internal class ShadecrystalBarrageHeld : BaseMagicBook<ShadecrystalBarrage> { }
    internal class ShadecrystalBarrageRItem : RMagicBook<ShadecrystalBarrage> { }

    internal class SlitheringEelsHeld : BaseMagicBook<SlitheringEels> { }
    internal class SlitheringEelsRItem : RMagicBook<SlitheringEels> { }

    internal class StarShowerHeld : BaseMagicBook<StarShower> { }
    internal class StarShowerRItem : RMagicBook<StarShower> { }

    internal class SubsumingVortexHeld : BaseMagicBook<SubsumingVortex>
    {
        public override void PostSetRangedProperty() {
            CanRightClick = true;
            HandFireDistanceX = -10;
        }
        public override bool CanSpanProj() {
            return base.CanSpanProj() && Owner.ownedProjectileCounts[VortexAlt.ID] == 0;
        }
        public override void FiringShootR() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , VortexAlt.ID, WeaponDamage, WeaponKnockback, Owner.whoAmI);
        }
        public override void PostDraw(Color lightColor) => VortexAlt.DoDraw();
    }
    internal class SubsumingVortexRItem : RMagicBook<SubsumingVortex> { }

    internal class TearsofHeavenHeld : BaseMagicBook<TearsofHeaven> { }
    internal class TearsofHeavenRItem : RMagicBook<TearsofHeaven> { }

    internal class TheDanceofLightHeld : BaseMagicBook<TheDanceofLight> { }
    internal class TheDanceofLightRItem : RMagicBook<TheDanceofLight> { }

    internal class TomeofFatesHeld : BaseMagicBook<TomeofFates> { }
    internal class TomeofFatesRItem : RMagicBook<TomeofFates> { }

    internal class TradewindsHeld : BaseMagicBook<Tradewinds>
    {
        public override void PostSetRangedProperty() {
            CanRightClick = true;
        }

        public override void FiringShootR() {
            AmmoTypes = ModContent.ProjectileType<Feathers>();
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity / 2
                , AmmoTypes, WeaponDamage / 2, WeaponKnockback / 2, Owner.whoAmI);
            Main.projectile[proj].ai[0] = 2;
            Main.projectile[proj].localAI[0] = 1;
            Main.projectile[proj].DamageType = DamageClass.Magic;
            Main.projectile[proj].netUpdate = true;
        }
    }
    internal class TradewindsRItem : RMagicBook<Tradewinds> { }

    internal class VeeringWindHeld : BaseMagicBook<VeeringWind>
    {
        public override void PostSetRangedProperty() {
            CanRightClick = true;
        }
        public override void FiringShootR() {
            AmmoTypes = ModContent.ProjectileType<VeeringWindFrostWave>();
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback / 3, Owner.whoAmI);
        }
    }
    internal class VeeringWindRItem : RMagicBook<VeeringWind> { }

    internal class WaywasherHeld : BaseMagicBook<Waywasher> { }
    internal class WaywasherRItem : RMagicBook<Waywasher> { }

    internal class WintersFuryHeld : BaseMagicBook<WintersFury> { }
    internal class WintersFuryRItem : RMagicBook<WintersFury> { }

    internal class WrathoftheAncientsHeld : BaseMagicBook<WrathoftheAncients> { }
    internal class WrathoftheAncientsRItem : RMagicBook<WrathoftheAncients> { }

    internal class BookofSkullsHeld() : BaseMagicBook { public override int TargetID => ItemID.BookofSkulls; }
    internal class BookofSkullsRItem : RMagicBook
    {
        public override int TargetID => ItemID.BookofSkulls;
        public override string HeldProjName => "BookofSkulls";
    }

    internal class WaterBoltHeld() : BaseMagicBook { public override int TargetID => ItemID.WaterBolt; }
    internal class WaterBoltRItem : RMagicBook
    {
        public override int TargetID => ItemID.WaterBolt;
        public override string HeldProjName => "WaterBolt";
    }

    internal class DemonScytheHeld() : BaseMagicBook { public override int TargetID => ItemID.DemonScythe; }
    internal class DemonScytheRItem : RMagicBook
    {
        public override int TargetID => ItemID.DemonScythe;
        public override string HeldProjName => "DemonScythe";
    }

    internal class RazorbladeTyphoonHeld() : BaseMagicBook { public override int TargetID => ItemID.RazorbladeTyphoon; }
    internal class RazorbladeTyphoonRItem : RMagicBook
    {
        public override int TargetID => ItemID.RazorbladeTyphoon;
        public override string HeldProjName => "RazorbladeTyphoon";
    }

    internal class LunarFlareBookHeld() : BaseMagicBook { public override int TargetID => ItemID.LunarFlareBook; }
    internal class LunarFlareBookRItem : RMagicBook
    {
        public override int TargetID => ItemID.LunarFlareBook;
        public override string HeldProjName => "LunarFlareBook";
    }

    internal class CrystalStormHeld() : BaseMagicBook { public override int TargetID => ItemID.CrystalStorm; }
    internal class CrystalStormRItem : RMagicBook
    {
        public override int TargetID => ItemID.CrystalStorm;
        public override string HeldProjName => "CrystalStorm";
    }

    internal class MagnetSphereHeld() : BaseMagicBook { public override int TargetID => ItemID.MagnetSphere; }
    internal class MagnetSphereRItem : RMagicBook
    {
        public override int TargetID => ItemID.MagnetSphere;
        public override string HeldProjName => "MagnetSphere";
    }

    internal class CursedFlamesHeld() : BaseMagicBook { public override int TargetID => ItemID.CursedFlames; }
    internal class CursedFlamesRItem : RMagicBook
    {
        public override int TargetID => ItemID.CursedFlames;
        public override string HeldProjName => "CursedFlames";
    }

    internal class GoldenShowerHeld() : BaseMagicBook { public override int TargetID => ItemID.GoldenShower; }
    internal class GoldenShowerRItem : RMagicBook
    {
        public override int TargetID => ItemID.GoldenShower;
        public override string HeldProjName => "GoldenShower";
    }
}
