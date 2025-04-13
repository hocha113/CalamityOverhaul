using CalamityMod.NPCs.ExoMechs.Artemis;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class MachineRebellionSceneEffect : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/DEMSoulforge");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => DraedonsRemote.LoadenMusic && CWRWorld.MachineRebellion;
    }

    internal class DraedonsRemote : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item + "Tools/DraedonsRemote";
        public static bool LoadenMusic => false;//他妈的在出现曲师写出新音乐之前这个都不能删
        public static Asset<Texture2D> Glow;
        public static LocalizedText DontUseByDeath { get; set; }
        internal static LocalizedText Text1;
        internal static LocalizedText Text2;
        internal static LocalizedText Text3;
        internal static LocalizedText Text4;
        internal static LocalizedText Text5;
        internal static LocalizedText Text6;
        internal static LocalizedText Text7;
        internal static LocalizedText Text8;
        internal static LocalizedText Text9;
        void ICWRLoader.LoadAsset() => Glow = CWRUtils.GetT2DAsset(Texture + "Glow");
        void ICWRLoader.UnLoadData() => Glow = null;
        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "");
            Text2 = this.GetLocalization(nameof(Text2), () => "");
            Text3 = this.GetLocalization(nameof(Text3), () => "");
            Text4 = this.GetLocalization(nameof(Text4), () => "");
            Text5 = this.GetLocalization(nameof(Text5), () => "");
            Text6 = this.GetLocalization(nameof(Text6), () => "");
            Text7 = this.GetLocalization(nameof(Text7), () => "");
            Text8 = this.GetLocalization(nameof(Text8), () => "");
            Text9 = this.GetLocalization(nameof(Text9), () => "");
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
            Projectile.timeLeft = CWRWorld.MachineRebellionDowned ? 180 : 1680;//如果已经打败了机械暴乱就不要再过剧情了
            if (DraedonsRemote.LoadenMusic) {
                Projectile.timeLeft = 180;
            }
        }

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                Projectile.velocity = new Vector2(0, -6);
            }

            CWRWorld.MachineRebellion = true;
            //设置成1秒的锁定时间，因为服务器那边的生成是请求状态，大概会有一帧到两帧的广播延迟
            //这导致服务器生成的时候标签已经被关闭了，所以需要一个时间锁
            CWRWorld.DontCloseMachineRebellion = 60;//如果60tick还不够，那一定奸奇捣的鬼

            if (!Main.player[Projectile.owner].Alives()) {
                Projectile.active = false;
                return;
            }
            
            if (!Main.dedServ) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , Projectile.ai[0] < 180 ? DustID.Electric : DustID.RedTorch);
                Main.dust[dust].noGravity = Projectile.ai[0] < 180;
            }

            if (Projectile.ai[0] > 30 && Projectile.ai[0] <= 180) {
                Projectile.velocity *= 0.99f;
            }

            if (Projectile.ai[0] > 180) {
                if (Projectile.ai[1] > 40) {
                    BasePRT pulse = new PRT_LonginusWave(Projectile.Center, Vector2.Zero, Color.Red, new Vector2(2f, 2f), 0, 0.82f, 3.32f, 80, Projectile);
                    PRTLoader.AddParticle(pulse);
                    Projectile.ai[1] = 0;
                }

                Projectile.ChasingBehavior(Main.player[Projectile.owner].Center + new Vector2(0, -300), 23, 32);
            }

            if (DraedonsRemote.LoadenMusic && !VaultUtils.isClient) {
                string[] dialogueTexts = [
                    DraedonsRemote.Text1.Value,
                    DraedonsRemote.Text2.Value,
                    DraedonsRemote.Text3.Value,
                    DraedonsRemote.Text4.Value,
                    DraedonsRemote.Text5.Value,
                    DraedonsRemote.Text6.Value,
                    DraedonsRemote.Text7.Value,
                    DraedonsRemote.Text8.Value,
                    DraedonsRemote.Text9.Value,
                ];

                float ai = Projectile.ai[0];
                int index = (int)(ai / 200f);

                if (index >= 0 && index < dialogueTexts.Length && ai % 200 == 0) {
                    Color textColor = VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Gray, Color.AliceBlue);
                    VaultUtils.Text(dialogueTexts[index], textColor);
                }
            }


            Projectile.ai[1]++;
            Projectile.ai[0]++;
        }

        public override void OnKill(int timeLeft) {
            BasePRT pulse = new PRT_DWave(Projectile.Center, Vector2.Zero, Color.Red, new Vector2(2f, 2f), 0, 0.82f, 13.32f, 80);
            PRTLoader.AddParticle(pulse);

            if (VaultUtils.isClient) {
                return;
            }

            if (!Projectile.Center.TryFindClosestPlayer(out var player)) {
                return;
            }

            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == NPCID.SkeletronPrime || npc.type == NPCID.Retinazer 
                    || npc.type == NPCID.Spazmatism || npc.type == NPCID.TheDestroyer) {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                    npc.netUpdate = true;
                }
            }

            VaultUtils.TrySpawnBossWithNet(player, NPCID.SkeletronPrime, false);
            VaultUtils.TrySpawnBossWithNet(player, NPCID.Retinazer, false);
            VaultUtils.TrySpawnBossWithNet(player, NPCID.Spazmatism, false);
            VaultUtils.TrySpawnBossWithNet(player, NPCID.TheDestroyer, false);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            value = DraedonsRemote.Glow.Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
