using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
#if DEBUG
using Terraria.Audio;
#endif

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 锈蚀的镣铐：深牢怨灵的掉落饰品。挣断过铐链的人拽不动：
    /// +3 防御、免疫击退；受击后 3 秒"越狱"提速，腕间迸冷粉狱火（纯表现）。
    /// 贴图借原版镣铐，绘制期重染锈粉，与不溺者掉落的水藻绿镣环区分（零新画像素）。
    /// DEBUG 构建下保留野外测试召唤：使用即召深牢怨灵（M0 测试路径，不进正式版）
    /// </summary>
    internal class RustedGaolIrons : GaolModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Shackle;

        /// <summary>锈粉重染色（铁具冷紫锈 + 一点狱火粉）</summary>
        private static readonly Color RustPinkTint = new(196, 148, 164);

        internal const int BreakoutFrames = 180;
        internal const float BreakoutSpeed = 0.12f;

        public override void SetStaticDefaults() {
#if DEBUG
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 12;
#endif
        }

        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.accessory = true;
            Item.defense = 3;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 2);
#if DEBUG
            //测试召唤路径：正式效果不变，仅调试构建可"使用"
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.consumable = false;
#endif
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.noKnockback = true;
            player.GetModPlayer<RustedGaolIronsPlayer>().equipped = true;
        }

#if DEBUG
        public override bool CanUseItem(Player player) {
            //场上无怨灵即可召，测试期不设进度与场景门槛
            return !NPC.AnyNPCs(ModContent.NPCType<DeepGaolWraith>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.4f }, player.position);
                int type = ModContent.NPCType<DeepGaolWraith>();
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.SpawnOnPlayer(player.whoAmI, type);
                }
                else {
                    NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent,
                        number: player.whoAmI, number2: type);
                }
            }
            return true;
        }
#endif

        //==================== 重染绘制（背包与落地都染锈粉）====================

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Texture2D tex = TextureAssets.Item[Item.type]?.Value;
            if (tex == null) {
                return true;
            }
            spriteBatch.Draw(tex, position, frame, drawColor.MultiplyRGB(RustPinkTint), 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            ref float rotation, ref float scale, int whoAmI) {
            Texture2D tex = TextureAssets.Item[Item.type]?.Value;
            if (tex == null) {
                return true;
            }
            spriteBatch.Draw(tex, Item.Center - Main.screenPosition, null,
                lightColor.MultiplyRGB(RustPinkTint), rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 镣铐佩戴状态（每玩家实例字段，禁 static）：受击点燃"越狱"计时，
    /// 提速判定各端本地推演（移动本就归属端权威），腕火纯表现
    /// </summary>
    internal class RustedGaolIronsPlayer : ModPlayer
    {
        public override bool IsLoadingEnabled(Mod mod) => DeepGaolWraithGate.Enabled;

        internal bool equipped;
        internal int breakoutTimer;

        public override void ResetEffects() {
            equipped = false;
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (equipped) {
                breakoutTimer = RustedGaolIrons.BreakoutFrames;
            }
        }

        public override void PostUpdateEquips() {
            if (breakoutTimer <= 0) {
                return;
            }
            breakoutTimer--;
            if (!equipped) {
                return;
            }
            Player.moveSpeed += RustedGaolIrons.BreakoutSpeed;
            //腕间冷粉狱火：越狱者的余怨（表现层，各端只画自己模拟到的）
            if (!Main.dedServ && breakoutTimer % 7 == 0) {
                Vector2 wrist = Player.Center + new Vector2(Player.direction * 10f, 2f);
                PRTLoader.NewParticle<PRT_GaolFireWisp>(wrist,
                    new Vector2(-Player.velocity.X * 0.08f, -Main.rand.NextFloat(0.6f, 1.2f)),
                    Main.rand.NextBool(3) ? DeepGaolWraith.GaolPinkDeep : DeepGaolWraith.GaolPink,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(Main.rand.Next(12, 20));
            }
        }
    }
}
