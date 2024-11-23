using CalamityMod;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Items.Rogue.Extras;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public class CWRPlayer : ModPlayer
    {
        #region Date
        /// <summary>
        /// 是否拥有大修宝典
        /// </summary>
        public bool HasOverhaulTheBibleBook;
        /// <summary>
        /// 圣物的装备等级，这个字段决定了玩家会拥有什么样的弹幕效果
        /// </summary>
        public int TheRelicLuxor = 0;
        /// <summary>
        /// 是否装备制动器
        /// </summary>
        public bool LoadMuzzleBrake;
        /// <summary>
        /// 装备的制动器等级
        /// </summary>
        public int LoadMuzzleBrakeLevel;
        /// <summary>
        /// 应力缩放
        /// </summary>
        public float PressureIncrease;
        /// <summary>
        /// 摄像头位置额外矫正值
        /// </summary>
        public Vector2 OffsetScreenPos;
        /// <summary>
        /// 设置屏幕振动
        /// </summary>
        public float ScreenShakeValue;
        /// <summary>
        /// 是否开启超级合成台
        /// </summary>
        public bool SupertableUIStartBool;
        /// <summary>
        /// 玩家是否坐在大排档塑料椅子之上
        /// </summary>
        public bool InFoodStallChair;
        /// <summary>
        /// 玩家是否装备休谟稳定器
        /// </summary>
        public bool EndlessStabilizerBool;
        /// <summary>
        /// 玩家是否手持鬼妖
        /// </summary>
        public bool HeldMurasamaBool;
        /// <summary>
        /// 玩家是否正在进行终结技
        /// </summary>
        public bool EndSkillEffectStartBool;
        /// <summary>
        /// 该属性用于判断鼠标是否处于接口状态，这个和<see cref="Player.mouseInterface"/>作用相同
        /// </summary>
        public bool uiMouseInterface => Player.mouseInterface;
        /// <summary>
        /// 手持状态
        /// </summary>
        public int HeldStyle;
        /// <summary>
        /// 升龙技充能
        /// </summary>
        public int RisingDragonCharged;
        public int SafeHeldProjIndex;
        public int TETramContrType;
        public int InspectOmigaTime;
        /// <summary>
        /// 挥舞索引，一般被刀具所使用
        /// </summary>
        public int SwingIndex;
        /// <summary>
        /// 是否受伤
        /// </summary>
        public bool OnHit;
        public bool HeldRangedBool;
        public bool HeldFeederGunBool;
        public bool HeldGunBool;
        public bool HeldBowBool;
        public bool NoCanAutomaticCartridgeChange;
        public bool RustyMedallion_Value;
        public int ReceivingPlatformTime;
        public int NoSemberCloneSpanTime;
        /// <summary>
        /// 如果该时间大于0，则玩家不能切换武器，这个值每帧会自动减1
        /// </summary>
        public int DontSwitchWeaponTime;
        public int PlayerIsKreLoadTime;
        /// <summary>
        /// 不能拥有暗影克隆体的时间，这个值每帧会自动减1
        /// </summary>
        public int DontHasSemberDarkMasterCloneTime;

        private Vector2 oldPlayerPositionChange;
        /// <summary>
        /// 一个实时的绘制矫正值
        /// </summary>
        internal Vector2 SpecialDrawPositionOffset;
        /// <summary>
        /// 玩家位置变化量
        /// </summary>
        public Vector2 PlayerPositionChange;

        #region Buff
        public bool TyrantsFuryBuffBool;
        public bool FlintSummonBool;
        public bool HellfireExplosion;
        #endregion

        #endregion

        public override void Initialize() {
            SwingIndex = 0;
            TETramContrType = 0;
            ReceivingPlatformTime = 0;
            Reset();
        }

        public override void ResetEffects() => Reset();

        private void Reset() {
            OffsetScreenPos = Vector2.Zero;
            TheRelicLuxor = 0;
            LoadMuzzleBrakeLevel = 0;
            PressureIncrease = 1;
            HeldStyle = -1;
            OnHit = false;
            HeldGunBool = false;
            HeldBowBool = false;
            FlintSummonBool = false;
            LoadMuzzleBrake = false;
            InFoodStallChair = false;
            HeldRangedBool = false;
            HeldMurasamaBool = false;
            HeldFeederGunBool = false;
            EndlessStabilizerBool = false;
            EndSkillEffectStartBool = false;
            TyrantsFuryBuffBool = false;
            NoCanAutomaticCartridgeChange = false;
            RustyMedallion_Value = false;
            HasOverhaulTheBibleBook = false;
            HellfireExplosion = false;
        }

        public override void OnEnterWorld() {
            CWRHook.CheckHookStatus();

            if (CWRMod.Instance.magicStorage != null) {
                SpwanTextProj.New(Player, () => CWRUtils.Text(CWRLocText.GetTextValue("MS_Config_Text"), Color.IndianRed));
            }
            if (!CWRMod.Suitableversion_improveGame && CWRMod.Instance.improveGame != null) {
                string improvGameText = CWRLocText.GetTextValue("OnEnterWorld_TextContent2");
                SpwanTextProj.New(Player, () => CWRUtils.Text(improvGameText, Color.Red), 210);
                CWRMod.Instance.Logger.Info(improvGameText);
            }
            if (CWRServerConfig.Instance.ForceReplaceResetContent && Player.name == "HoCha113") {
                string text = CWRMod.RItemIndsDict.Count + CWRLocText.GetTextValue("OnEnterWorld_TextContent");
                SpwanTextProj.New(Player, () => CWRUtils.Text(text, Color.GreenYellow), 240);
            }

            ModGanged.Set_MS_Config_recursionCraftingDepth();

            if (CWRServerConfig.Instance.AddExtrasContent) {
                RecipeErrorFullUI.Instance.eyEBool = true;
                if (SupertableUI.Instance != null) {
                    SupertableUI.Instance.loadOrUnLoadZenithWorldAsset = true;
                    SupertableUI.Instance.Active = false;
                }
                if (RecipeUI.Instance != null) {
                    RecipeUI.Instance.index = 0;
                    RecipeUI.Instance.LoadPsreviewItems();
                }
            }

            Gangarus.ZenithWorldAsset();

            oldPlayerPositionChange = oldPlayerPositionChange = Player.position;
        }

        public override void OnHurt(Player.HurtInfo info) {
            OnHit = true;
            if (Main.myPlayer == Player.whoAmI) {
                Projectile.NewProjectile(Player.GetSource_FromThis(), Player.Center, Vector2.Zero, ModContent.ProjectileType<Hit>(), 0, 0, Player.whoAmI);
            }
        }

        public override void CatchFish(FishingAttempt attempt, ref int itemDrop
            , ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (!CWRServerConfig.Instance.ForceReplaceResetContent) {
                return;
            }
            if (!attempt.inHoney && !attempt.inLava && Main.rand.NextBool(500)) {
                itemDrop = ModContent.ItemType<HalibutCannon>();
            }
        }

        public override void PreUpdateMovement() {
            if (ReceivingPlatformTime > 0) {
                Player.gravity = 0;
                if (Player.velocity.Y > 0) {
                    Player.velocity.Y = 0;
                }
                ReceivingPlatformTime--;
            }
        }

        public void SetScope() {
            Item heldItem = Player.ActiveItem();
            if (heldItem.type != ItemID.None && heldItem.CWR().Scope) {
                Player.scope = false;
            }
        }

        public override void PostUpdate() {
            SetScope();

            if (DontSwitchWeaponTime > 0) {
                DontSwitchWeaponTime--;
            }
            if (PlayerIsKreLoadTime > 0) {
                PlayerIsKreLoadTime--;
            }
            if (DontHasSemberDarkMasterCloneTime > 0) {
                DontHasSemberDarkMasterCloneTime--;
            }
            if (InspectOmigaTime > 0) {
                InspectOmigaTime--;
            }

            PlayerPositionChange = oldPlayerPositionChange.To(Player.position);
            oldPlayerPositionChange = Player.position;
        }

        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo) {
            if (drawInfo.shadow != 0f) {
                return;
            }

            Player player = drawInfo.drawPlayer;

            SpecialDrawPositionOffset = Main.OffsetsPlayerHeadgear[player.bodyFrame.Y / player.bodyFrame.Height] * player.Directions;
            SpecialDrawPositionOffset.Y -= 2;

            Item item = player.ActiveItem();
            if (HeldStyle >= 0) {
                player.bodyFrame.Y = player.bodyFrame.Height * HeldStyle;
            }
            if (!player.frozen && !item.IsAir && !player.dead && item.type > ItemID.None) {
                CWRItems cwrItem = item.CWR();
                Texture2D value = null;
                Rectangle frame = new Rectangle(0, 0, 1, 1);
                Vector2 orig = Vector2.Zero;
                Vector2 offsetPos = Vector2.Zero;
                float size = 1;
                int frameindex = 0;

                if (cwrItem.IsBow) {
                    if (player.velocity.Y == 0f && player.velocity.X != 0) {
                        frameindex = (int)(Main.GameUpdateCount / 3 % 5);
                    }
                    value = CWRAsset.Quiver_back_Asset.Value;
                    frame = CWRUtils.GetRec(value, frameindex, 5);
                    if (HeldStyle >= 0) {
                        frame = CWRUtils.GetRec(value, 0, 5);
                    }
                    orig = CWRUtils.GetOrig(value, 5);
                }

                if (item.type == ModContent.ItemType<DarkFrostSolstice>()) {
                    value = CWRAsset.IceGod_back_Asset.Value;
                    frame = CWRUtils.GetRec(value);
                    orig = CWRUtils.GetOrig(value);
                    float sengs = Main.GameUpdateCount * 0.05f;
                    offsetPos = new Vector2(player.direction * 8, -25 + MathF.Sin(sengs) * 5);
                }

                if (value == null) {
                    return;
                }

                SpriteEffects spriteEffects = Player.direction == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Vector2 drawPos = Vector2.Zero;
                drawPos.X = (int)(((int)player.position.X) - Main.screenPosition.X + (player.width / 2) - (9 * player.direction)) - 4f * player.direction + offsetPos.X;
                drawPos.Y = (int)(((int)player.position.Y) - Main.screenPosition.Y + (player.height / 2) + 2f * player.gravDir - 8f * player.gravDir) + offsetPos.Y + player.gfxOffY;
                DrawData howDoIDrawThings = new DrawData(value, drawPos, frame, drawInfo.colorArmorBody, player.bodyRotation, orig, size, spriteEffects, 0) {
                    shader = 0
                };
                drawInfo.DrawDataCache.Add(howDoIDrawThings);
            }
        }

        public override void ModifyScreenPosition() {
            Main.screenPosition += OffsetScreenPos;
            if (ScreenShakeValue > 0f) {
                Main.screenPosition += Main.rand.NextVector2Circular(ScreenShakeValue, ScreenShakeValue);
                ScreenShakeValue = MathHelper.Clamp(ScreenShakeValue - 0.185f, 0f, 20f);
            }
        }
        /// <summary>
        /// 设置屏幕震荡模长
        /// </summary>
        /// <param name="mode"></param>
        public void SetScreenShake(float mode) {
            if (ScreenShakeValue < mode)
                ScreenShakeValue = mode;
        }

        //public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath) {
        //    if (CWRServerConfig.Instance.OpeningOukModification) {
        //        yield return new Item(ModContent.ItemType<PebbleSpear>());
        //        yield return new Item(ModContent.ItemType<PebblePick>());
        //        yield return new Item(ModContent.ItemType<PebbleAxe>());
        //    }
        //    else {//如果不进行开局修改，那么直接放一本宝典在玩家背包中
        //        yield return new Item(ModContent.ItemType<OverhaulTheBibleBook>());
        //    }
        //}

        //public override void ModifyStartingInventory(IReadOnlyDictionary<string, List<Item>> itemsByMod, bool mediumCoreDeath) {
        //    if (!mediumCoreDeath && CWRServerConfig.Instance.OpeningOukModification) {
        //        _ = itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.CopperAxe);
        //        _ = itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.CopperShortsword);
        //        _ = itemsByMod["Terraria"].RemoveAll(item => item.type == ItemID.CopperPickaxe);
        //    }
        //}

        public override void UpdateBadLifeRegen() {
            if (HellfireExplosion) {
                if (Player.lifeRegen > 0) {
                    Player.lifeRegen = 0;
                }
                Player.lifeRegenTime = 0;
                Player.lifeRegen -= 120;
            }
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (HellfireExplosion) {
                damageSource = PlayerDeathReason.ByCustomReason(Player.name + CWRLocText.GetTextValue("HellfireExplosion_DeadLang_Text"));
            }
            return true;
        }

        /// <summary>
        /// 尝试获取玩家当前持有的类型为 T 的投射物实例。
        /// </summary>
        /// <typeparam name="T">要获取的投射物实例的类型。</typeparam>
        /// <param name="result">方法返回时，如果找到且成功获取到类型为 T 的投射物实例，则包含该实例；否则为 null。</param>
        /// <returns>如果找到并成功获取到类型为 T 的投射物实例，则为 true；否则为 false。</returns>
        internal bool TryGetHeldProjInds<T>(out T result) where T : class {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                // 检查投射物是否处于激活状态，是否属于玩家所有，并且是否隐藏
                if (!p.active || p.owner != Player.whoAmI || !p.hide) {
                    continue; // 如果当前投射物不符合条件，则跳过并检查下一个投射物
                }
                if (p.ModProjectile as T != null) {
                    Player.heldProj = p.whoAmI;
                    T instance = p.ModProjectile as T;
                    if (instance != null) {
                        result = instance;
                        return true;
                    }
                }
            }
            result = null;
            return false;
        }
        /// <summary>
        /// 获取玩家所手持的BaseHeldRanged实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持BaseGun或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseHeldRanged(out BaseHeldRanged baseranged) {
            return TryGetHeldProjInds(out baseranged);
        }
        /// <summary>
        /// 获取玩家所手持的<see cref="BaseGun"/>实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持<see cref="BaseGun"/>或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseGun(out BaseGun baseGun) {
            return TryGetHeldProjInds(out baseGun);
        }
        /// <summary>
        /// 获取玩家所手持的<see cref="BaseFeederGun"/>实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持<see cref="BaseFeederGun"/>或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseFeederGun(out BaseFeederGun baseFeederGun) {
            return TryGetHeldProjInds(out baseFeederGun);
        }
        /// <summary>
        /// 获取玩家所手持的<see cref="BaseBow"/>实例
        /// </summary>
        /// <param name="baseGun"></param>
        /// <returns>如果玩家没有手持<see cref="BaseBow"/>或者发生了其他非法情况，返回<see langword="false"/></returns>
        internal bool TryGetInds_BaseBow(out BaseBow baseBow) {
            return TryGetHeldProjInds(out baseBow);
        }
    }
}
