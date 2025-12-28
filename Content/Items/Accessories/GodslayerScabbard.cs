using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    //弑神者剑鞘饰品主类
    internal class GodslayerScabbard : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "GodslayerScabbard";
        //拔刀值上限120帧即2秒
        public const int MaxDrawCharge = 120;
        //提供的无敌帧时间
        public const int IFrameTime = 120;

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(0, 25, 0, 0);
            Item.rare = CWRID.Rarity_DarkBlue;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<GodslayerScabbardPlayer>().EquipScabbard = true;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                return;
            }
            _ = CreateRecipe().
                AddIngredient(CWRID.Item_CosmiliteBar, 10).
                AddTile(CWRID.Tile_CosmicAnvil).
                Register();
        }
    }

    //玩家效果类负责处理拔刀值积累和无敌帧效果
    internal class GodslayerScabbardPlayer : ModPlayer
    {
        //是否装备剑鞘
        public bool EquipScabbard;
        //当前拔刀值0到MaxDrawCharge
        public int DrawCharge;
        //拔刀值达标标志当拔刀值满时为true
        public bool DrawChargeReady;
        //无敌帧剩余时间
        private int iFrameTimer;

        public override void ResetEffects() {
            EquipScabbard = false;
        }

        public override void PreUpdateMovement() {
            if (!EquipScabbard) {
                //未装备时清空拔刀值
                DrawCharge = 0;
                DrawChargeReady = false;
                return;
            }

            Item heldItem = Player.HeldItem;

            //检查是否手持近战武器且未攻击
            bool isMeleeWeapon = heldItem != null && !heldItem.IsAir
                && (heldItem.DamageType == DamageClass.Melee
                || heldItem.DamageType == CWRRef.GetTrueMeleeDamageClass()
                || heldItem.DamageType == CWRRef.GetTrueMeleeNoSpeedDamageClass());

            bool notAttacking = Player.itemAnimation <= 0 && Player.itemTime <= 0;

            if (isMeleeWeapon && notAttacking) {
                //积累拔刀值
                if (DrawCharge < GodslayerScabbard.MaxDrawCharge) {
                    DrawCharge++;
                    //满值时设置标志并播放音效
                    if (DrawCharge >= GodslayerScabbard.MaxDrawCharge && !DrawChargeReady) {
                        DrawChargeReady = true;
                        //播放充能完成音效
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.5f, Volume = 0.6f }, Player.Center);
                        //生成充能完成粒子效果
                        SpawnChargeReadyEffect();
                    }
                }
            }
            else if (Player.itemAnimation > 0) {
                //正在攻击时如果拔刀值已满则触发效果
                if (DrawChargeReady) {
                    //赋予无敌帧
                    Player.GivePlayerImmuneState(GodslayerScabbard.IFrameTime, true);
                    iFrameTimer = GodslayerScabbard.IFrameTime;
                    //播放拔刀音效
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 0.8f }, Player.Center);
                    //生成拔刀特效
                    SpawnDrawEffect();
                }
                //攻击后清空拔刀值
                DrawCharge = 0;
                DrawChargeReady = false;
            }

            //更新无敌帧计时器
            if (iFrameTimer > 0) {
                iFrameTimer--;
                //无敌帧期间生成保护光环粒子
                if (iFrameTimer % 3 == 0) {
                    SpawnProtectionAura();
                }
            }
        }

        //生成充能完成的粒子效果
        private void SpawnChargeReadyEffect() {
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(4f, 4f);
                int dust = Dust.NewDust(Player.Center, 0, 0, DustID.Electric, velocity.X, velocity.Y, 100, Color.Gold, 1.5f);
                Main.dust[dust].noGravity = true;
            }
        }

        //生成拔刀瞬间的粒子效果
        private void SpawnDrawEffect() {
            Vector2 direction = Player.To(Main.MouseWorld).UnitVector();
            //剑气斩击线
            for (int i = 0; i < 8; i++) {
                Vector2 offset = direction.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(30f, 60f);
                int dust = Dust.NewDust(Player.Center + offset, 0, 0, DustID.Electric, -offset.X * 0.3f, -offset.Y * 0.3f, 120, Color.Cyan, 2f);
                Main.dust[dust].noGravity = true;
            }
            //环状冲击波
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 velocity = angle.ToRotationVector2() * 8f;
                int dust = Dust.NewDust(Player.Center, 0, 0, DustID.MagicMirror, velocity.X, velocity.Y, 100, Color.LightBlue, 1.8f);
                Main.dust[dust].noGravity = true;
            }
        }

        //生成保护光环粒子
        private void SpawnProtectionAura() {
            float progress = 1f - (iFrameTimer / (float)GodslayerScabbard.IFrameTime);
            float radius = 30f + progress * 10f;
            Vector2 offset = Main.rand.NextVector2Circular(radius, radius);
            int dust = Dust.NewDust(Player.Center + offset, 0, 0, DustID.IceTorch, 0, 0, 150, Color.Cyan, 1.2f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = -offset * 0.05f;
        }

        public override void PostUpdate() {
            //绘制拔刀值充能光环
            if (EquipScabbard && DrawCharge > 0) {
                float chargeRatio = DrawCharge / (float)GodslayerScabbard.MaxDrawCharge;
                //每10帧生成一个粒子显示充能进度
                if (Main.GameUpdateCount % 10 == 0 && chargeRatio < 1f) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 offset = angle.ToRotationVector2() * 40f;
                    Color color = Color.Lerp(Color.Gray, Color.Gold, chargeRatio);
                    int dust = Dust.NewDust(Player.Center + offset, 0, 0, DustID.GoldCoin, 0, 0, 100, color, 1f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = Vector2.Zero;
                }
            }
        }
    }
}
