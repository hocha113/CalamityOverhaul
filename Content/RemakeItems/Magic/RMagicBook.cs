using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal abstract class RMagicBook<TItem> : BaseRItem where TItem : ModItem
    {
        public override bool DrawingInfo => false;
        public override bool FormulaSubstitution => true;
        public override int TargetID => ModContent.ItemType<TItem>();
        public override void SetDefaults(Item item) => item.SetHeldProj(CWRMod.Instance.Find<ModProjectile>(typeof(TItem).Name + "Held").Type);
    }

    internal abstract class RMagicBook : BaseRItem
    {
        public override bool DrawingInfo => false;
        public override bool FormulaSubstitution => true;
        public override int TargetID => ItemID.None;
        public virtual string HeldProjName => "";
        public override void SetDefaults(Item item) => item.SetHeldProj(CWRMod.Instance.Find<ModProjectile>(HeldProjName + "Held").Type);
    }

    internal abstract class BaseMagicBook<TItem> : BaseMagicBookAction where TItem : ModItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + typeof(TItem).Name;
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<TItem>();
        public override int targetCayItem => ModContent.ItemType<TItem>();
        public override int targetCWRItem => CWRServerConfig.Instance.WeaponOverhaul
            ? ItemID.None : CWRMod.Instance.Find<ModItem>(typeof(TItem).Name + "EcType").Type;
    }

    internal abstract class BaseMagicBook : BaseMagicBookAction
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override Texture2D TextureValue => TextureAssets.Item[targetCayItem].Value;
        public override LocalizedText DisplayName => Language.GetOrRegister("Mods.CalamityOverhaul.Projectiles.MagicBook_" + targetCayItem);
        public override int targetCayItem => ItemID.None;
        public override int targetCWRItem => targetCayItem;
    }

    internal abstract class BaseMagicBookAction : BaseMagicGun
    {
        private int useAnimation;
        public sealed override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandFireDistanceX = 20;
            HandFireDistanceY = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            Onehanded = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            SetBookProperty();
        }

        public override void Initialize() {
            useAnimation = Item.useAnimation;
            if (!Main.dedServ) {
                HandFireDistanceX = TextureValue.Width / 2;
            }
        }

        public override void HanderPlaySound() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            useAnimation -= Item.useTime;
            if (useAnimation <= 0) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                useAnimation = Item.useAnimation;
            }
        }

        public virtual void SetBookProperty() {

        }

        public override void FiringShoot() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            OrigItemShoot();
        }

        public override void FiringShootR() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            Owner.altFunctionUse = 2;
            OrigItemShoot();
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Vector2 orig = DirSign > 0 ? new Vector2(0, TextureValue.Height) : new Vector2(0, 0);
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation + offsetRot, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
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

    internal class EternityHeld : BaseMagicBook<Eternity> { }
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

    internal class SubsumingVortexHeld : BaseMagicBook<SubsumingVortex> { }
    internal class SubsumingVortexRItem : RMagicBook<SubsumingVortex> { }

    internal class TearsofHeavenHeld : BaseMagicBook<TearsofHeaven> { }
    internal class TearsofHeavenRItem : RMagicBook<TearsofHeaven> { }

    internal class TheDanceofLightHeld : BaseMagicBook<TheDanceofLight> { }
    internal class TheDanceofLightRItem : RMagicBook<TheDanceofLight> { }

    internal class TomeofFatesHeld : BaseMagicBook<TomeofFates> { }
    internal class TomeofFatesRItem : RMagicBook<TomeofFates> { }

    internal class TradewindsHeld : BaseMagicBook<Tradewinds> { }
    internal class TradewindsRItem : RMagicBook<Tradewinds> { }

    internal class VeeringWindHeld : BaseMagicBook<VeeringWind> { }
    internal class VeeringWindRItem : RMagicBook<VeeringWind> { }

    internal class WaywasherHeld : BaseMagicBook<Waywasher> { }
    internal class WaywasherRItem : RMagicBook<Waywasher> { }

    internal class WintersFuryHeld : BaseMagicBook<WintersFury> { }
    internal class WintersFuryRItem : RMagicBook<WintersFury> { }

    internal class WrathoftheAncientsHeld : BaseMagicBook<WrathoftheAncients> { }
    internal class WrathoftheAncientsRItem : RMagicBook<WrathoftheAncients> { }

    internal class BookofSkullsHeld() : BaseMagicBook { public override int targetCayItem => ItemID.BookofSkulls; }
    internal class BookofSkullsRItem : RMagicBook
    {
        public override int TargetID => ItemID.BookofSkulls;
        public override string HeldProjName => "BookofSkulls";
    }

    internal class WaterBoltHeld() : BaseMagicBook { public override int targetCayItem => ItemID.WaterBolt; }
    internal class WaterBoltRItem : RMagicBook
    {
        public override int TargetID => ItemID.WaterBolt;
        public override string HeldProjName => "WaterBolt";
    }

    internal class DemonScytheHeld() : BaseMagicBook { public override int targetCayItem => ItemID.DemonScythe; }
    internal class DemonScytheRItem : RMagicBook
    {
        public override int TargetID => ItemID.DemonScythe;
        public override string HeldProjName => "DemonScythe";
    }

    internal class RazorbladeTyphoonHeld() : BaseMagicBook { public override int targetCayItem => ItemID.RazorbladeTyphoon; }
    internal class RazorbladeTyphoonRItem : RMagicBook
    {
        public override int TargetID => ItemID.RazorbladeTyphoon;
        public override string HeldProjName => "RazorbladeTyphoon";
    }

    internal class LunarFlareBookHeld() : BaseMagicBook { public override int targetCayItem => ItemID.LunarFlareBook; }
    internal class LunarFlareBookRItem : RMagicBook
    {
        public override int TargetID => ItemID.LunarFlareBook;
        public override string HeldProjName => "LunarFlareBook";
    }

    internal class CrystalStormHeld() : BaseMagicBook { public override int targetCayItem => ItemID.CrystalStorm; }
    internal class CrystalStormRItem : RMagicBook
    {
        public override int TargetID => ItemID.CrystalStorm;
        public override string HeldProjName => "CrystalStorm";
    }

    internal class MagnetSphereHeld() : BaseMagicBook { public override int targetCayItem => ItemID.MagnetSphere; }
    internal class MagnetSphereRItem : RMagicBook
    {
        public override int TargetID => ItemID.MagnetSphere;
        public override string HeldProjName => "MagnetSphere";
    }

    internal class CursedFlamesHeld() : BaseMagicBook { public override int targetCayItem => ItemID.CursedFlames; }
    internal class CursedFlamesRItem : RMagicBook
    {
        public override int TargetID => ItemID.CursedFlames;
        public override string HeldProjName => "CursedFlames";
    }

    internal class GoldenShowerHeld() : BaseMagicBook { public override int targetCayItem => ItemID.GoldenShower; }
    internal class GoldenShowerRItem : RMagicBook
    {
        public override int TargetID => ItemID.GoldenShower;
        public override string HeldProjName => "GoldenShower";
    }
}
