using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 验工印章：铸造监工的头号奖励，直接回答 L6 的通行痛点（机关）。
    /// 陷阱类弹幕伤害 -25%，且 15% 概率哑火（完全闪避）。
    /// 首杀必掉/复杀 25%，结算在 DungeonworldBossRecords.ServerSettleKill。
    /// 贴图借原版齿轮，绘制期炉橙描染（零新画像素）。
    /// 效果挂共置的 ProofSealPlayer（session 旗标，判定全在受害端本地）
    /// </summary>
    internal class ProofSealCharm : OverseerModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Cog;

        private static readonly Color SealTint = new(226, 168, 96);

        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.accessory = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 2);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<ProofSealPlayer>().sealActive = true;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Texture2D tex = TextureAssets.Item[Item.type]?.Value;
            if (tex == null) {
                return true;
            }
            spriteBatch.Draw(tex, position, frame, drawColor.MultiplyRGB(SealTint), 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            ref float rotation, ref float scale, int whoAmI) {
            Texture2D tex = TextureAssets.Item[Item.type]?.Value;
            if (tex == null) {
                return true;
            }
            spriteBatch.Draw(tex, Item.Center - Main.screenPosition, null,
                lightColor.MultiplyRGB(SealTint), rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 验工印章的效果载体：陷阱弹幕减伤与哑火。
    /// 命中判定与减免全在受害端本地解算（原版被弹命中契约），哑火掷骰只影响本机受击，
    /// 无跨端状态；旗标 session 态不入存档
    /// </summary>
    internal class ProofSealPlayer : OverseerModPlayer
    {
        internal bool sealActive;

        /// <summary>陷阱弹幕哑火概率（百分比）</summary>
        internal const int MisfirePercent = 15;

        public override void ResetEffects() {
            sealActive = false;
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (sealActive && proj.trap) {
                //陷阱伤害 -25%
                modifiers.FinalDamage *= 0.75f;
            }
        }

        public override bool FreeDodge(Player.HurtInfo info) {
            if (!sealActive) {
                return false;
            }
            //只对陷阱弹幕哑火（受害端本地掷骰，仅影响本机判定）
            int projIdx = info.DamageSource.SourceProjectileLocalIndex;
            if (projIdx < 0 || projIdx >= Main.maxProjectiles) {
                return false;
            }
            Projectile proj = Main.projectile[projIdx];
            if (!proj.active || !proj.trap) {
                return false;
            }
            if (Main.rand.Next(100) < MisfirePercent) {
                //哑火提示（本机演出）
                if (!Main.dedServ && Player.whoAmI == Main.myPlayer) {
                    CombatText.NewText(Player.Hitbox, FoundryOverseer.LampGreen,
                        Terraria.Localization.Language.GetTextValue("Mods.CalamityOverhaul.Items.ProofSealCharm.Misfire"));
                }
                return true;
            }
            return false;
        }
    }
}
