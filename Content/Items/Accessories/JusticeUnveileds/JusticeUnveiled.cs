using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Projectiles.Rogue;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.Longinus;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.JusticeUnveileds
{
    /// <summary>
    /// 正义的显现
    /// </summary>
    internal class JusticeUnveiled : ModItem
    {
        public static bool oldControlUp;
        public static bool justUp;
        public override string Texture => CWRConstant.Item_Accessorie + "JusticeUnveiled";
        public const int DropProbabilityDenominator = 6000;
        private static bool OnLoaden;
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 2, 15, 0);
            Item.rare = ModContent.RarityType<Turquoise>();
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.CWR().IsJusticeUnveiled = true;
            //检测换弹
            if (player.CWR().PlayerIsKreLoadTime > 0) {
                OnLoaden = true;
            }

            justUp = player.controlUp && !oldControlUp;
            oldControlUp = player.controlUp;
        }

        public static bool SpwanBool(Player player, Projectile projectile, NPC target, NPC.HitInfo hit) {
            int type = ModContent.ProjectileType<DivineJustice>();
            int type2 = ModContent.ProjectileType<JusticeUnveiledExplode>();
            int type3 = ModContent.ProjectileType<JUZenithWorldTime>();

            if (projectile.numHits > 0) {
                return false;
            }
            if (projectile.type == type || projectile.type == type2) {
                return false;
            }

            if (!player.CWR().IsJusticeUnveiled) {
                return false;
            }

            if (Main.zenithWorld) {
                if (player.CountProjectilesOfID(type3) == 0) {
                    Projectile.NewProjectile(player.FromObjectGetParent(), target.Center, Vector2.Zero, type3, 0, 0, player.whoAmI);
                    return true;
                }
                else {
                    return false;
                }
            }

            Item item = player.GetItem();
            if (item.type > ItemID.None && item.CWR().HasCartridgeHolder && item.CWR().AmmoCapacity <= 20) {
                if (OnLoaden) {
                    OnLoaden = false;
                    return true;
                }
            }

            if (projectile.type == ModContent.ProjectileType<StellarContemptEcho>()
                || projectile.type == ModContent.ProjectileType<GalaxySmasherEcho>()
                || projectile.type == ModContent.ProjectileType<TriactisHammerProj>()
                || projectile.type == ModContent.ProjectileType<LonginusThrow>()) {
                return true;
            }

            if (projectile.type == ModContent.ProjectileType<ExorcismProj>() && projectile.Calamity().stealthStrike) {
                return true;
            }

            //修改触发条件，不再是暴击，而是按概率给予玩家触发机会
            if (projectile.DamageType == DamageClass.Ranged) {
                if (hit.Crit && Main.rand.NextBool(3)) {//暴击后33%概率给予机会
                    player.CWR().JusticeUnveiledCharges++;
                    if (player.whoAmI == Main.myPlayer && player.CWR().JusticeUnveiledCharges <= 5) {
                        Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero
                            , ModContent.ProjectileType<JusticeUnveiledCross>(), 0, 0, player.whoAmI, player.CWR().JusticeUnveiledCharges);
                    }
                    if (player.CWR().JusticeUnveiledCharges > 5) {
                        player.CWR().JusticeUnveiledCharges = 5;//最多5次
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 生成命中十字标记特效
        /// </summary>
        public static void SpawnCrossMarker(NPC target, int whoAmI) {
            if (whoAmI == Main.myPlayer) {
                //生成十字标记弹幕
                Projectile.NewProjectile(
                    Main.player[Main.myPlayer].GetSource_Misc("JusticeUnveiledMark"),
                    target.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<JusticeCrossMark>(),
                    0,
                    0f,
                    Main.myPlayer,
                    target.whoAmI
                );
            }

            //命中闪光特效（弱化）
            for (int i = 0; i < 2; i++) {
                float angle = MathHelper.TwoPi * i / 2f;
                Vector2 direction = angle.ToRotationVector2();

                //金色光线爆发（减少数量）
                for (int j = 0; j < 3; j++) {
                    Vector2 velocity = direction * Main.rand.NextFloat(2f, 5f);
                    Dust light = Dust.NewDustPerfect(
                        target.Center,
                        DustID.GoldCoin,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(1f, 1.5f)
                    );
                    light.noGravity = true;
                    light.fadeIn = 0.8f;
                }
            }

            //环形冲击波粒子（减少数量）
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 velocity = angle.ToRotationVector2() * 5f;
                Dust ring = Dust.NewDustPerfect(
                    target.Center,
                    DustID.Electric,
                    velocity,
                    0,
                    Color.Gold,
                    Main.rand.NextFloat(1f, 1.5f)
                );
                ring.noGravity = true;
            }

            //命中音效（降低音量）
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 0.3f,
                Pitch = 0.5f
            }, target.Center);
        }

        public static void OnHitNPCSpwanProj(Player player, Projectile projectile, NPC target, NPC.HitInfo hit) {
            if (!SpwanBool(player, projectile, target, hit)) {
                return;
            }

            if (Main.zenithWorld && projectile.type == ModContent.ProjectileType<LonginusThrow>()) {
                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.friendly) {
                        continue;
                    }
                    Projectile.NewProjectile(player.FromObjectGetParent()
                    , npc.Center + new Vector2(0, -1120), new Vector2(0, 6)
                    , ModContent.ProjectileType<DivineJustice>(), projectile.damage, 2, player.whoAmI, npc.whoAmI);
                }
                return;
            }
        }
    }
}
