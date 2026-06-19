using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class DragonsWord : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "DragonsWord";
        [VaultLoaden(CWRConstant.Item_Magic + "DragonsWordGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.width = 68;
            Item.height = 78;
            Item.damage = 682;
            Item.mana = 80;
            Item.shootSpeed = 6;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.DamageType = DamageClass.Magic;
            Item.useTime = Item.useAnimation = 60;
            Item.rare = ItemRarityID.Red;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item92;
            Item.value = Item.buyPrice(0, 85, 5, 5);
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<DragonsWordProj>();
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_DragonsWord;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<DragonsWordMouse>(), damage, knockback, player.whoAmI, 0f, 0.03f);
                return false;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 vr = (MathHelper.TwoPi / 3f * i + Main.GameUpdateCount * 0.1f).ToRotationVector2();
                Projectile.NewProjectile(source, player.Center + vr * Main.rand.Next(22, 38), vr.RotatedByRandom(0.32f) * 3
                , type, damage, knockback, player.whoAmI, 0f, 0.03f);
            }
            return false;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<DragonsWordMouse>()] <= 0;
    }

    internal class DragonsWordCut : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.timeLeft = 22;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ArmorPenetration = 1000;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 180);
        }

        public override void OnKill(int timeLeft) {
            PRTLoader.NewParticle<PRT_DragonsWordCut>(Projectile.Center, new Vector2(0.1f, 0.1f)
                .RotatedByRandom(100), Main.rand.NextBool() ? Color.DarkRed : Color.IndianRed, Main.rand.NextFloat(0.65f, 0.85f)).Configure(false, 19);
            SoundStyle sound = "CalamityMod/Sounds/Item/MurasamaHitOrganic".GetSound();
            SoundEngine.PlaySound(sound with { Volume = 0.8f, PitchRange = (0.6f, 0.7f) }, Projectile.Center);
        }
    }
    internal class DragonsWordMouse : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        private Vector2 targetPos;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 122;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
        }

        private void SpanDragonsFireEffect(float maxNum) {
            for (int i = 0; i < maxNum; i++) {
                Vector2 spanPos = (MathHelper.TwoPi / maxNum * i + Projectile.ai[0] * 0.1f).ToRotationVector2() * Projectile.ai[1] + Projectile.Center;
                PRTLoader.NewParticle<PRT_LavaFire>(
                    spanPos, new Vector2(0, -3),
                    Color.White, Main.rand.NextFloat(0.2f, 0.3f) * (1 + Projectile.ai[1] * 0.006f))?.SetLifetime(10, 15);
            }
        }

        private void SpanDragonsWordCut() {
            int num = 255;
            foreach (var npc in Main.npc) {
                if (num <= 0) {
                    break;
                }
                if (!npc.Alives()) {
                    continue;
                }
                if (npc.friendly) {
                    continue;
                }
                if (npc.Distance(Projectile.Center) > Projectile.ai[1]) {
                    continue;
                }
                if (Projectile.ai[0] % 15 == 0) {
                    if (Owner.name == "Sakura") {
                        num *= 5;
                    }
                    int newDmg = (int)(Projectile.damage * (0.2f + num / 55f));
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), npc.Center, Vector2.Zero
                        , ModContent.ProjectileType<DragonsWordCut>(), newDmg, 2, Owner.whoAmI, 0f, 0.03f);
                    num--;
                }
            }
        }

        private void InOwner() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            SetDirection();
            if (Projectile.ai[0] == 0) {
                SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Providence/ProvidenceHolyRay".GetSound());
                targetPos = InMousePos;
            }
            targetPos = Vector2.Lerp(targetPos, InMousePos, 0.1f);
            Projectile.Center = targetPos;
        }

        private void UpdateSakura() {
            if (DownRight && Owner.CheckMana(Owner.GetItem())) {
                if (Owner.name == "Sakura") {
                    Owner.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
                    if (Main.rand.NextBool(300)) {
                        Owner.AddBuff(BuffID.Darkness, 60);
                    }
                }

                Owner.statMana -= 1;
                Owner.manaRegenDelay = 6;
                if (Projectile.ai[1] < 660) {
                    Projectile.ai[1] += 2;
                }
            }
            else {
                Projectile.ai[1] -= 6;
                if (Projectile.ai[1] <= 0) {
                    Projectile.Kill();
                }
            }
        }

        public override void AI() {
            InOwner();
            UpdateSakura();

            if (Projectile.ai[1] >= 0) {
                SpanDragonsFireEffect(300);
                SpanDragonsWordCut();
            }

            Projectile.ai[0]++;
        }
    }

    internal class DragonsWordProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float Time => ref Projectile.ai[0];
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 18;
            Projectile.extraUpdates = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
            Projectile.timeLeft = 1220 * Projectile.extraUpdates;
        }

        public override bool? CanHitNPC(NPC target) {
            return Time < 150 * Projectile.extraUpdates ? false : base.CanHitNPC(target);
        }

        public override bool PreAI() {
            Player Owner = Main.player[Projectile.owner];
            float targetDist = Vector2.Distance(Owner.Center, Projectile.Center);
            if (!VaultUtils.isServer) {
                float OrbSize = Main.rand.NextFloat(0.5f, 0.8f);
                PRTLoader.NewParticle<PRT_Bloomlight>(Projectile.Center, Vector2.Zero, Color.OrangeRed, OrbSize + 0.6f).Configure(8);
                PRTLoader.NewParticle<PRT_Bloomlight>(Projectile.Center, Vector2.Zero, Color.White, OrbSize + 0.2f).Configure(8);
                if (Time % 5 == 0 && Time > 35f && targetDist < 1400f) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(1 + Time * 0.1f, 1 + Time * 0.1f), -Projectile.velocity * 0.5f, Main.rand.NextBool() ? Color.DarkOrange : Color.OrangeRed, Main.rand.NextFloat(0.4f, 0.7f)).Configure(false, 15);
                }
            }
            if (Time > 160 * Projectile.extraUpdates) {
                NPC target = Projectile.Center.FindClosestNPC(1600);
                if (target != null) {
                    if (Time < 290 * Projectile.extraUpdates) {
                        Projectile.SmoothHomingBehavior(target.Center, 1, 0.08f);
                    }
                    else {
                        Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    }
                }
            }
            else {
                Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.ai[1]);
            }
            Time++;
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_Dragonfire, 420);
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 16; i++) {
                float OrbSize = Main.rand.NextFloat(1.5f, 1.8f);
                PRTLoader.NewParticle<PRT_Bloomlight>(Projectile.Center, Vector2.Zero, Color.OrangeRed, OrbSize + 0.6f).Configure(8);
                PRTLoader.NewParticle<PRT_Bloomlight>(Projectile.Center, Vector2.Zero, Color.White, OrbSize + 0.2f).Configure(8);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(11 + Time * 0.1f, 11 + Time * 0.1f), -Projectile.velocity * 0.5f, Main.rand.NextBool() ? Color.DarkOrange : Color.OrangeRed, Main.rand.NextFloat(0.4f, 0.7f)).Configure(false, 15);
            }

            Projectile.Explode();
        }
    }
}
