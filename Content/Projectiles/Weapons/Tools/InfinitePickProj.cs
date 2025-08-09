using CalamityMod.Particles;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Tools
{
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
                    SpawnSpark(Projectile.Center + CWRUtils.randVr(13), Projectile.velocity);
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
                    SpawnSpark(InMousePos + CWRUtils.randVr(213), new Vector2(0, 3));
                }
            }
            Vector2 pos = InMousePos - new Vector2(500, 500) / 2;
            ProcessTilesInArea(pos, 500, 500);
        }

        private static void SpawnSpark(Vector2 position, Vector2 velocity) {
            PRT_HeavenfallStar spark = new PRT_HeavenfallStar(position, velocity, false, 13, 1
                , VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors));
            PRTLoader.AddParticle(spark);
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
            tilePos /= 16;//坐标默认为世界实体坐标，所以需要除以16进行换算
            if (VaultUtils.TryKillChest(tilePos.ToPoint16(), out var chestItems, true, false, false)) {
                foreach (var item in chestItems) {
                    dropTypes.Add(item.type);
                }
            }
            //判断是否进行挖掘
            if (tile.HasTile && WorldGen.CanKillTile((int)tilePos.X, (int)tilePos.Y)) {
                if (VaultUtils.IsTopLeft((int)tilePos.X, (int)tilePos.Y, out _)) {
                    int dorptype = tile.GetTileDrop();
                    if (dorptype != 0) {
                        dropTypes.Add(dorptype);
                    }
                }
                WorldGen.KillTile((int)tilePos.X, (int)tilePos.Y, noItem: true);
            }
            //锤子形态下才能拆墙
            if (Projectile.ai[0] == 0 && tile.WallType != 0) {
                if (CWRLoad.WallToItem.TryGetValue(tile.WallType, out int wallValue) && wallValue != 0) {
                    dropTypes.Add(wallValue);
                }
                tile.WallType = 0;
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
                float slp = Main.rand.NextFloat(0.5f, 0.9f);
                GeneralParticleHandler.SpawnParticle(new FlareShine(Projectile.Center + Main.rand.NextVector2Unit() * 13, vector, Color.White, outerSparkColor
                    , 0f, new Vector2(0.6f, 1f) * slp, new Vector2(1.5f, 2.7f) * slp, 20 + Main.rand.Next(6), 0f, 3f, 0f, Main.rand.Next(7) * 2));

                float scaleBoost = MathHelper.Clamp(i * 0.005f, 0f, 2f);
                float outerSparkScale = 3.2f + scaleBoost;
                PRT_HeavenfallStar spark = new PRT_HeavenfallStar(Projectile.Center, vector, false, 7, outerSparkScale, outerSparkColor);
                PRTLoader.AddParticle(spark);

                Color innerSparkColor = VaultUtils.MultiStepColorLerp(i % 30 / 30f, HeavenfallLongbow.rainbowColors);
                float innerSparkScale = 0.6f + scaleBoost;
                PRT_HeavenfallStar spark2 = new PRT_HeavenfallStar(Projectile.Center, vector, false, 7, innerSparkScale, innerSparkColor);
                PRTLoader.AddParticle(spark2);
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
}
