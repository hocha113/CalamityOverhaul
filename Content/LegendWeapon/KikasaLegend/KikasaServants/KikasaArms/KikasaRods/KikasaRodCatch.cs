using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaArmsPalette;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaRods
{
    /// <summary>
    /// 湖底渔获：钓奴从血湖里拽出来甩向敌人的东西——罐头、旧靴、水草、鱼，
    /// 大物是一整条旗鱼。抛物线翻滚飞行，落地即碎成水珠。
    /// ai0 = 皮肤序（贴图查表），ai1 = 大物旗；生成包自含
    /// </summary>
    internal class KikasaRodCatch : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>渔获皮肤池：全用原版物品贴图，湖里捞出来的破烂与鲜鱼</summary>
        private static readonly int[] CatchSkins = [
            ItemID.TinCan,
            ItemID.OldShoe,
            ItemID.FishingSeaweed,
            ItemID.Bass,
            ItemID.Trout,
            ItemID.Tuna,
        ];

        private ref float SkinIndex => ref Projectile.ai[0];
        private ref float BigFlag => ref Projectile.ai[1];

        private bool Big => BigFlag > 0.5f;

        private int SkinItemType => Big
            ? ItemID.Swordfish
            : CatchSkins[Math.Clamp((int)SkinIndex, 0, CatchSkins.Length - 1)];

        private ref float SpinDir => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
        }

        public override void AI() {
            if (SpinDir == 0f) {
                SpinDir = Projectile.velocity.X >= 0f ? 1f : -1f;
                if (Big) {
                    Projectile.penetrate = 3;
                    Projectile.width = Projectile.height = 34;
                    Projectile.scale = 1.15f;
                }
            }

            //抛物线 + 翻滚：被甩出去的死物
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 18f);
            Projectile.rotation += SpinDir * (Big ? 0.12f : 0.22f);

            //带着湖水飞：隔拍甩珠
            if (!Main.dedServ && Projectile.timeLeft % 4 == 0 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.08f + new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                    BloodMain * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.24f, 0.42f) * (Big ? 1.4f : 1f))
                    ?.Configure(Main.rand.Next(10, 18), 0f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //落点闷响 + 水珠崩散：湿东西砸地
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = Big ? 0.6f : 0.38f, Pitch = Big ? -0.5f : -0.15f, MaxInstances = 3
            }, Projectile.Center);
            int burst = Big ? 12 : 7;
            for (int k = 0; k < burst; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(Projectile.Center,
                    Main.rand.NextVector2Circular(3.5f, 3f) - new Vector2(0f, Main.rand.NextFloat(1f, 3f)),
                    BloodMain * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.3f, 0.52f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.3f, 0.3f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int itemType = SkinItemType;
            Main.instance.LoadItem(itemType);
            Texture2D tex = TextureAssets.Item[itemType]?.Value;
            if (tex == null) {
                return false;
            }
            //实物本体带湿感血渍：物是真物，只是刚从血湖里捞出来
            Color wet = Color.Lerp(lightColor, BloodMain, 0.3f);
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null,
                wet, Projectile.rotation, tex.Size() * 0.5f, Projectile.scale,
                SpinDir < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            return true;
        }
    }
}
