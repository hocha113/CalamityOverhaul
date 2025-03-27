using CalamityMod.NPCs.ExoMechs.Artemis;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class DraedonsRemote : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item + "Tools/DraedonsRemote";
        public static Asset<Texture2D> Glow;
        public static LocalizedText DontUseByDeath { get; set; }
        void ICWRLoader.LoadAsset() => Glow = CWRUtils.GetT2DAsset(Texture + "Glow");
        void ICWRLoader.UnLoadData() => Glow = null;
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 64;
            DontUseByDeath = this.GetLocalization(nameof(DontUseByDeath), () => "The current game difficulty does not allow sending signals!");
        }
        public override void SetDefaults() {
            Item.width = 36;
            Item.height = 46;
            Item.value = Item.buyPrice(0, 2, 50, 50);
            Item.rare = ItemRarityID.Purple;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = Artemis.AttackSelectionSound;
            Item.useAnimation = 20;
            Item.useTime = 20;
            Item.autoReuse = false;
            Item.consumable = false;//还是不要消耗的好
            Item.shoot = ModContent.ProjectileType<DraedonsRemoteSpawn>();
            Item.CWR().IsShootCountCorlUse = true;
            Item.CWR().StorageUE = true;
            Item.CWR().MaxUEValue = 200;
            Item.CWR().ConsumeUseUE = 20;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override bool CanUseItem(Player player) {
            if (ModGanged.InfernumModeOpenState) {
                CombatText.NewText(player.Hitbox, Color.OrangeRed, DontUseByDeath.Value);
                SoundEngine.PlaySound(SoundID.MenuClose);
                return false;
            }

            if (Item.CWR().UEValue < Item.CWR().ConsumeUseUE) {
                CombatText.NewText(player.Hitbox, Color.DimGray, CWRLocText.Instance.EnergyShortage.Value);
                SoundEngine.PlaySound(SoundID.MenuClose);
                return false;
            }

            if (!NPC.AnyNPCs(NPCID.SkeletronPrime) && !NPC.AnyNPCs(NPCID.Retinazer)
            && !NPC.AnyNPCs(NPCID.Spazmatism) && !NPC.AnyNPCs(NPCID.TheDestroyer) && !Main.dayTime) {
                Item.CWR().UEValue -= Item.CWR().ConsumeUseUE;
                return true;
            }
            
            return false;
        }
           

        public override void AddRecipes() {
            _ = CreateRecipe()
                 .AddIngredient(ItemID.LunarBar, 2)
                 .AddIngredient(ItemID.Wire, 4)
                 .AddTile(TileID.LunarCraftingStation)
                 .Register();
        }
    }

    internal class DraedonsRemoteSpawn : ModProjectile
    {
        public override string Texture => CWRConstant.Item + "Tools/DraedonsRemote";
        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 46;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            Projectile.velocity = new Vector2(0, -6);
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Electric);
        }

        public override void OnKill(int timeLeft) {
            CWRWorld.MachineRebellion = true;
            //设置成1秒的锁定时间，因为服务器那边的生成是请求状态，大概会有一帧到两帧的广播延迟
            //这导致服务器生成的时候标签已经被关闭了，所以需要一个时间锁
            CWRWorld.DontCloseMachineRebellion = 60;//如果60tick还不够，那一定奸奇搞的鬼
            if (!VaultUtils.isClient) {
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.SkeletronPrime, false);
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.Retinazer, false);
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.Spazmatism, false);
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.TheDestroyer, false);
            }
        }
    }
}
