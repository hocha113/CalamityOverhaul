using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Items.Ranged.HeavenfallLongbows;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class ModifyInfinitePick : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<InfinitePick>();
        public override bool DrawingInfo => false;
        public override bool CanLoadLocalization => false;
        //在某些不应该的情况下，武器会被禁止使用，使用这个钩子来防止这种事情的发生
        public override bool? On_CanUseItem(Item item, Player player) => true;
    }

    internal class InfinitePick : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item + "Tools/Pickaxe";
        private static bool IsPick = true;
        [VaultLoaden(CWRConstant.Item + "Tools/Pickaxe")]
        private static Asset<Texture2D> Pickaxe = null;
        [VaultLoaden(CWRConstant.Item + "Tools/Hammer")]
        private static Asset<Texture2D> Hammer = null;
        private bool rDown;
        private bool oldRDown;
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 4));
        }
        public override void SetDefaults() {
            Item.damage = 9999;
            Item.knockBack = 6;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 1;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.DamageType = EndlessDamageClass.Instance;
            Item.value = Item.buyPrice(gold: 999);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.pick = 9999;
            Item.tileBoost = 64;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_InfinitePick;
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) => player.itemLocation = player.GetPlayerStabilityCenter();

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit = 9999;

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) => damage = damage.Scale(0);

        public override void HoldItem(Player player) {
            if (Main.myPlayer != player.whoAmI) {
                return;
            }

            if (IsPick) {
                Item.pick = 9999;
                Item.hammer = 0;
                Item.useTime = 1;
                Item.useAnimation = 10;

            }
            else {
                Item.pick = 0;
                Item.hammer = 9999;
                Item.useTime = 2;
                Item.useAnimation = 10;
            }

            if (CWRKeySystem.WeponSkill_Q.JustPressed) {
                IsPick = !IsPick;
                SoundEngine.PlaySound(!IsPick ? CWRSound.Pecharge : CWRSound.Peuncharge, player.Center);
                TextureAssets.Item[Type] = IsPick ? Pickaxe : Hammer;
            }

            rDown = player.PressKey(false);
            bool justRDown = rDown && !oldRDown;
            oldRDown = rDown;

            if (justRDown && !player.mouseInterface && !player.cursorItemIconEnabled && player.cursorItemIconID == ItemID.None) {
                Projectile.NewProjectile(player.FromObjectGetParent(), player.GetPlayerStabilityCenter()
                    , player.Center.To(Main.MouseWorld).UnitVector() * 32, ModContent.ProjectileType<InfinitePickProj>()
                    , Item.damage, 0, player.whoAmI, IsPick ? 1 : 0, Main.MouseWorld.X, Main.MouseWorld.Y);
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            if (IsPick) {
                TooltipLine cumstops = tooltips.FirstOrDefault((TooltipLine x) => x.Name == "PickPower" && x.Mod == "Terraria");
                if (cumstops != null) {
                    string typeV = Language.GetTextValue("LegacyTooltip.26");
                    cumstops.Text = $"{int.MaxValue}{typeV}";
                }
            }
            else {
                TooltipLine cumstops = tooltips.FirstOrDefault((TooltipLine x) => x.Name == "HammerPower" && x.Mod == "Terraria");
                if (cumstops != null) {
                    string typeV = Language.GetTextValue("LegacyTooltip.28");
                    cumstops.Text = $"{int.MaxValue}{typeV}";
                }
            }

            tooltips.InsertHotkeyBinding(CWRKeySystem.WeponSkill_Q, noneTip: CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.WeponSkill_Q.DisplayName}]");
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset) {
            if ((line.Name == "ItemName" || line.Name == "Damage" || line.Name == "PickPower" || line.Name == "HammerPower")
                && line.Mod == "Terraria") {
                InfiniteIngot.DrawColorText(Main.spriteBatch, line);
                return false;
            }
            return true;
        }
    }

    internal class InfinitePickProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public List<int> dropTypes = [];
        private bool spwan;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.MaxUpdates = 13;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
        }

        public override void AI() {
            InMousePos = new Vector2(Projectile.ai[1], Projectile.ai[2]);
            Lighting.AddLight(Projectile.Center, Main.DiscoColor.ToVector3() * (Projectile.ai[0] == 1 ? 1.2f : 10));
            if (!spwan && !Main.dedServ) {
                SoundEngine.PlaySound(new SoundStyle(CWRConstant.Sound + "Pedestruct"), Owner.Center);
            }

            if (Projectile.ai[0] == 1) {
                HandleProjectileAIForType1();
            }
            else {
                HandleProjectileAIForType0();
            }

            foreach (var item in Main.ActiveItems) {
                if (item.Hitbox.Intersects(Projectile.Hitbox)) {
                    item.active = false;
                }
            }
            spwan = true;
        }

        private void HandleProjectileAIForType1() {
            if (!spwan) {
                Vector2 projPos = Projectile.Center;
                Projectile.width = Projectile.height = 64;
                Projectile.Center = projPos;
            }

            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    SpawnSpark(Projectile.Center + VaultUtils.RandVr(13), Projectile.velocity);
                }
            }

            ProcessTilesInArea(Projectile.position, Projectile.width, Projectile.height);
        }

        private void HandleProjectileAIForType0() {
            if (spwan) {
                return;
            }
            if (!Main.dedServ) {
                for (int i = 0; i < 188; i++) {
                    SpawnSpark(InMousePos + VaultUtils.RandVr(213), new Vector2(0, 3));
                }
            }
            Vector2 pos = InMousePos - new Vector2(500, 500) / 2;
            ProcessTilesInArea(pos, 500, 500);
        }

        private static void SpawnSpark(Vector2 position, Vector2 velocity) {
            PRTLoader.NewParticle<PRT_HeavenfallStar>(position, velocity, VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors), 1).Configure(false, 13);
        }

        private void ProcessTilesInArea(Vector2 startPos, int width, int height) {
            for (int x = 0; x < width / 16; x++) {
                for (int y = 0; y < height / 16; y++) {
                    Vector2 tilePos = startPos + new Vector2(x, y) * 16;
                    ProcessTile(Framing.GetTileSafely(tilePos), tilePos);
                }
            }
        }

        private void ProcessTile(Tile tile, Vector2 tilePos) {
            tile.LiquidAmount = 0;
            tilePos /= 16;//世界坐标转格坐标
            if (VaultUtils.TryKillChest(tilePos.ToPoint16(), out var chestItems, true, false, false)) {
                foreach (var item in chestItems) {
                    dropTypes.Add(item.type);
                }
            }
            //挖掘判定
            if (tile.HasTile && WorldGen.CanKillTile((int)tilePos.X, (int)tilePos.Y)) {
                if (VaultUtils.IsTopLeft((int)tilePos.X, (int)tilePos.Y, out _)) {
                    int dorptype = tile.GetTileDrop((int)tilePos.X, (int)tilePos.Y);
                    if (dorptype != 0) {
                        dropTypes.Add(dorptype);
                    }
                }
                WorldGen.KillTile((int)tilePos.X, (int)tilePos.Y, noItem: true);
            }
            //锤形态才可拆墙
            if (Projectile.ai[0] == 0 && tile.WallType != WallID.None) {
                if (CWRLoad.WallToItem.TryGetValue(tile.WallType, out int wallValue) && wallValue != 0) {
                    dropTypes.Add(wallValue);
                }
                tile.WallType = WallID.None;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), target.position + new Vector2(Main.rand.Next(-160, 160), -420)
                    , new Vector2(0, 13), ModContent.ProjectileType<InfiniteEnmgs>(), Projectile.damage / 2, 0, Projectile.owner);
            }
            for (int i = 0; i < 36; i++) {
                Color outerSparkColor = VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors);
                Vector2 vector = Main.rand.NextVector2Unit() * Main.rand.Next(77);

                float scaleBoost = MathHelper.Clamp(i * 0.005f, 0f, 2f);
                float outerSparkScale = 3.2f + scaleBoost;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vector, outerSparkColor, outerSparkScale).Configure(false, 7);

                Color innerSparkColor = VaultUtils.MultiStepColorLerp(i % 30 / 30f, HeavenfallLongbow.rainbowColors);
                float innerSparkScale = 0.6f + scaleBoost;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, vector, innerSparkColor, innerSparkScale).Configure(false, 7);
            }
        }

        public override void OnKill(int timeLeft) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Item ball = new Item(ModContent.ItemType<DarkMatterBall>());
            DarkMatterBall darkMatterBall = (DarkMatterBall)ball.ModItem;
            if (dropTypes.Count <= 0 || darkMatterBall == null) {
                return;
            }

            Vector2 spanPos = Projectile.Center;
            if (Projectile.ai[0] != 1) {
                spanPos = InMousePos;
            }

            int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), spanPos, Vector2.Zero
                , ModContent.ProjectileType<SpanDMBall>(), 0, 0, Projectile.owner, ai1: Projectile.ai[0]);
            Projectile projectile = Main.projectile[proj];
            if (projectile.ModProjectile is SpanDMBall span) {
                foreach (var id in dropTypes) {
                    darkMatterBall.DorpItems.Add(new Item(id));
                }
                span.darkMatterBall = darkMatterBall;
            }
            projectile.netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    internal class InfiniteEnmgs : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 2;
            Projectile.timeLeft = 130;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Main.DiscoColor.ToVector3());
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            NPC potentialTarget = Projectile.Center.FindClosestNPC(1500f, true, true);
            if (potentialTarget != null) {
                Projectile.velocity = (Projectile.velocity * 29f + Projectile.To(potentialTarget.Center).UnitVector() * 21f) / 30f;
                Projectile.velocity *= 1.01f;
            }

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vector = Projectile.velocity * 1.05f;
                    float slp = Main.rand.NextFloat(0.5f, 0.9f);
                    PRTLoader.NewParticle<PRT_HeavenStar>(Projectile.Center, vector, Color.White, 1f).Configure(VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors), 0f, new Vector2(0.6f, 1f) * slp, new Vector2(1.5f, 2.7f) * slp, 20 + Main.rand.Next(6), 0f, 3f, 0f, Main.rand.Next(7) * 2, Main.rand.NextFloat(-0.3f, 0.3f));
                }
            }
        }
    }

    internal class SpanDMBall : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item + "Tools/DarkMatter";
        internal DarkMatterBall darkMatterBall;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 130;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.hostile = false;
        }

        public override void NetHeldSend(BinaryWriter writer) {
            if (darkMatterBall != null && darkMatterBall.Type > ItemID.None) {
                ItemIO.Send(darkMatterBall.Item, writer, true);
            }
            else {
                ItemIO.Send(new Item(), writer, true);
            }
        }

        public override void NetHeldReceive(BinaryReader reader) {
            var item = ItemIO.Receive(reader, true);
            if (item.Alives() && item.ModItem != null && item.ModItem is DarkMatterBall _darkMatterBall) {
                darkMatterBall = _darkMatterBall;
            }
        }

        public override void AI() {
            if (Projectile.ai[0] > 60) {
                Projectile.ChasingBehavior(Owner.Center, 13);
                Projectile.Center = Vector2.Lerp(Projectile.Center, Owner.Center, 0.04f);
                if (Projectile.Distance(Owner.Center) < Projectile.width) {
                    Projectile.Kill();
                }
                Projectile.scale -= 0.02f;
            }
            else {
                Projectile.rotation += 0.1f;
                Projectile.scale += 0.02f;
                Projectile.alpha += 5;
                if (Projectile.alpha > 255) {
                    Projectile.alpha = 255;
                }
            }
            Projectile.ai[0]++;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.QuickSpawnItem(Owner.FromObjectGetParent(), darkMatterBall.Item, 1);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float alp = Projectile.alpha / 255f;

            if (Projectile.ai[1] != 1) {
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Color drawColor = Color.White * alp * 0.02f;
                Vector2 drawOrig = DarkMatterBall.DarkMatter.Size() / 2;
                float slp = (255 - Projectile.alpha) / 15f;
                for (int i = 0; i < 113; i++) {
                    Main.EntitySpriteDraw(DarkMatterBall.DarkMatter.Value, drawPos, null, drawColor
                    , Projectile.rotation + (MathHelper.TwoPi / 113 * i), drawOrig, slp, SpriteEffects.None, 0);
                }
            }

            Main.EntitySpriteDraw(DarkMatterBall.DarkMatter.Value, Projectile.Center - Main.screenPosition, null, Color.White * alp
                , Projectile.rotation, DarkMatterBall.DarkMatter.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
