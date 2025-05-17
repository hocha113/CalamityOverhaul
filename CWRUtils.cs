using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs.NormalNPCs;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public static class CWRUtils
    {
        #region System
        public static LocalizedText SafeGetItemName<T>() where T : ModItem => SafeGetItemName(ModContent.ItemType<T>());

        public static LocalizedText SafeGetItemName(int id) {
            ModItem item = ItemLoader.GetItem(id);
            if (item == null || item.Type == ItemID.None) {
                return CWRLocText.GetText("None");
            }
            return item.GetLocalization("DisplayName");
        }

        /// <summary>
        /// 一个额外的跳字方法，向游戏内打印对象的ToString内容
        /// </summary>
        /// <param name="obj"></param>
        public static void Domp(this object obj, Color color = default) {
            if (color == default) {
                color = Color.White;
            }
            if (obj == null) {
                VaultUtils.Text("ERROR Is Null", Color.Red);
                return;
            }
            VaultUtils.Text(obj.ToString(), color);
        }

        /// <summary>
        /// 一个额外的跳字方法，向控制台面板打印对象的ToString内容，并自带换行
        /// </summary>
        /// <param name="obj"></param>
        public static void DompInConsole(this object obj, bool outputLogger = true) {
            if (obj == null) {
                Console.WriteLine("ERROR Is Null");
                return;
            }
            string value = obj.ToString();
            Console.WriteLine(value);
            if (outputLogger) {
                CWRMod.Instance.Logger.Info(value);
            }
        }

        ///// <summary>
        ///// 将 Item 数组的信息写入指定路径的文件中
        ///// </summary>
        ///// <param name="items">要导出的 Item 数组</param>
        ///// <param name="path">写入文件的路径，默认为 "D:\\Mod_Resource\\input.cs"</param>
        //public static void ExportItemTypesToFile(Item[] items, string path = "D:\\Mod_Resource\\input.cs") {
        //    try {
        //        int columnIndex = 0;
        //        using StreamWriter sw = new(path);
        //        sw.Write("string[] fullItems = new string[] {");
        //        foreach (Item item in items) {
        //            columnIndex++;
        //            // 根据是否有 ModItem 决定写入的内容
        //            string itemInfo = item.ModItem == null ? $"\"{item.type}\"" : $"\"{item.ModItem.FullName}\"";
        //            sw.Write(itemInfo);
        //            sw.Write(", ");
        //            // 每行最多写入9个元素，然后换行
        //            if (columnIndex >= 9) {
        //                sw.WriteLine();
        //                columnIndex = 0;
        //            }
        //        }
        //        sw.Write("};");
        //    } catch (UnauthorizedAccessException) {
        //        CWRMod.Instance.Logger.Info($"UnauthorizedAccessException: 无法访问文件路径 '{path}'. 权限不足");
        //    } catch (DirectoryNotFoundException) {
        //        CWRMod.Instance.Logger.Info($"DirectoryNotFoundException: 文件路径 '{path}' 中的目录不存在");
        //    } catch (PathTooLongException) {
        //        CWRMod.Instance.Logger.Info($"PathTooLongException: 文件路径 '{path}' 太长");
        //    } catch (IOException) {
        //        CWRMod.Instance.Logger.Info($"IOException: 无法打开文件 '{path}' 进行写入");
        //    } catch (Exception e) {
        //        CWRMod.Instance.Logger.Info($"An error occurred: {e.Message}");
        //    }
        //}

        public static Player InPosFindPlayer(Vector2 position, int maxRange = 3000) {
            foreach (Player player in Main.player) {
                if (!player.Alives()) {
                    continue;
                }
                if (maxRange == -1) {
                    return player;
                }
                int distance = (int)player.position.To(position).Length();
                if (distance < maxRange) {
                    return player;
                }
            }
            return null;
        }

        public static Player TileFindPlayer(int i, int j) {
            return InPosFindPlayer(new Vector2(i, j) * 16, 9999);
        }

        public static Chest FindNearestChest(int x, int y) {
            int distance = 99999;
            Chest nearestChest = null;

            for (int c = 0; c < Main.chest.Length; c++) {
                Chest currentChest = Main.chest[c];
                if (currentChest != null) {
                    int length = (int)Math.Sqrt(Math.Pow(x - currentChest.x, 2) + Math.Pow(y - currentChest.y, 2));
                    if (length < distance) {
                        nearestChest = currentChest;
                        distance = length;
                    }
                }
            }
            return nearestChest;
        }

        public static void AddItem(this Chest chest, Item item) {
            Item infoItem = item.Clone();
            for (int i = 0; i < chest.item.Length; i++) {
                if (chest.item[i] == null) {
                    chest.item[i] = new Item();
                }
                if (chest.item[i].type == ItemID.None) {
                    chest.item[i] = infoItem;
                    return;
                }
                if (chest.item[i].type == item.type) {
                    chest.item[i].stack += infoItem.stack;
                    return;
                }
            }
        }

        public static Color[] GetColorDate(Texture2D tex) {
            Color[] colors = new Color[tex.Width * tex.Height];
            tex.GetData(colors);
            List<Color> nonTransparentColors = [];
            foreach (Color color in colors) {
                if ((color.A > 0 || color.R > 0 || color.G > 0 || color.B > 0) && color != Color.White && color != Color.Black) {
                    nonTransparentColors.Add(color);
                }
            }
            return nonTransparentColors.ToArray();
        }

        #endregion

        #region AIUtils

        #region 工具部分

        public const float atoR = MathHelper.Pi / 180;

        public static float AtoR(this float num) => num * atoR;

        public static float RtoA(this float num) => num / atoR;

        public static void SetArrowRot(int proj) => Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        public static void SetArrowRot(this Projectile proj) => proj.rotation = proj.velocity.ToRotation() + MathHelper.PiOver2;

        public static void UpdateOldPosCache(this Projectile projectile, bool useCenter = true, bool addVelocity = true) {
            for (int i = 0; i < projectile.oldPos.Length - 1; i++)
                projectile.oldPos[i] = projectile.oldPos[i + 1];
            projectile.oldPos[^1] = (useCenter ? projectile.Center : projectile.position) + (addVelocity ? projectile.velocity : Vector2.Zero);
        }

        public static void InitOldPosCache(this Projectile projectile, int trailCount, bool useCenter = true) {
            projectile.oldPos = new Vector2[trailCount];

            for (int i = 0; i < trailCount; i++) {
                if (useCenter)
                    projectile.oldPos[i] = projectile.Center;
                else
                    projectile.oldPos[i] = projectile.position;
            }
        }

        /// <summary>
        /// 如果对象是一个蠕虫体节，那么按机会分母的倒数返回布尔值，如果输入5，那么会有4/5的概率返回<see langword="true"/>
        /// </summary>
        /// <param name="targetNPCType"></param>
        /// <param name="randomCount"></param>
        /// <returns></returns>
        public static bool FromWormBodysRandomSet(int targetNPCType, int randomCount) {
            return CWRLoad.WormBodys.Contains(targetNPCType) && !Main.rand.NextBool(randomCount);
        }
        /// <summary>
        /// 如果对象是一个蠕虫体节，那么按机会分母的倒数返回布尔值，如果输入5，那么会有4/5的概率返回<see langword="true"/>
        /// </summary>
        /// <param name="targetNPCType"></param>
        /// <param name="randomCount"></param>
        /// <returns></returns>
        public static bool FromWormBodysRandomSet(this NPC npc, int randomCount) => FromWormBodysRandomSet(npc.type, randomCount);

        /// <summary>
        /// 这个NPC是否属于一个蠕虫体节
        /// </summary>
        /// <param name="npc"></param>
        /// <returns></returns>
        public static bool IsWormBody(this NPC npc) => CWRLoad.WormBodys.Contains(npc.type);

        /// <summary>
        /// 世界实体坐标转物块坐标
        /// </summary>
        /// <param name="wePos"></param>
        /// <returns></returns>
        public static Vector2 WEPosToTilePos(Vector2 wePos) {
            int tilePosX = (int)(wePos.X / 16f);
            int tilePosY = (int)(wePos.Y / 16f);
            Vector2 tilePos = new(tilePosX, tilePosY);
            tilePos = PTransgressionTile(tilePos);
            return tilePos;
        }

        /// <summary>
        /// 物块坐标转世界实体坐标
        /// </summary>
        /// <param name="tilePos"></param>
        /// <returns></returns>
        public static Vector2 TilePosToWEPos(Vector2 tilePos) {
            float wePosX = (float)(tilePos.X * 16f);
            float wePosY = (float)(tilePos.Y * 16f);

            return new Vector2(wePosX, wePosY);
        }

        /// <summary>
        /// 计算一个渐进速度值
        /// </summary>
        /// <param name="thisCenter">本体位置</param>
        /// <param name="targetCenter">目标位置</param>
        /// <param name="speed">速度</param>
        /// <param name="shutdownDistance">停摆范围</param>
        /// <returns></returns>
        public static float AsymptoticVelocity(Vector2 thisCenter, Vector2 targetCenter, float speed, float shutdownDistance) {
            Vector2 toMou = targetCenter - thisCenter;
            float thisSpeed = toMou.LengthSquared() > shutdownDistance * shutdownDistance ? speed : MathHelper.Min(speed, toMou.Length());
            return thisSpeed;
        }

        /// <summary>
        /// 根据索引返回在player域中的player实例，同时考虑合法性校验
        /// </summary>
        /// <returns>当获取值非法时将返回 <see cref="null"/> </returns>
        public static Player GetPlayerInstance(int playerIndex) {
            if (playerIndex.ValidateIndex(Main.player)) {
                Player player = Main.player[playerIndex];

                return player.Alives() ? player : null;
            }
            else {
                return null;
            }
        }

        /// <summary>
        /// 根据索引返回在npc域中的npc实例，同时考虑合法性校验
        /// </summary>
        /// <returns>当获取值非法时将返回 <see cref="null"/> </returns>
        public static NPC GetNPCInstance(int npcIndex) {
            if (npcIndex.ValidateIndex(Main.npc)) {
                NPC npc = Main.npc[npcIndex];

                return npc.Alives() ? npc : null;
            }
            else {
                return null;
            }
        }

        /// <summary>
        /// 根据索引返回在projectile域中的Projectile实例，同时考虑合法性校验
        /// </summary>
        /// <returns>当获取值非法时将返回 <see cref="null"/> </returns>
        public static Projectile GetProjectileInstance(int projectileIndex) {
            if (projectileIndex.ValidateIndex(Main.projectile)) {
                Projectile proj = Main.projectile[projectileIndex];
                return proj.Alives() ? proj : null;
            }
            else {
                return null;
            }
        }

        /// <summary>
        /// 获取鞭类弹幕的路径点集
        /// </summary>
        public static List<Vector2> GetWhipControlPoints(this Projectile projectile) {
            List<Vector2> list = [];
            Projectile.FillWhipControlPoints(projectile, list);
            return list;
        }

        #endregion

        #region 行为部分

        public static void DigByTile(this Projectile projectile, SoundStyle soundStyle = default) {
            Collision.HitTiles(projectile.position, projectile.velocity, projectile.width, projectile.height);
            SoundEngine.PlaySound(soundStyle == default ? SoundID.Dig : soundStyle, projectile.position);
        }

        public static void SpawnTrailDust(this Projectile Projectile, int type, float velocityMult
            , int Alpha = 0, Color newColor = default, float Scale = 1f, bool noGravity = true) {
            if (VaultUtils.isServer) {
                return;
            }

            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width
                , Projectile.height, type, Alpha: Alpha, newColor: newColor, Scale: Scale);
            dust.noGravity = noGravity;
            dust.velocity = -Projectile.velocity * velocityMult;
        }

        public static void WulfrumAmplifierAI(NPC npc, float maxrg = 495f, int maxchargeTime = 600) {
            List<int> SuperchargableEnemies = [
                ModContent.NPCType<WulfrumDrone>(),
                ModContent.NPCType<WulfrumGyrator>(),
                ModContent.NPCType<WulfrumHovercraft>(),
                ModContent.NPCType<WulfrumRover>()
            ];

            npc.ai[1] = (int)MathHelper.Lerp(npc.ai[1], maxrg, 0.1f);

            if (Main.rand.NextBool(4)) {
                float dustCount = MathHelper.TwoPi * npc.ai[1] / 8f;
                for (int i = 0; i < dustCount; i++) {
                    float angle = MathHelper.TwoPi * i / dustCount;
                    Dust dust = Dust.NewDustPerfect(npc.Center, 229);
                    dust.position = npc.Center + angle.ToRotationVector2() * npc.ai[1];
                    dust.scale = 0.7f;
                    dust.noGravity = true;
                    dust.velocity = npc.velocity;
                }
            }

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npcAtIndex = Main.npc[i];
                if (!npcAtIndex.active)
                    continue;
                if (!SuperchargableEnemies.Contains(npcAtIndex.type) && npcAtIndex.type != ModContent.NPCType<WulfrumRover>())
                    continue;
                if (npcAtIndex.ai[3] > 0f)
                    continue;
                if (npc.Distance(npcAtIndex.Center) > npc.ai[1])
                    continue;

                npcAtIndex.ai[3] = maxchargeTime;
                npcAtIndex.netUpdate = true;

                if (Main.dedServ)
                    continue;

                for (int j = 0; j < 10; j++) {
                    Dust.NewDust(npcAtIndex.position, npcAtIndex.width, npcAtIndex.height, DustID.Electric);
                }
            }
        }

        public static void EntityToRot(this NPC entity, float toRot, float rotSpeed) => entity.rotation = ToRot(entity.rotation, toRot, rotSpeed);

        public static float ToRot(float setRot, float toRot, float rotSpeed) {
            setRot = MathHelper.WrapAngle(setRot);
            float diff = MathHelper.WrapAngle(toRot - setRot);
            return setRot + MathHelper.Clamp(diff, -rotSpeed, rotSpeed);
        }

        /// <summary>
        /// 处理实体的旋转行为
        /// </summary>
        public static void EntityToRot(this Projectile entity, float ToRot, float rotSpeed) {
            //entity.rotation = MathHelper.SmoothStep(entity.rotation, ToRot, rotSpeed);

            // 将角度限制在 -π 到 π 的范围内
            entity.rotation = MathHelper.WrapAngle(entity.rotation);

            // 计算差异角度
            float diff = MathHelper.WrapAngle(ToRot - entity.rotation);

            // 选择修改幅度小的方向进行旋转
            if (Math.Abs(diff) < MathHelper.Pi) {
                entity.rotation += diff * rotSpeed;
            }
            else {
                entity.rotation -= MathHelper.WrapAngle(-diff) * rotSpeed;
            }
        }

        #endregion

        #endregion

        #region GameUtils
        /// <summary>
        /// 是否处于入侵期间
        /// </summary>
        public static bool Invasion => Main.invasionType > 0 || Main.pumpkinMoon
                || Main.snowMoon || DD2Event.Ongoing || AcidRainEvent.AcidRainEventIsOngoing;
        /// <summary>
        /// 是否处于愚人节
        /// </summary>
        public static bool IsAprilFoolsDay => DateTime.Now.Month == 4 && DateTime.Now.Day == 1;

        public static bool IsTool(this Item item) => item.pick > 0 || item.axe > 0 || item.hammer > 0;

        public static void GiveMeleeType(this Item item, bool isGiveTrueMelee = false) => item.DamageType = GiveMeleeType(isGiveTrueMelee);

        public static DamageClass GiveMeleeType(bool isGiveTrueMelee = false) => isGiveTrueMelee ? ModContent.GetInstance<TrueMeleeDamageClass>() : DamageClass.Melee;

        /// <summary>
        /// 设置玩家鼠标指向物块的信息状态
        /// </summary>
        /// <param name="player"></param>
        /// <param name="itemID"></param>
        public static void SetMouseOverByTile(this Player player, int itemID = ItemID.None) {
            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            if (itemID > 0) {
                player.cursorItemIconID = itemID;//当玩家鼠标悬停在物块之上时，显示该物品的材质
            }
        }

        /// <summary>
        /// 目标弹药是否应该判定为一个木箭
        /// </summary>
        /// <param name="player"></param>
        /// <param name="ammoType"></param>
        /// <returns></returns>
        public static bool IsWoodenAmmo(this Player player, int ammoType) {
            if (player.hasMoltenQuiver && ammoType == ProjectileID.FireArrow) {
                return true;
            }
            if (ammoType == ProjectileID.WoodenArrowFriendly) {
                return true;
            }

            return false;
        }

        public static bool AdrenalineMode(this Player player) => player.Calamity().adrenalineModeActive;

        public static void SetItemLegendContentTops(ref List<TooltipLine> tooltips, string itemKey) {
            TooltipLine legendtops = tooltips.FirstOrDefault((TooltipLine x) => x.Text.Contains("[legend]") && x.Mod == "Terraria");
            if (legendtops != null) {
                KeyboardState state = Keyboard.GetState();
                if ((state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift))) {
                    legendtops.Text = Language.GetTextValue($"Mods.CalamityOverhaul.Items.{itemKey}.Legend");
                    legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                }
                else {
                    legendtops.Text = CWRLocText.GetTextValue("Item_LegendOnMouseLang");
                    legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.Gold, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                }
            }
        }

        public static int GetProjectileHasNum(int targetProjType, int ownerIndex = -1) {
            int num = 0;
            foreach (var proj in Main.ActiveProjectiles) {
                if (ownerIndex >= 0 && ownerIndex != proj.owner) {
                    continue;
                }
                if (proj.type == targetProjType) {
                    num++;
                }
            }
            return num;
        }

        public static int GetProjectileHasNum(this Player player, int targetProjType) => GetProjectileHasNum(targetProjType, player.whoAmI);

        public static void ModifyLegendWeaponDamageFunc(Player player, Item item, int GetOnDamage, int GetStartDamage, ref StatModifier damage) {
            float oldMultiplicative = damage.Multiplicative;
            damage *= GetOnDamage / (float)GetStartDamage;
            damage /= oldMultiplicative;
            //首先，因为SD的运行优先级并不可靠，有的模组的修改在SD之后运行，比如炼狱模式，这个基础伤害缩放保证一些情况不会发生
            damage *= GetStartDamage / (float)item.damage;
            damage *= item.GetPrefixState().damageMult;
        }

        public static void ModifyLegendWeaponKnockbackFunc(Player player, Item item, float GetOnKnockback, float GetStartKnockback, ref StatModifier Knockback) {
            Knockback *= GetOnKnockback / (float)GetStartKnockback;
            //首先，因为SD的运行优先级并不可靠，有的模组的修改在SD之后运行，比如炼狱模式，这个基础击退缩放保证一些情况不会发生
            Knockback *= GetStartKnockback / item.knockBack;
            Knockback *= item.GetPrefixState().knockbackMult;
        }

        public static NPC FindNPCFromeType(int type) {
            NPC npc = null;
            foreach (var n in Main.npc) {
                if (!n.active) {
                    continue;
                }
                if (n.type == type) {
                    npc = n;
                }
            }
            return npc;
        }

        public static Recipe AddBlockingSynthesisEvent(this Recipe recipe) =>
             recipe.AddConsumeIngredientCallback((Recipe recipe, int type, ref int amount, bool isDecrafting) => { amount = 0; })
            .AddOnCraftCallback(CWRRecipes.SpawnAction);

        /// <summary>
        /// 用于将一个武器设置为手持刀剑类，这个函数若要正确设置物品的近战属性，需要让其在初始化函数中最后调用
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <param name="dontStopOrigShoot"></param>
        public static void SetKnifeHeld<T>(this Item item, bool dontStopOrigShoot = false) where T : ModProjectile {
            if (item.shoot == ProjectileID.None || !item.noUseGraphic
                || item.DamageType == ModContent.GetInstance<TrueMeleeDamageClass>()
                || item.DamageType == ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>()) {
                CWRItems.ItemMeleePrefixDic[item.type] = true;
            }
            item.noMelee = true;
            item.noUseGraphic = true;
            item.CWR().IsShootCountCorlUse = true;
            item.CWR().IsHeldSwing = true;
            item.CWR().IsHeldSwingDontStopOrigShoot = dontStopOrigShoot;
            item.CWR().SetHeldSwingOrigShootID = item.shoot;//提前存储一下射弹值
            item.CWR().WeaponInSetKnifeHeld = true;
            item.shoot = ModContent.ProjectileType<T>();
            if (item.shootSpeed <= 0) {
                //不能让速度模场为0，这会让向量失去方向的性质，从而影响一些刀剑的方向判定
                item.shootSpeed = 0.0001f;
            }
        }

        /// <summary>
        /// 快捷的将一个物品实例设置为手持对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        public static void SetHeldProj<T>(this Item item) where T : ModProjectile {
            item.noUseGraphic = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<T>();
        }

        /// <summary>
        /// 快捷的将一个物品实例设置为手持对象
        /// </summary>
        public static void SetHeldProj(this Item item, int id) {
            item.noUseGraphic = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = id;
        }

        /// <summary>
        /// 快捷的将一个物品实例设置为填装枪类实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        public static void SetCartridgeGun<T>(this Item item, int ammoCapacity = 1) where T : ModProjectile {
            item.SetHeldProj<T>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = ammoCapacity;
        }

        /// <summary>
        /// 复制一个物品的属性
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        public static void SetItemCopySD<T>(this Item item) where T : ModItem => item.CloneDefaults(ModContent.ItemType<T>());

        /// <summary>
        /// 弹药是否应该被消耗，先行判断武器自带的消耗抵消比率，再在该基础上计算玩家的额外消耗抵消比率，比如弹药箱加成或者药水
        /// </summary>
        /// <param name="player"></param>
        /// <param name="weapon"></param>
        /// <returns></returns>
        public static bool CanUseAmmoInWeaponShoot(this Player player, Item weapon) {
            bool result = true;
            if (weapon.type != ItemID.None) {
                Item[] magazineContents = weapon.CWR().MagazineContents;
                if (magazineContents.Length <= 0) {
                    return true;
                }
                result = ItemLoader.CanConsumeAmmo(weapon, magazineContents[0], player);
                if (player.IsRangedAmmoFreeThisShot(magazineContents[0])) {
                    result = false;
                }
            }
            return result;
        }

        public static bool IsRangedAmmoFreeThisShot(this Player player, Item ammo) {
            bool flag2 = false;
            if (player.magicQuiver && ammo.ammo == AmmoID.Arrow && Main.rand.NextBool(5)) {
                flag2 = true;
            }

            if (player.ammoBox && Main.rand.NextBool(5)) {
                flag2 = true;
            }

            if (player.ammoPotion && Main.rand.NextBool(5)) {
                flag2 = true;
            }

            if (player.huntressAmmoCost90 && Main.rand.NextBool(10)) {
                flag2 = true;
            }

            if (player.chloroAmmoCost80 && Main.rand.NextBool(5)) {
                flag2 = true;
            }

            if (player.ammoCost80 && Main.rand.NextBool(5)) {
                flag2 = true;
            }

            if (player.ammoCost75 && Main.rand.NextBool(4)) {
                flag2 = true;
            }

            return flag2;
        }

        /// <summary>
        /// 赋予玩家无敌状态，这个函数与<see cref="Player.SetImmuneTimeForAllTypes(int)"/>类似
        /// </summary>
        /// <param name="player">要赋予无敌状态的玩家</param>
        /// <param name="blink">是否允许玩家在无敌状态下闪烁默认为 false</param>
        public static void GivePlayerImmuneState(this Player player, int time, bool blink = false) {
            player.immuneNoBlink = !blink;
            player.immune = true;
            player.immuneTime = time;
            for (int k = 0; k < player.hurtCooldowns.Length; k++) {
                player.hurtCooldowns[k] = player.immuneTime;
            }
        }

        /// <summary>
        /// 将热键绑定的提示信息添加到 TooltipLine 列表中
        /// </summary>
        /// <param name="tooltips">要添加提示信息的 TooltipLine 列表</param>
        /// <param name="mhk">Mod 热键绑定</param>
        /// <param name="keyName">替换的关键字，默认为 "[KEY]"</param>
        /// <param name="modName">Mod 的名称，默认为 "Terraria"</param>
        public static void SetHotkey(this List<TooltipLine> tooltips, ModKeybind mhk, string keyName = "[KEY]", string modName = "") {
            if (Main.dedServ || mhk is null) {
                return;
            }

            string finalKey = mhk.TooltipHotkeyString();
            tooltips.ReplaceTooltip(keyName, finalKey, modName);
        }

        /// <summary>
        /// 替换 TooltipLine 列表中指定关键字的提示信息
        /// </summary>
        /// <param name="tooltips">要进行替换的 TooltipLine 列表</param>
        /// <param name="targetKeyStr">要替换的关键字</param>
        /// <param name="contentStr">替换后的内容</param>
        /// <param name="modName">Mod 的名称，默认为 "Terraria"</param>
        public static void ReplaceTooltip(this List<TooltipLine> tooltips, string targetKeyStr, string contentStr, string modName = "") {
            foreach (var line in tooltips) {
                if (modName != "" && line.Mod != modName) {
                    continue;
                }
                if (!line.Text.Contains(targetKeyStr)) {
                    continue;
                }
                line.Text = line.Text.Replace(targetKeyStr, contentStr);
            }
        }

        /// <summary>
        /// 将文本拆分为多行，并为每行分别添加颜色代码。
        /// </summary>
        /// <param name="textContent">输入的文本内容，支持换行符</param>
        /// <param name="color">颜色对象</param>
        /// <returns>格式化后的多行带颜色文本</returns>
        public static string FormatColorTextMultiLine(string textContent, Color color) {
            if (string.IsNullOrEmpty(textContent))
                return string.Empty;

            // 将颜色转换为 16 进制字符串
            string hexColor = $"{color.R:X2}{color.G:X2}{color.B:X2}";

            // 按换行符分割文本
            string[] lines = textContent.Split('\n');

            // 对每一行添加颜色代码
            for (int i = 0; i < lines.Length; i++) {
                lines[i] = $"[c/{hexColor}:{lines[i]}]";
            }

            // 使用换行符重新组合
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 快速修改一个物品的简介文本，从<see cref="CWRLocText"/>中拉取资源
        /// </summary>
        public static void OnModifyTooltips(Mod mod, List<TooltipLine> tooltips, LocalizedText value) {
            List<TooltipLine> newTooltips = new(tooltips);
            List<TooltipLine> overTooltips = [];
            List<TooltipLine> prefixTooltips = [];
            foreach (TooltipLine line in tooltips.ToList()) {//复制 tooltips 集合，以便在遍历时修改
                for (int i = 0; i < 9; i++) {
                    if (line.Name == "Tooltip" + i) {
                        line.Hide();
                    }
                }
                if (line.Name == "CalamityDonor" || line.Name == "CalamityDev") {
                    overTooltips.Add(line.Clone());
                    line.Hide();
                }
                if (line.Name.Contains("Prefix")) {
                    prefixTooltips.Add(line.Clone());
                    line.Hide();
                }
            }

            TooltipLine newLine = new(mod, "CWRText", value.Value);
            newTooltips.Add(newLine);
            newTooltips.AddRange(overTooltips);
            tooltips.Clear(); // 清空原 tooltips 集合
            tooltips.AddRange(newTooltips); // 添加修改后的 newTooltips 集合
            tooltips.AddRange(prefixTooltips);
        }

        public static TooltipLine Clone(this TooltipLine tooltipLine) {
            Mod mod = CWRMod.Instance;
            foreach (Mod mod1 in ModLoader.Mods) {
                if (mod1.Name == tooltipLine.Mod) {
                    mod = mod1;
                }
            }
            TooltipLine line = new TooltipLine(mod, tooltipLine.Name, tooltipLine.Text) {
                OverrideColor = tooltipLine.OverrideColor,
                IsModifier = tooltipLine.IsModifier,
                IsModifierBad = tooltipLine.IsModifierBad
            };
            return line;
        }

        public static CWRNpc CWR(this NPC npc) {
            return npc.GetGlobalNPC<CWRNpc>();
        }

        public static CWRPlayer CWR(this Player player) {
            return player.GetModPlayer<CWRPlayer>();
        }

        public static CWRItems CWR(this Item item) {
            if (item.type == ItemID.None) {
                VaultUtils.Text("ERROR!发生了一次空传递，该物品为None!", Color.Red);
                CWRMod.Instance.Logger.Info("ERROR!发生了一次空传递，该物品为None!");
                return null;
            }
            return item.GetGlobalItem<CWRItems>();
        }

        public static CWRProjectile CWR(this Projectile projectile) {
            return projectile.GetGlobalProjectile<CWRProjectile>();
        }

        public static void initialize(this Item item) {
            if (item.CWR().ai == null) {
                item.CWR().ai = [0, 0, 0];
            }
        }

        #endregion

        #region MathUtils

        public static Vector2 randVr(int min, int max) {
            return Main.rand.NextVector2Unit() * Main.rand.Next(min, max);
        }

        public static Vector2 randVr(int max) {
            return Main.rand.NextVector2Unit() * Main.rand.Next(0, max);
        }

        public static Vector2 randVr(float min, float max) {
            return Main.rand.NextVector2Unit() * Main.rand.NextFloat(min, max);
        }

        public static Vector2 randVr(float max) {
            return Main.rand.NextVector2Unit() * Main.rand.NextFloat(0, max);
        }

        public static float GetCorrectRadian(float minusRadian) {
            return minusRadian < 0 ? (MathHelper.TwoPi + minusRadian) / MathHelper.TwoPi : minusRadian / MathHelper.TwoPi;
        }

        public static T[] FastUnion<T>(this T[] front, T[] back) {
            T[] combined = new T[front.Length + back.Length];

            Array.Copy(front, combined, front.Length);
            Array.Copy(back, 0, combined, front.Length, back.Length);

            return combined;
        }

        /// <summary>
        /// 生成一组不重复的随机数集合，数字的数量不能大于取值范围
        /// </summary>
        /// <param name="count">集合元素数量</param>
        /// <param name="minValue">元素最小值</param>
        /// <param name="maxValue">元素最大值</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static List<int> GenerateUniqueNumbers(int count, int minValue, int maxValue) {
            if (count > maxValue - minValue + 1) {
                throw new ArgumentException("Count of unique numbers cannot be greater than the range of values.");
            }

            List<int> uniqueNumbers = [];
            HashSet<int> usedNumbers = [];

            for (int i = minValue; i <= maxValue; i++) {
                _ = usedNumbers.Add(i);
            }

            for (int i = 0; i < count; i++) {
                int randomIndex = Main.rand.Next(usedNumbers.Count);
                int randomNumber = usedNumbers.ElementAt(randomIndex);
                _ = usedNumbers.Remove(randomNumber);
                uniqueNumbers.Add(randomNumber);
            }

            return uniqueNumbers;
        }

        public static float RotTowards(this float curAngle, float targetAngle, float maxChange) {
            curAngle = MathHelper.WrapAngle(curAngle);
            targetAngle = MathHelper.WrapAngle(targetAngle);
            if (curAngle < targetAngle) {
                if (targetAngle - curAngle > (float)Math.PI) {
                    curAngle += (float)Math.PI * 2f;
                }
            }
            else if (curAngle - targetAngle > (float)Math.PI) {
                curAngle -= (float)Math.PI * 2f;
            }

            curAngle += MathHelper.Clamp(targetAngle - curAngle, 0f - maxChange, maxChange);
            return MathHelper.WrapAngle(curAngle);
        }

        /// <summary>
        /// 色彩混合
        /// </summary>
        public static Color RecombinationColor(params (Color color, float weight)[] colorWeightPairs) {
            Vector4 result = Vector4.Zero;

            for (int i = 0; i < colorWeightPairs.Length; i++) {
                result += colorWeightPairs[i].color.ToVector4() * colorWeightPairs[i].weight;
            }

            return new Color(result);
        }

        /// <summary>
        /// 获取一个随机方向的向量
        /// </summary>
        /// <param name="startAngle">开始角度,输入角度单位的值</param>
        /// <param name="targetAngle">目标角度,输入角度单位的值</param>
        /// <param name="ModeLength">返回的向量的长度</param>
        /// <returns></returns>
        public static Vector2 GetRandomVevtor(float startAngle, float targetAngle, float ModeLength) {
            float angularSeparation = targetAngle - startAngle;
            float randomPosx = ((angularSeparation * Main.rand.NextFloat()) + startAngle) * (MathHelper.Pi / 180);
            float cosValue = MathF.Cos(randomPosx);
            float sinValue = MathF.Sin(randomPosx);

            return new Vector2(cosValue, sinValue) * ModeLength;
        }

        /// <summary>
        /// 检测索引的合法性
        /// </summary>
        /// <returns>合法将返回 <see cref="true"/></returns>
        public static bool ValidateIndex(this int index, Array array) {
            return index >= 0 && index < array.Length;
        }

        /// <summary>
        /// 委托，用于定义曲线的缓动函数
        /// </summary>
        /// <param name="progress">进度，范围在0到1之间。</param>
        /// <param name="polynomialDegree">如果缓动模式是多项式，则此为多项式的阶数。</param>
        /// <returns>给定进度下的曲线值。</returns>
        public delegate float CurveEasingFunction(float progress, int polynomialDegree);

        /// <summary>
        /// 表示分段函数的一部分
        /// </summary>
        public struct AnimationCurvePart
        {
            /// <summary>
            /// 用于该段的缓动函数类型
            /// </summary>
            public CurveEasingFunction CurveEasingFunction { get; }

            /// <summary>
            /// 段在动画中的起始位置
            /// </summary>
            public float StartX { get; internal set; }

            /// <summary>
            /// 段的起始高度
            /// </summary>
            public float StartHeight { get; }

            /// <summary>
            /// 段内的高度变化量设为0时段为平直线通常在段末应用，但sinebump缓动类型在曲线顶点应用
            /// </summary>
            public float HeightShift { get; }

            /// <summary>
            /// 如果选择的缓动模式是多项式，则此为多项式的阶数
            /// </summary>
            public int PolynomialDegree { get; }

            /// <summary>
            /// 在考虑高度变化后的段结束高度
            /// </summary>
            public float EndHeight => StartHeight + HeightShift;

            public struct StartData
            {
                public float startX;
                public float startHeight;
                public float heightShift;
                public int degree = 1;

                public StartData() { }
            }

            public AnimationCurvePart(CurveEasingFunction curveEasingFunction
                , float startX, float startHeight, float heightShift, int degree = 1) {
                CurveEasingFunction = curveEasingFunction;
                StartX = startX;
                StartHeight = startHeight;
                HeightShift = heightShift;
                PolynomialDegree = degree;
            }

            public AnimationCurvePart(CurveEasingFunction curveEasingFunction, StartData starData) {
                CurveEasingFunction = curveEasingFunction;
                StartX = starData.startX;
                StartHeight = starData.startHeight;
                HeightShift = starData.heightShift;
                PolynomialDegree = starData.degree;
            }
        }

        /// <summary>
        /// 获取自定义分段函数在任意给定X值的高度，使您可以轻松创建复杂的动画曲线。X值自动限定在0到1之间，但函数高度可以超出0到1的范围。
        /// </summary>
        /// <param name="progress">曲线进度。自动限定在0到1之间。</param>
        /// <param name="segments">构成完整动画曲线的曲线段数组。</param>
        /// <returns>给定X值的函数高度。</returns>
        public static float EvaluateCurve(float progress, params AnimationCurvePart[] segments) {
            if (segments.Length == 0) {
                return 0f;
            }

            if (segments[0].StartX != 0) {
                segments[0].StartX = 0;
            }

            progress = MathHelper.Clamp(progress, 0f, 1f); // 限定进度在0到1之间
            float height = 0f;

            for (int i = 0; i < segments.Length; i++) {
                AnimationCurvePart segment = segments[i];
                float startX = segment.StartX;
                float endX = (i < segments.Length - 1) ? segments[i + 1].StartX : 1f;

                if (progress < startX) {
                    continue;
                }


                if (progress >= endX) {
                    continue;
                }

                float segmentProgress = (progress - startX) / (endX - startX); // 计算段内进度
                height = segment.StartHeight + segment.CurveEasingFunction(segmentProgress, segment.PolynomialDegree) * segment.HeightShift;
                break;
            }
            return height;
        }
        #endregion

        #region DrawUtils

        /// <summary>
        /// 安全的获取对应实例的图像资源
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static Texture2D T2DValue(this Projectile p, bool loadCeahk = true) {
            if (Main.dedServ) {
                return null;
            }
            if (p.type < 0 || p.type >= TextureAssets.Projectile.Length) {
                return null;
            }
            if (loadCeahk && p.ModProjectile == null) {
                Main.instance.LoadProjectile(p.type);
            }

            return TextureAssets.Projectile[p.type].Value;
        }

        /// <summary>
        /// 安全的获取对应实例的图像资源
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static Texture2D T2DValue(this Item i, bool loadCeahk = true) {
            if (Main.dedServ) {
                return null;
            }
            if (i.type < ItemID.None || i.type >= TextureAssets.Item.Length) {
                return null;
            }
            if (loadCeahk && i.ModItem == null) {
                Main.instance.LoadItem(i.type);
            }

            return TextureAssets.Item[i.type].Value;
        }

        /// <summary>
        /// 安全的获取对应实例的图像资源
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static Texture2D T2DValue(this NPC n, bool loadCeahk = true) {
            if (Main.dedServ) {
                return null;
            }
            if (n.type < NPCID.None || n.type >= TextureAssets.Npc.Length) {
                return null;
            }
            if (loadCeahk && n.ModNPC == null) {
                Main.instance.LoadNPC(n.type);
            }

            return TextureAssets.Npc[n.type].Value;
        }

        /// <summary>
        /// 获取指定路径的纹理实例 <see cref="Texture2D"/>
        /// </summary>
        /// <param name="texture">纹理路径（相对于模组内容目录的路径）</param>
        /// <param name="immediateLoad">
        /// 是否立即加载纹理：
        /// <br>- <see langword="true"/>：同步加载纹理（适合需要立即使用的资源）</br>
        /// <br>- <see langword="false"/>：异步加载纹理（提升加载性能，适合非紧急资源）</br>
        /// </param>
        /// <returns>返回加载的 Texture2D 实例</returns>
        public static Texture2D GetT2DValue(string texture, bool immediateLoad = false) {
            return ModContent.Request<Texture2D>(texture
                , immediateLoad ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad).Value;
        }

        /// <summary>
        /// 获取指定路径的纹理资源（类型为 Asset&lt;Texture2D&gt;）
        /// </summary>
        /// <param name="texture">纹理路径（相对于模组内容目录的路径）</param>
        /// <param name="immediateLoad">
        /// 是否立即加载纹理：
        /// <br>- <see langword="true"/>：同步加载纹理（适合需要立即使用的资源）</br>
        /// <br>- <see langword="false"/>：异步加载纹理（提升加载性能，适合非紧急资源）</br>
        /// </param>
        /// <returns>返回加载的 Asset&lt;Texture2D&gt; 对象，包含纹理资源及其加载状态</returns>
        public static Asset<Texture2D> GetT2DAsset(string texture, bool immediateLoad = false) {
            return ModContent.Request<Texture2D>(texture
                , immediateLoad ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad);
        }

        /// <summary>
        /// 获取与纹理大小对应的矩形框
        /// </summary>
        /// <param name="value">纹理对象</param>
        public static Rectangle GetRec(Texture2D value) {
            return new Rectangle(0, 0, value.Width, value.Height);
        }
        /// <summary>
        /// 获取与纹理大小对应的矩形框
        /// </summary>
        /// <param name="value">纹理对象</param>
        /// <param name="Dx">X起点</param>
        /// <param name="Dy">Y起点</param>
        /// <param name="Sx">宽度</param>
        /// <param name="Sy">高度</param>
        /// <returns></returns>
        public static Rectangle GetRec(Texture2D value, int Dx, int Dy, int Sx, int Sy) {
            return new Rectangle(Dx, Dy, Sx, Sy);
        }
        /// <summary>
        /// 获取与纹理大小对应的矩形框
        /// </summary>
        /// <param name="value">纹理对象</param>
        /// <param name="frame">帧索引</param>
        /// <param name="frameCounterMax">总帧数，该值默认为1</param>
        /// <returns></returns>
        public static Rectangle GetRec(Texture2D value, int frame, int frameCounterMax = 1) {
            int singleFrameY = value.Height / frameCounterMax;
            return new Rectangle(0, singleFrameY * frame, value.Width, singleFrameY);
        }
        /// <summary>
        /// 获取与纹理大小对应的缩放中心
        /// </summary>
        /// <param name="value">纹理对象</param>
        /// <returns></returns>
        public static Vector2 GetOrig(Texture2D value) {
            return new Vector2(value.Width, value.Height) * 0.5f;
        }
        /// <summary>
        /// 获取与纹理大小对应的缩放中心
        /// </summary>
        /// <param name="value">纹理对象</param>
        /// <param name="frameCounter">帧索引</param>
        /// <param name="frameCounterMax">总帧数，该值默认为1</param>
        /// <returns></returns>
        public static Vector2 GetOrig(Texture2D value, int frameCounterMax = 1) {
            float singleFrameY = value.Height / frameCounterMax;
            return new Vector2(value.Width * 0.5f, singleFrameY / 2);
        }
        /// <summary>
        /// 对帧数索引进行走表
        /// </summary>
        /// <param name="frameCounter"></param>
        /// <param name="intervalFrame"></param>
        /// <param name="Maxframe"></param>
        public static void ClockFrame(ref int frameCounter, int intervalFrame, int maxFrame) {
            if (Main.GameUpdateCount % intervalFrame == 0) {
                frameCounter++;
            }

            if (frameCounter > maxFrame) {
                frameCounter = 0;
            }
        }
        /// <summary>
        /// 对帧数索引进行走表
        /// </summary>
        /// <param name="frameCounter"></param>
        /// <param name="intervalFrame"></param>
        /// <param name="Maxframe"></param>
        /// <param name="startCounter"></param>
        public static void ClockFrame(ref double frameCounter, int intervalFrame, int maxFrame, int startCounter = 0) {
            if (Main.GameUpdateCount % intervalFrame == 0) {
                frameCounter++;
            }

            if (frameCounter > maxFrame) {
                frameCounter = startCounter;
            }
        }

        /// <summary>
        /// 对帧数索引进行走表
        /// </summary>
        /// <param name="frameCounter"></param>
        /// <param name="intervalFrame"></param>
        /// <param name="Maxframe"></param>
        /// <param name="startCounter"></param>
        public static void ClockFrame(ref int frameCounter, int intervalFrame, int maxFrame, int startCounter = 0) {
            if (Main.GameUpdateCount % intervalFrame == 0) {
                frameCounter++;
            }

            if (frameCounter > maxFrame) {
                frameCounter = startCounter;
            }
        }

        /// <summary>
        /// 便捷的获取模组内的Effect实例
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Effect GetEffectValue(string name, bool immediateLoad = false) {
            return CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffect + name
                , immediateLoad ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad).Value;
        }

        #endregion

        #region TileUtils
        /// <summary>
        /// 将可能越界的方块坐标收值为非越界坐标
        /// </summary>
        public static Vector2 PTransgressionTile(Vector2 TileVr, int L = 0, int R = 0, int D = 0, int S = 0) {
            if (TileVr.X > Main.maxTilesX - R) {
                TileVr.X = Main.maxTilesX - R;
            }
            if (TileVr.X < 0 + L) {
                TileVr.X = 0 + L;
            }
            if (TileVr.Y > Main.maxTilesY - S) {
                TileVr.Y = Main.maxTilesY - S;
            }
            if (TileVr.Y < 0 + D) {
                TileVr.Y = 0 + D;
            }
            return new Vector2(TileVr.X, TileVr.Y);
        }

        /// <summary>
        /// 检测该位置是否存在一个实心的固体方块
        /// </summary>
        public static bool HasSolidTile(this Tile tile) {
            return tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        /// <summary>
        /// 获取一个物块目标，输入世界物块坐标，自动考虑收界情况
        /// </summary>
        public static Tile GetTile(int i, int j) {
            return GetTile(new Vector2(i, j));
        }

        /// <summary>
        /// 获取一个物块目标，输入世界物块坐标，自动考虑收界情况
        /// </summary>
        public static Tile GetTile(Vector2 pos) {
            pos = PTransgressionTile(pos);
            return Main.tile[(int)pos.X, (int)pos.Y];
        }

        #endregion
    }
}
