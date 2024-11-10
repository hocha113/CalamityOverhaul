using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs.NormalNPCs;
using CalamityOverhaul.Common;
using CalamityOverhaul.Common.Effects;
using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.Items;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Utilities;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.GameContent.ItemDropRules;
using Terraria.GameInput;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.Social;
using static CalamityMod.CalamityUtils;

namespace CalamityOverhaul
{
    public static class CWRUtils
    {
        #region System
        public static string GenerateRandomString(int length) {
            const string characters = "!@#$%^&*()-_=+[]{}|;:'\",.<>/?`~0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            char[] result = new char[length];

            for (int i = 0; i < length; i++) {
                result[i] = characters[Main.rand.Next(characters.Length)];
            }

            return new string(result);
        }

        public static LocalizedText SafeGetItemName<T>() where T : ModItem {
            Type type = typeof(T);
            return type.BaseType == typeof(EctypeItem)
                ? Language.GetText($"Mods.CalamityOverhaul.Items.{(Activator.CreateInstance(type) as EctypeItem)?.Name}.DisplayName")
                : GetItemName<T>();
        }

        public static LocalizedText SafeGetItemName(int id) {
            ModItem item = ItemLoader.GetItem(id);
            if (item == null) {
                return CWRLocText.GetText("None");
            }
            return item.GetLocalization("DisplayName");
        }

        public static void WebRedirection(this string str, bool inSteam = true) {
            if (SocialAPI.Mode == SocialMode.Steam && inSteam) {
                SteamFriends.ActivateGameOverlayToWebPage(str);
            }
            else {
                Utils.OpenToURL(str);
            }
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
                Text("ERROR Is Null", Color.Red);
                return;
            }
            Text(obj.ToString(), color);
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

        /// <summary>
        /// 将 Item 数组的信息写入指定路径的文件中
        /// </summary>
        /// <param name="items">要导出的 Item 数组</param>
        /// <param name="path">写入文件的路径，默认为 "D:\\模组资源\\AAModPrivate\\input.cs"</param>
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

        public static int GetTileDorp(Tile tile) {
            int stye = TileObjectData.GetTileStyle(tile);
            if (stye == -1) {
                stye = 0;
            }

            return TileLoader.GetItemDropFromTypeAndStyle(tile.TileType, stye);
        }

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

        /// <summary>
        /// 获取指定基类的所有子类列表
        /// </summary>
        /// <param name="baseType">基类的类型</param>
        /// <returns>子类列表</returns>
        public static List<Type> GetSubclassTypeList(Type baseType) {
            List<Type> subclasses = [];
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type[] allTypes = assembly.GetTypes();

            foreach (Type type in allTypes) {
                if (type.IsClass && !type.IsAbstract && baseType.IsAssignableFrom(type)) {
                    subclasses.Add(type);
                }
            }

            return subclasses;
        }

        /// <summary>
        /// 根据给定的类型列表，创建符合条件的类型实例，并将实例添加到输出列表中，该方法默认要求类型拥有无参构造
        /// </summary>
        public static List<T> GetSubclass<T>(bool parameterless = true) {
            List<Type> inTypes = GetSubclassTypeList(typeof(T));
            List<T> outInds = [];
            foreach (Type type in inTypes) {
                if (type != typeof(T)) {
                    object obj = parameterless ? Activator.CreateInstance(type) : RuntimeHelpers.GetUninitializedObject(type);
                    if (obj is T inds) {
                        outInds.Add(inds);
                    }
                }
            }
            return outInds;
        }

        /// <summary>
        /// 获取当前程序集（Assembly）中实现了指定接口（通过接口名称 `lname` 指定）的所有类的实例列表
        /// </summary>
        /// <typeparam name="T">接口类型，用于检查类是否实现该接口</typeparam>
        /// <param name="lname">接口的名称，用于匹配实现类</param>
        /// <returns>一个包含所有实现了指定接口的类实例的列表</returns>
        public static List<T> GetSubInterface<T>() {
            string lname = typeof(T).Name;
            List<T> subInterface = new List<T>();
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type[] allTypes = assembly.GetTypes();

            foreach (Type type in allTypes) {
                if (type.IsClass && !type.IsAbstract && type.GetInterface(lname) != null) {
                    object obj = RuntimeHelpers.GetUninitializedObject(type);
                    if (obj is T instance) {
                        subInterface.Add(instance);
                    }
                }
            }

            return subInterface;
        }
        #endregion

        #region AIUtils

        #region 工具部分

        public const float atoR = MathHelper.Pi / 180;

        public static float AtoR(this float num) => num * atoR;

        public static float RtoA(this float num) => num / atoR;

        /// <summary>
        /// 获取生成源
        /// </summary>
        public static EntitySource_Parent parent(this Entity entity) {
            return new EntitySource_Parent(entity);
        }

        /// <summary>
        /// 在指定位置施加吸引力或斥力，影响附近的NPC、投掷物、灰尘和物品
        /// </summary>
        /// <param name="position">施加效果的位置</param>
        /// <param name="range">影响范围的半径</param>
        /// <param name="strength">力的强度</param>
        /// <param name="projectiles">是否影响投掷物</param>
        /// <param name="magicOnly">是否仅影响魔法投掷物</param>
        /// <param name="npcs">是否影响NPC</param>
        /// <param name="items">是否影响物品</param>
        /// <param name="slow">力的影响程度，1 表示不受影响</param>
        public static void ForceFieldEffect(Vector2 position, int range, float strength, bool projectiles = true, bool magicOnly = false, bool npcs = true, bool items = true, float slow = 1.0f) {
            int rangeSquared = range * range;

            // 影响NPC
            if (npcs) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active) {
                        int dist = (int)Vector2.DistanceSquared(npc.Center, position);
                        if (dist < rangeSquared) {
                            npc.velocity *= slow;
                            npc.velocity += Vector2.Normalize(position - npc.Center) * strength;
                            _ = Dust.NewDust(npc.position, npc.width, npc.height, DustID.Shadowflame, 0, 0, 0, default);
                        }
                    }
                }
            }

            // 影响投掷物
            if (projectiles) {
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile projectile = Main.projectile[i];
                    if (projectile.active && (!magicOnly || (projectile.DamageType == DamageClass.Magic && projectile.friendly && !projectile.hostile))) {
                        int dist = (int)Vector2.DistanceSquared(projectile.Center, position);
                        if (dist < rangeSquared) {
                            projectile.velocity *= slow;
                            projectile.velocity += Vector2.Normalize(position - projectile.Center) * strength;
                            _ = Dust.NewDust(projectile.position, projectile.width, projectile.height, DustID.Shadowflame, 0, 0, 0, default);
                        }
                    }
                }
            }

            // 影响灰尘
            for (int i = 0; i < Main.maxDust; i++) {
                Dust dust = Main.dust[i];
                if (dust.active) {
                    int dist = (int)Vector2.DistanceSquared(dust.position, position);
                    if (dist < rangeSquared) {
                        dust.velocity *= slow;
                        dust.velocity += Vector2.Normalize(position - dust.position) * strength;
                    }
                }
            }

            // 影响物品
            if (items) {
                for (int i = 0; i < Main.maxItems; i++) {
                    Item item = Main.item[i];
                    if (item.active) {
                        int dist = (int)Vector2.DistanceSquared(item.Center, position);
                        if (dist < rangeSquared) {
                            item.velocity *= slow;
                            item.velocity += Vector2.Normalize(position - item.Center) * strength;
                            _ = Dust.NewDust(item.position, item.width, item.height, DustID.Shadowflame, 0, 0, 0, default);
                        }
                    }
                }
            }
        }

        public static void SetArrowRot(int proj) => Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        public static void SetArrowRot(this Projectile proj) => proj.rotation = proj.velocity.ToRotation() + MathHelper.PiOver2;

        /// <summary>
        /// 关于火箭的弹药映射
        /// </summary>
        /// <param name="ammoItem"></param>
        /// <returns></returns>
        public static int RocketAmmo(Item ammoItem) {
            int ammoTypes = ammoItem.shoot;
            if (ammoItem.type == ItemID.RocketI) {
                ammoTypes = ProjectileID.RocketI;
            }
            if (ammoItem.type == ItemID.RocketII) {
                ammoTypes = ProjectileID.RocketII;
            }
            if (ammoItem.type == ItemID.RocketIII) {
                ammoTypes = ProjectileID.RocketIII;
            }
            if (ammoItem.type == ItemID.RocketIV) {
                ammoTypes = ProjectileID.RocketIV;
            }
            if (ammoItem.type == ItemID.ClusterRocketI) {
                ammoTypes = ProjectileID.ClusterRocketI;
            }
            if (ammoItem.type == ItemID.ClusterRocketII) {
                ammoTypes = ProjectileID.ClusterRocketII;
            }
            if (ammoItem.type == ItemID.DryRocket) {
                ammoTypes = ProjectileID.DryRocket;
            }
            if (ammoItem.type == ItemID.WetRocket) {
                ammoTypes = ProjectileID.WetRocket;
            }
            if (ammoItem.type == ItemID.HoneyRocket) {
                ammoTypes = ProjectileID.HoneyRocket;
            }
            if (ammoItem.type == ItemID.LavaRocket) {
                ammoTypes = ProjectileID.LavaRocket;
            }
            if (ammoItem.type == ItemID.MiniNukeI) {
                ammoTypes = ProjectileID.MiniNukeRocketI;
            }
            if (ammoItem.type == ItemID.MiniNukeII) {
                ammoTypes = ProjectileID.MiniNukeRocketII;
            }
            return ammoTypes;
        }

        /// <summary>
        /// 雪人类弹药映射
        /// </summary>
        /// <param name="ammoItem"></param>
        /// <returns></returns>
        public static int SnowmanCannonAmmo(Item ammoItem) {
            int AmmoTypes = ProjectileID.RocketSnowmanI;
            switch (ammoItem.type) {
                case ItemID.RocketI:
                    AmmoTypes = ProjectileID.RocketSnowmanI;
                    break;
                case ItemID.RocketII:
                    AmmoTypes = ProjectileID.RocketSnowmanII;
                    break;
                case ItemID.RocketIII:
                    AmmoTypes = ProjectileID.RocketSnowmanIII;
                    break;
                case ItemID.RocketIV:
                    AmmoTypes = ProjectileID.RocketSnowmanIV;
                    break;
                case ItemID.ClusterRocketI:
                    AmmoTypes = ProjectileID.ClusterSnowmanRocketI;
                    break;
                case ItemID.ClusterRocketII:
                    AmmoTypes = ProjectileID.ClusterSnowmanRocketII;
                    break;
                case ItemID.DryRocket:
                    AmmoTypes = ProjectileID.DrySnowmanRocket;
                    break;
                case ItemID.WetRocket:
                    AmmoTypes = ProjectileID.WetSnowmanRocket;
                    break;
                case ItemID.HoneyRocket:
                    AmmoTypes = ProjectileID.HoneySnowmanRocket;
                    break;
                case ItemID.LavaRocket:
                    AmmoTypes = ProjectileID.LavaSnowmanRocket;
                    break;
                case ItemID.MiniNukeI:
                    AmmoTypes = ProjectileID.MiniNukeSnowmanRocketI;
                    break;
                case ItemID.MiniNukeII:
                    AmmoTypes = ProjectileID.MiniNukeSnowmanRocketII;
                    break;
            }
            return AmmoTypes;
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
        /// 进行圆形的碰撞检测
        /// </summary>
        /// <param name="centerPosition">中心点</param>
        /// <param name="radius">半径</param>
        /// <param name="targetHitbox">碰撞对象的箱体结构</param>
        /// <returns></returns>
        public static bool CircularHitboxCollision(Vector2 centerPosition, float radius, Rectangle targetHitbox) {
            if (new Rectangle((int)centerPosition.X, (int)centerPosition.Y, 1, 1).Intersects(targetHitbox)) {
                return true;
            }

            float distanceToTopLeft = Vector2.Distance(centerPosition, targetHitbox.TopLeft());
            float distanceToTopRight = Vector2.Distance(centerPosition, targetHitbox.TopRight());
            float distanceToBottomLeft = Vector2.Distance(centerPosition, targetHitbox.BottomLeft());
            float distanceToBottomRight = Vector2.Distance(centerPosition, targetHitbox.BottomRight());
            float closestDistance = distanceToTopLeft;

            if (distanceToTopRight < closestDistance) {
                closestDistance = distanceToTopRight;
            }

            if (distanceToBottomLeft < closestDistance) {
                closestDistance = distanceToBottomLeft;
            }

            if (distanceToBottomRight < closestDistance) {
                closestDistance = distanceToBottomRight;
            }

            return closestDistance <= radius;
        }

        /// <summary>
        /// 检测玩家是否有效且正常存活
        /// </summary>
        /// <returns>返回 true 表示活跃，返回 false 表示为空或者已经死亡的非活跃状态</returns>
        public static bool Alives(this Player player) {
            return player != null && player.active && !player.dead;
        }


        /// <summary>
        /// 检测弹幕是否有效且正常存活
        /// </summary>
        /// <returns>返回 true 表示活跃，返回 false 表示为空或者已经死亡的非活跃状态</returns>
        public static bool Alives(this Projectile projectile) {
            return projectile != null && projectile.active && projectile.timeLeft > 0;
        }

        /// <summary>
        /// 检测NPC是否有效且正常存活
        /// </summary>
        /// <returns>返回 true 表示活跃，返回 false 表示为空或者已经死亡的非活跃状态</returns>
        public static bool Alives(this NPC npc) {
            return npc != null && npc.active && npc.timeLeft > 0;
        }

        public static bool AlivesByNPC<T>(this ModNPC npc) where T : ModNPC {
            return npc != null && npc.NPC.Alives() && npc.NPC.type == ModContent.NPCType<T>();
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
        /// 返回该NPC的生命比例
        /// </summary>
        public static float NPCLifeRatio(NPC npc) {
            return npc.life / (float)npc.lifeMax;
        }

        /// <summary>
        /// 根据难度返回相应的血量数值
        /// </summary>
        public static int ConvenientBossHealth(int normalHealth, int expertHealth, int masterHealth) {
            return Main.expertMode ? expertHealth : Main.masterMode ? masterHealth : normalHealth;
        }
        /// <summary>
        /// 根据难度返回相应的伤害数值
        /// </summary>
        public static int ConvenientBossDamage(int normalDamage, int expertDamage, int masterDamage) {
            return Main.expertMode ? expertDamage : Main.masterMode ? masterDamage : normalDamage;
        }

        private static readonly object listLock = new();

        /// <summary>
        /// 用于处理NPC的局部集合加载问题
        /// </summary>
        /// <param name="Lists">这个NPC专属的局部集合</param>
        /// <param name="npc">NPC本身</param>
        /// <param name="NPCindexes">NPC的局部索引值</param>
        public static void LoadList(ref List<int> Lists, NPC npc, ref int NPCindexes) {
            ListUnNoAction(Lists, 0);//每次添加新元素时都将清理一次目标集合

            lock (listLock) {
                Lists.AddOrReplace(npc.whoAmI);
                NPCindexes = Lists.IndexOf(npc.whoAmI);
            }
        }

        /// <summary>
        /// 用于处理弹幕的局部集合加载问题
        /// </summary>
        /// <param name="Lists">这个弹幕专属的局部集合</param>
        /// <param name="projectile">弹幕本身</param>
        /// <param name="returnProJindex">弹幕的局部索引值</param>
        public static void LoadList(ref List<int> Lists, Projectile projectile, ref int returnProJindex) {
            ListUnNoAction(Lists, 1);

            lock (listLock) {
                Lists.AddOrReplace(projectile.whoAmI);
                returnProJindex = Lists.IndexOf(projectile.whoAmI);
            }
        }

        /// <summary>
        /// 用于处理NPC局部集合的善后工作，通常在NPC死亡或者无效化时调用，与 LoadList 配合使用
        /// </summary>
        public static void UnLoadList(ref List<int> Lists, NPC npc, ref int NPCindexes) {
            if (NPCindexes >= 0 && NPCindexes < Lists.Count) {
                Lists[NPCindexes] = -1;
            }
            else {
                npc.active = false;
                ListUnNoAction(Lists, 0);
            }
        }

        /// <summary>
        /// 用于处理弹幕局部集合的善后工作，通常在弹幕死亡或者无效化时调用，与 LoadList 配合使用
        /// </summary>
        public static void UnLoadList(ref List<int> Lists, Projectile projectile, ref int ProJindexes) {
            if (ProJindexes >= 0 && ProJindexes < Lists.Count) {
                Lists[ProJindexes] = -1;
            }
            else {
                projectile.active = false;
                ListUnNoAction(Lists, 1);
            }
        }

        /// <summary>
        /// 将非活跃的实体剔除出局部集合，该方法会影响到原集合
        /// </summary>
        /// <param name="Thislist">传入的局部集合</param>
        /// <param name="funcInt">处理对象，0将处理NPC，1将处理弹幕</param>
        public static void ListUnNoAction(List<int> Thislist, int funcInt) {
            List<int> list = Thislist.GetIntList();

            if (funcInt == 0) {
                foreach (int e in list) {
                    NPC npc = Main.npc[e];
                    int index = Thislist.IndexOf(e);

                    if (npc == null) {
                        Thislist[index] = -1;
                        continue;
                    }

                    if (npc.active == false) {
                        Thislist[index] = -1;
                    }
                }
            }
            if (funcInt == 1) {
                foreach (int e in list) {
                    Projectile proj = Main.projectile[e];
                    int index = Thislist.IndexOf(e);

                    if (proj == null) {
                        Thislist[index] = -1;
                        continue;
                    }

                    if (proj.active == false) {
                        Thislist[index] = -1;
                    }
                }
            }
        }

        /// <summary>
        /// 获取一个干净且无非活跃成员的集合，该方法不会直接影响原集合
        /// </summary>
        /// <param name="ThisList">传入的局部集合</param>
        /// <param name="funcInt">处理对象，0将处理NPC，非0值将处理弹幕</param>
        /// <param name="valueToReplace">决定排除对象，默认排除-1值元素</param>
        /// <returns></returns>
        public static List<int> GetListOnACtion(List<int> ThisList, int funcInt, int valueToReplace = -1) {
            List<int> list = ThisList.GetIntList();

            if (funcInt == 0) {
                foreach (int e in list) {
                    NPC npc = Main.npc[e];
                    int index = list.IndexOf(e);

                    if (npc == null) {
                        list[index] = -1;
                        continue;
                    }

                    if (npc.active == false) {
                        list[index] = -1;
                    }
                }

                return list.GetIntList();
            }
            else {
                foreach (int e in list) {
                    Projectile proj = Main.projectile[e];
                    int index = list.IndexOf(e);

                    if (proj == null) {
                        list[index] = -1;
                        continue;
                    }

                    if (proj.active == false) {
                        list[index] = -1;
                    }
                }

                return list.GetIntList();
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

        public static void SpawnGunDust(Projectile projectile, Vector2 pos, Vector2 velocity, int splNum = 1) {
            if (Main.myPlayer != projectile.owner) return;

            pos += velocity.SafeNormalize(Vector2.Zero) * projectile.width * projectile.scale * 0.71f;
            for (int i = 0; i < 30 * splNum; i++) {
                int dustID;
                switch (Main.rand.Next(6)) {
                    case 0:
                        dustID = 262;
                        break;
                    case 1:
                    case 2:
                        dustID = 54;
                        break;
                    default:
                        dustID = 53;
                        break;
                }
                float num = Main.rand.NextFloat(3f, 13f) * splNum;
                float angleRandom = 0.06f;
                Vector2 dustVel = new Vector2(num, 0f).RotatedBy((double)velocity.ToRotation(), default);
                dustVel = dustVel.RotatedBy(0f - angleRandom);
                dustVel = dustVel.RotatedByRandom(2f * angleRandom);
                if (Main.rand.NextBool(4)) {
                    dustVel = Vector2.Lerp(dustVel, -Vector2.UnitY * dustVel.Length(), Main.rand.NextFloat(0.6f, 0.85f)) * 0.9f;
                }
                float scale = Main.rand.NextFloat(0.5f, 1.5f);
                int idx = Dust.NewDust(pos, 1, 1, dustID, dustVel.X, dustVel.Y, 0, default, scale);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].position = pos;
            }
        }

        /// <summary>
        /// 让弹幕进行爆炸效果的操作
        /// </summary>
        /// <param name="projectile">要爆炸的投射物</param>
        /// <param name="blastRadius">爆炸效果的半径（默认为 120 单位）</param>
        /// <param name="explosionSound">爆炸声音的样式（默认为默认的爆炸声音）</param>
        public static void Explode(this Projectile projectile, int blastRadius = 120, SoundStyle explosionSound = default, bool spanSound = true) {
            Vector2 originalPosition = projectile.position;
            int originalWidth = projectile.width;
            int originalHeight = projectile.height;

            if (spanSound) {
                _ = SoundEngine.PlaySound(explosionSound == default ? SoundID.Item14 : explosionSound, projectile.Center);
            }

            projectile.position = projectile.Center;
            projectile.width = projectile.height = blastRadius * 2;
            projectile.position.X -= projectile.width / 2;
            projectile.position.Y -= projectile.height / 2;

            projectile.maxPenetrate = -1;
            projectile.penetrate = -1;
            projectile.usesLocalNPCImmunity = true;
            projectile.localNPCHitCooldown = -1;

            projectile.Damage();

            projectile.position = originalPosition;
            projectile.width = originalWidth;
            projectile.height = originalHeight;
        }

        /// <summary>
        /// 普通的追逐行为
        /// </summary>
        /// <param name="entity">需要操纵的实体</param>
        /// <param name="TargetCenter">目标地点</param>
        /// <param name="Speed">速度</param>
        /// <param name="ShutdownDistance">停摆距离</param>
        /// <returns></returns>
        public static Vector2 ChasingBehavior(this Entity entity, Vector2 TargetCenter, float Speed, float ShutdownDistance = 16) {
            if (entity == null) {
                return Vector2.Zero;
            }

            Vector2 ToTarget = TargetCenter - entity.Center;
            Vector2 ToTargetNormalize = ToTarget.SafeNormalize(Vector2.Zero);
            Vector2 speed = ToTargetNormalize * AsymptoticVelocity(entity.Center, TargetCenter, Speed, ShutdownDistance);
            entity.velocity = speed;
            return speed;
        }

        /// <summary>
        /// 更加缓和的追逐行为
        /// </summary>
        /// <param name="entity">需要操纵的实体</param>
        /// <param name="TargetCenter">目标地点</param>
        /// <param name="SpeedUpdates">速度的更新系数</param>
        /// <param name="HomingStrenght">追击力度</param>
        /// <returns></returns>
        public static Vector2 ChasingBehavior2(this Entity entity, Vector2 TargetCenter, float SpeedUpdates = 1, float HomingStrenght = 0.1f) {
            float targetAngle = entity.AngleTo(TargetCenter);
            float f = entity.velocity.ToRotation().RotTowards(targetAngle, HomingStrenght);
            Vector2 speed = f.ToRotationVector2() * entity.velocity.Length() * SpeedUpdates;
            entity.velocity = speed;
            return speed;
        }

        /// <summary>
        /// 寻找距离指定位置最近的NPC
        /// </summary>
        /// <param name="origin">开始搜索的位置</param>
        /// <param name="maxDistanceToCheck">搜索NPC的最大距离</param>
        /// <param name="ignoreTiles">在检查障碍物时是否忽略瓦片</param>
        /// <param name="bossPriority">是否优先选择Boss</param>
        /// <returns>距离最近的NPC。</returns>
        public static NPC FindClosestNPC(this Vector2 origin, float maxDistanceToCheck, bool ignoreTiles = true, bool bossPriority = false) {
            NPC closestTarget = null;
            float distance = maxDistanceToCheck;
            if (bossPriority) {
                bool bossFound = false;
                for (int index2 = 0; index2 < Main.npc.Length; index2++) {
                    if ((bossFound && !Main.npc[index2].boss && Main.npc[index2].type != NPCID.WallofFleshEye) || !Main.npc[index2].CanBeChasedBy()) {
                        continue;
                    }
                    float extraDistance2 = (Main.npc[index2].width / 2) + (Main.npc[index2].height / 2);
                    bool canHit2 = true;
                    if (extraDistance2 < distance && !ignoreTiles) {
                        canHit2 = Collision.CanHit(origin, 1, 1, Main.npc[index2].Center, 1, 1);
                    }
                    if (Vector2.Distance(origin, Main.npc[index2].Center) < distance + extraDistance2 && canHit2) {
                        if (Main.npc[index2].boss || Main.npc[index2].type == NPCID.WallofFleshEye) {
                            bossFound = true;
                        }
                        distance = Vector2.Distance(origin, Main.npc[index2].Center);
                        closestTarget = Main.npc[index2];
                    }
                }
            }
            else {
                for (int index = 0; index < Main.npc.Length; index++) {
                    if (Main.npc[index].CanBeChasedBy()) {
                        float extraDistance = (Main.npc[index].width / 2) + (Main.npc[index].height / 2);
                        bool canHit = true;
                        if (extraDistance < distance && !ignoreTiles) {
                            canHit = Collision.CanHit(origin, 1, 1, Main.npc[index].Center, 1, 1);
                        }
                        if (Vector2.Distance(origin, Main.npc[index].Center) < distance + extraDistance && canHit) {
                            distance = Vector2.Distance(origin, Main.npc[index].Center);
                            closestTarget = Main.npc[index];
                        }
                    }
                }
            }
            return closestTarget;
        }

        public static void EntityToRot(this NPC entity, float ToRot, float rotSpeed) {
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
                || Main.snowMoon || DD2Event.Ongoing || AcidRainEvent.AcidRainEventIsOngoing
                || TungstenRiot.Instance.TungstenRiotIsOngoing;

        public static bool IsTool(this Item item) => item.pick > 0 || item.axe > 0 || item.hammer > 0;

        public static Item GetItem(this Player player) => Main.mouseItem.IsAir ? player.inventory[player.selectedItem] : Main.mouseItem;

        public static void GiveMeleeType(this Item item, bool isGiveTrueMelee = false) => item.DamageType = GiveMeleeType(isGiveTrueMelee);

        public static DamageClass GiveMeleeType(bool isGiveTrueMelee = false) => isGiveTrueMelee ? ModContent.GetInstance<TrueMeleeDamageClass>() : DamageClass.Melee;

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

        public static void SafeLoadItem(int id) {
            if (!Main.dedServ && id > 0 && id < TextureAssets.Item.Length && Main.Assets != null && TextureAssets.Item[id] != null) {
                Main.instance.LoadItem(id);
            }
        }

        public static void SafeLoadProj(int id) {
            if (!Main.dedServ && id > 0 && id < TextureAssets.Projectile.Length && Main.Assets != null && TextureAssets.Projectile[id] != null) {
                Main.instance.LoadProjectile(id);
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
            recipe.AddConsumeItemCallback((Recipe recipe, int type, ref int amount) => { amount = 0; })
            .AddOnCraftCallback(CWRRecipes.SpawnAction);

        /// <summary>
        /// 让一个NPC可以正常的掉落物品而不触发其他死亡事件，只应该在非服务端上调用该方法
        /// </summary>
        /// <param name="npc"></param>
        public static void DropItem(this NPC npc) {
            DropAttemptInfo dropAttemptInfo = default;
            dropAttemptInfo.player = Main.LocalPlayer;
            dropAttemptInfo.npc = npc;
            dropAttemptInfo.IsExpertMode = Main.expertMode;
            dropAttemptInfo.IsMasterMode = Main.masterMode;
            dropAttemptInfo.IsInSimulation = false;
            dropAttemptInfo.rng = Main.rand;
            DropAttemptInfo info = dropAttemptInfo;
            Main.ItemDropSolver.TryDropping(info);
        }

        /// <summary>
        /// 用于将一个武器设置为手持刀剑类，这个函数若要正确设置物品的近战属性，需要让其在初始化函数中最后调用
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        public static void SetKnifeHeld<T>(this Item item) where T : ModProjectile {
            if (item.shoot == ProjectileID.None || !item.noUseGraphic
                || item.DamageType == ModContent.GetInstance<TrueMeleeDamageClass>()
                || item.DamageType == ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>()) {
                item.CWR().GetMeleePrefix = true;
            }
            item.noMelee = true;
            item.noUseGraphic = true;
            item.CWR().IsShootCountCorlUse = true;
            item.shoot = ModContent.ProjectileType<T>();
        }

        /// <summary>
        /// 获取玩家对象一个稳定的中心位置，考虑斜坡矫正与坐骑矫正，适合用于处理手持弹幕的位置获取
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static Vector2 GetPlayerStabilityCenter(this Player player) => player.MountedCenter.Floor() + new Vector2(0, player.gfxOffY);

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

        /// <summary>
        /// 计算并获取物品的前缀附加属性
        /// 根据物品的前缀ID，确定前缀所提供的各种属性加成，包括伤害倍率、击退倍率、使用时间倍率、尺寸倍率、射速倍率、法力消耗倍率以及暴击加成，
        /// 并根据这些加成计算出前缀的总体强度。对于模组的前缀，使用自定义的逻辑处理属性加成
        /// </summary>
        /// <param name="item">带有前缀的物品实例。</param>
        /// <returns>
        /// 返回包含前缀附加属性的结构体<see cref="PrefixState"/>，
        /// 该结构体中包括前缀ID以及计算得到的属性加成与前缀强度
        /// </returns>
        public static PrefixState GetPrefixState(this Item item) {
            int prefixID = item.prefix;

            PrefixState additionStruct = new PrefixState();

            float strength;
            float damageMult = 1f;
            float knockbackMult = 1f;
            float useTimeMult = 1f;
            float scaleMult = 1f;
            float shootSpeedMult = 1f;
            float manaMult = 1f;
            int critBonus = 0;

            if (prefixID >= PrefixID.Count && prefixID < PrefixLoader.PrefixCount) {
                additionStruct.isModPreFix = true;
                PrefixLoader.GetPrefix(prefixID).SetStats(ref damageMult, ref knockbackMult
                    , ref useTimeMult, ref scaleMult, ref shootSpeedMult, ref manaMult, ref critBonus);
            }
            else {
                additionStruct.isModPreFix = false;
                switch (prefixID) {
                    case 1:
                        scaleMult = 1.12f;
                        break;
                    case 2:
                        scaleMult = 1.18f;
                        break;
                    case 3:
                        damageMult = 1.05f;
                        critBonus = 2;
                        scaleMult = 1.05f;
                        break;
                    case 4:
                        damageMult = 1.1f;
                        scaleMult = 1.1f;
                        knockbackMult = 1.1f;
                        break;
                    case 5:
                        damageMult = 1.15f;
                        break;
                    case 6:
                        damageMult = 1.1f;
                        break;
                    case 81:
                        knockbackMult = 1.15f;
                        damageMult = 1.15f;
                        critBonus = 5;
                        useTimeMult = 0.9f;
                        scaleMult = 1.1f;
                        break;
                    case 7:
                        scaleMult = 0.82f;
                        break;
                    case 8:
                        knockbackMult = 0.85f;
                        damageMult = 0.85f;
                        scaleMult = 0.87f;
                        break;
                    case 9:
                        scaleMult = 0.9f;
                        break;
                    case 10:
                        damageMult = 0.85f;
                        break;
                    case 11:
                        useTimeMult = 1.1f;
                        knockbackMult = 0.9f;
                        scaleMult = 0.9f;
                        break;
                    case 12:
                        knockbackMult = 1.1f;
                        damageMult = 1.05f;
                        scaleMult = 1.1f;
                        useTimeMult = 1.15f;
                        break;
                    case 13:
                        knockbackMult = 0.8f;
                        damageMult = 0.9f;
                        scaleMult = 1.1f;
                        break;
                    case 14:
                        knockbackMult = 1.15f;
                        useTimeMult = 1.1f;
                        break;
                    case 15:
                        knockbackMult = 0.9f;
                        useTimeMult = 0.85f;
                        break;
                    case 16:
                        damageMult = 1.1f;
                        critBonus = 3;
                        break;
                    case 17:
                        useTimeMult = 0.85f;
                        shootSpeedMult = 1.1f;
                        break;
                    case 18:
                        useTimeMult = 0.9f;
                        shootSpeedMult = 1.15f;
                        break;
                    case 19:
                        knockbackMult = 1.15f;
                        shootSpeedMult = 1.05f;
                        break;
                    case 20:
                        knockbackMult = 1.05f;
                        shootSpeedMult = 1.05f;
                        damageMult = 1.1f;
                        useTimeMult = 0.95f;
                        critBonus = 2;
                        break;
                    case 21:
                        knockbackMult = 1.15f;
                        damageMult = 1.1f;
                        break;
                    case 82:
                        knockbackMult = 1.15f;
                        damageMult = 1.15f;
                        critBonus = 5;
                        useTimeMult = 0.9f;
                        shootSpeedMult = 1.1f;
                        break;
                    case 22:
                        knockbackMult = 0.9f;
                        shootSpeedMult = 0.9f;
                        damageMult = 0.85f;
                        break;
                    case 23:
                        useTimeMult = 1.15f;
                        shootSpeedMult = 0.9f;
                        break;
                    case 24:
                        useTimeMult = 1.1f;
                        knockbackMult = 0.8f;
                        break;
                    case 25:
                        useTimeMult = 1.1f;
                        damageMult = 1.15f;
                        critBonus = 1;
                        break;
                    case 58:
                        useTimeMult = 0.85f;
                        damageMult = 0.85f;
                        break;
                    case 26:
                        manaMult = 0.85f;
                        damageMult = 1.1f;
                        break;
                    case 27:
                        manaMult = 0.85f;
                        break;
                    case 28:
                        manaMult = 0.85f;
                        damageMult = 1.15f;
                        knockbackMult = 1.05f;
                        break;
                    case 83:
                        knockbackMult = 1.15f;
                        damageMult = 1.15f;
                        critBonus = 5;
                        useTimeMult = 0.9f;
                        manaMult = 0.9f;
                        break;
                    case 29:
                        manaMult = 1.1f;
                        break;
                    case 30:
                        manaMult = 1.2f;
                        damageMult = 0.9f;
                        break;
                    case 31:
                        knockbackMult = 0.9f;
                        damageMult = 0.9f;
                        break;
                    case 32:
                        manaMult = 1.15f;
                        damageMult = 1.1f;
                        break;
                    case 33:
                        manaMult = 1.1f;
                        knockbackMult = 1.1f;
                        useTimeMult = 0.9f;
                        break;
                    case 34:
                        manaMult = 0.9f;
                        knockbackMult = 1.1f;
                        useTimeMult = 1.1f;
                        damageMult = 1.1f;
                        break;
                    case 35:
                        manaMult = 1.2f;
                        damageMult = 1.15f;
                        knockbackMult = 1.15f;
                        break;
                    case 52:
                        manaMult = 0.9f;
                        damageMult = 0.9f;
                        useTimeMult = 0.9f;
                        break;
                    case 84:
                        knockbackMult = 1.17f;
                        damageMult = 1.17f;
                        critBonus = 8;
                        break;
                    case 36:
                        critBonus = 3;
                        break;
                    case 37:
                        damageMult = 1.1f;
                        critBonus = 3;
                        knockbackMult = 1.1f;
                        break;
                    case 38:
                        knockbackMult = 1.15f;
                        break;
                    case 53:
                        damageMult = 1.1f;
                        break;
                    case 54:
                        knockbackMult = 1.15f;
                        break;
                    case 55:
                        knockbackMult = 1.15f;
                        damageMult = 1.05f;
                        break;
                    case 59:
                        knockbackMult = 1.15f;
                        damageMult = 1.15f;
                        critBonus = 5;
                        break;
                    case 60:
                        damageMult = 1.15f;
                        critBonus = 5;
                        break;
                    case 61:
                        critBonus = 5;
                        break;
                    case 39:
                        damageMult = 0.7f;
                        knockbackMult = 0.8f;
                        break;
                    case 40:
                        damageMult = 0.85f;
                        break;
                    case 56:
                        knockbackMult = 0.8f;
                        break;
                    case 41:
                        knockbackMult = 0.85f;
                        damageMult = 0.9f;
                        break;
                    case 57:
                        knockbackMult = 0.9f;
                        damageMult = 1.18f;
                        break;
                    case 42:
                        useTimeMult = 0.9f;
                        break;
                    case 43:
                        damageMult = 1.1f;
                        useTimeMult = 0.9f;
                        break;
                    case 44:
                        useTimeMult = 0.9f;
                        critBonus = 3;
                        break;
                    case 45:
                        useTimeMult = 0.95f;
                        break;
                    case 46:
                        critBonus = 3;
                        useTimeMult = 0.94f;
                        damageMult = 1.07f;
                        break;
                    case 47:
                        useTimeMult = 1.15f;
                        break;
                    case 48:
                        useTimeMult = 1.2f;
                        break;
                    case 49:
                        useTimeMult = 1.08f;
                        break;
                    case 50:
                        damageMult = 0.8f;
                        useTimeMult = 1.15f;
                        break;
                    case 51:
                        knockbackMult = 0.9f;
                        useTimeMult = 0.9f;
                        damageMult = 1.05f;
                        critBonus = 2;
                        break;
                }
            }

            strength = 1f * damageMult * (2f - useTimeMult) * (2f - manaMult) * scaleMult
                * knockbackMult * shootSpeedMult * (1f + critBonus * 0.02f);
            if (prefixID == 62 || prefixID == 69 || prefixID == 73 || prefixID == 77)
                strength *= 1.05f;

            if (prefixID == 63 || prefixID == 70 || prefixID == 74 || prefixID == 78 || prefixID == 67)
                strength *= 1.1f;

            if (prefixID == 64 || prefixID == 71 || prefixID == 75 || prefixID == 79 || prefixID == 66)
                strength *= 1.15f;

            if (prefixID == 65 || prefixID == 72 || prefixID == 76 || prefixID == 80 || prefixID == 68)
                strength *= 1.2f;

            additionStruct.prefixID = prefixID;
            additionStruct.damageMult = damageMult;
            additionStruct.knockbackMult = knockbackMult;
            additionStruct.useTimeMult = useTimeMult;
            additionStruct.scaleMult = scaleMult;
            additionStruct.shootSpeedMult = shootSpeedMult;
            additionStruct.manaMult = manaMult;
            additionStruct.critBonus = critBonus;
            additionStruct.strength = strength;

            return additionStruct;
        }

        public static ShootState GetShootState(this Player player, string shootKey = "Null") {
            ShootState shootState = new();
            Item item = player.ActiveItem();
            if (item.useAmmo == AmmoID.None) {
                shootState.WeaponDamage = player.GetWeaponDamage(item);
                shootState.WeaponKnockback = item.knockBack;
                shootState.AmmoTypes = item.shoot;
                shootState.ScaleFactor = item.shootSpeed;
                shootState.UseAmmoItemType = ItemID.None;
                shootState.HasAmmo = false;
                if (shootState.AmmoTypes == 0 || shootState.AmmoTypes == 10) {
                    shootState.AmmoTypes = ProjectileID.Bullet;
                }
                return shootState;
            }
            shootState.HasAmmo = player.PickAmmo(item, out shootState.AmmoTypes, out shootState.ScaleFactor
                , out shootState.WeaponDamage, out shootState.WeaponKnockback, out shootState.UseAmmoItemType, true);
            if (shootKey == "Null") {
                shootKey = null;
            }
            shootState.Source = new EntitySource_ItemUse_WithAmmo(player, item, shootState.UseAmmoItemType, shootKey);
            return shootState;
        }

        public static AmmoState GetAmmoState(this Player player, int assignAmmoType = 0, bool numSort = false) {
            AmmoState ammoState = new();
            int num = 0;
            List<Item> itemInds = [];
            List<int> itemTypes = [];
            List<int> itemShootTypes = [];
            foreach (Item item in player.inventory) {
                if (item.ammo == AmmoID.None) {
                    continue;
                }
                if (assignAmmoType != 0) {
                    if (item.ammo != assignAmmoType) {
                        continue;
                    }
                }
                itemTypes.Add(item.type);
                itemShootTypes.Add(item.shoot);
                num += item.stack;
            }
            for (int i = 54; i < 58; i++) {
                Item item = player.inventory[i];
                if ((assignAmmoType != 0 && item.ammo != assignAmmoType) || item.ammo == AmmoID.None) {
                    continue;
                }
                itemInds.Add(player.inventory[i]);
            }
            for (int i = 0; i < 54; i++) {
                Item item = player.inventory[i];
                if ((assignAmmoType != 0 && item.ammo != assignAmmoType) || item.ammo == AmmoID.None) {
                    continue;
                }
                itemInds.Add(player.inventory[i]);
            }
            if (numSort) {
                itemInds = itemInds.OrderByDescending(item => item.stack).ToList();
            }
            ammoState.InProjIDs = itemShootTypes.ToArray();
            ammoState.InItemInds = itemInds.ToArray();
            ammoState.InItemIDs = itemTypes.ToArray();
            ammoState.Amount = num;
            if (itemInds.Count > 0) {
                ammoState.MaxAmountToItem = itemInds[0];
                ammoState.MinAmountToItem = itemInds[itemInds.Count - 1];
            }
            else {
                ammoState.MaxAmountToItem = new Item();
                ammoState.MinAmountToItem = new Item();
            }
            return ammoState;
        }

        /// <summary>
        /// 判断该弹药物品是否应该被视为无限弹药
        /// </summary>
        /// <param name="ammoItem">要检查的弹药物品</param>
        /// <returns>如果弹药物品是无限的，返回<see langword="true"/>；否则返回<see langword="false"/></returns>
        public static bool IsAmmunitionUnlimited(Item ammoItem) {
            bool result = !ammoItem.consumable;
            if (CWRMod.Instance.luiafk != null || CWRMod.Instance.improveGame != null) {
                if (ammoItem.stack >= 3996) {
                    result = true;
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
        public static void SetHotkey(this List<TooltipLine> tooltips, ModKeybind mhk, string keyName = "[KEY]", string modName = "Terraria") {
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
        public static void ReplaceTooltip(this List<TooltipLine> tooltips, string targetKeyStr, string contentStr, string modName = "Terraria") {
            TooltipLine line = tooltips.FirstOrDefault(x => x.Mod == modName && x.Text.Contains(targetKeyStr));
            if (line != null) {
                line.Text = line.Text.Replace(targetKeyStr, contentStr);
            }
        }

        /// <summary>
        /// 快速从模组本地化文件中设置对应物品的名称
        /// </summary>
        /// <param name="item"></param>
        /// <param name="key"></param>
        public static void EasySetLocalTextNameOverride(this Item item, string key) {
            if (Main.GameModeInfo.IsJourneyMode) {
                return;
            }
            item.SetNameOverride(Language.GetText($"Mods.CalamityOverhaul.Items.{key}.DisplayName").Value);
        }

        /// <summary>
        /// 在游戏中发送文本消息
        /// </summary>
        /// <param name="message">要发送的消息文本</param>
        /// <param name="colour">（可选）消息的颜色,默认为 null</param>
        public static void Text(string message, Color? colour = null) {
            Color newColor = (Color)(colour == null ? Color.White : colour);
            if (Main.netMode == NetmodeID.Server) {
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), (Color)(colour == null ? Color.White : colour));
                return;
            }
            Main.NewText(message, newColor);
        }

        /// <summary>
        /// 一个根据语言选项返回字符的方法
        /// </summary>
        public static string Translation(string Chinese = null, string English = null, string Spanish = null, string Russian = null) {
            string text = default(string);

            if (English == null) {
                English = "Invalid Character";
            }

            switch (Language.ActiveCulture.LegacyId) {
                case (int)GameCulture.CultureName.Chinese:
                    text = Chinese;
                    break;
                case (int)GameCulture.CultureName.Russian:
                    text = Russian;
                    break;
                case (int)GameCulture.CultureName.Spanish:
                    text = Spanish;
                    break;
                case (int)GameCulture.CultureName.English:
                    text = English;
                    break;
                default:
                    text = English;
                    break;
            }

            return text;
        }

        public static Color MultiStepColorLerp(float percent, params Color[] colors) {
            if (colors == null) {
                Text("MultiLerpColor: 空的颜色数组!");
                return Color.White;
            }
            float per = 1f / (colors.Length - 1f);
            float total = per;
            int currentID = 0;
            while (percent / total > 1f && currentID < colors.Length - 2) {
                total += per;
                currentID++;
            }
            return Color.Lerp(colors[currentID], colors[currentID + 1], (percent - (per * currentID)) / per);
        }

        /// <summary>
        /// 快速修改一个物品的简介文本，从模组本地化文本中拉取资源
        /// </summary>
        public static void OnModifyTooltips(Mod mod, List<TooltipLine> tooltips, string key) {
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

            TooltipLine newLine = new(mod, "CWRText"
                , Language.GetText($"Mods.CalamityOverhaul.Items.{key}.Tooltip").Value);
            newTooltips.Add(newLine);
            newTooltips.AddRange(overTooltips);
            tooltips.Clear(); // 清空原 tooltips 集合
            tooltips.AddRange(newTooltips); // 添加修改后的 newTooltips 集合
            tooltips.AddRange(prefixTooltips);
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

        /// <summary>
        /// 检查指定玩家是否按下了鼠标键
        /// </summary>
        /// <param name="player">要检查的玩家</param>
        /// <param name="leftCed">是否检查左鼠标键，否则检测右鼠标键</param>
        /// <param name="netCed">是否进行网络同步检查</param>
        /// <returns>如果按下了指定的鼠标键，则返回true，否则返回false</returns>
        public static bool PressKey(this Player player, bool leftCed = true, bool netCed = true) {
            return (!netCed || Main.myPlayer == player.whoAmI) && (leftCed ? PlayerInput.Triggers.Current.MouseLeft : PlayerInput.Triggers.Current.MouseRight);
        }

        public static CWRNpc CWR(this NPC npc) {
            return npc.GetGlobalNPC<CWRNpc>();
        }

        public static CWRPlayer CWR(this Player player) {
            return player.GetModPlayer<CWRPlayer>();
        }

        public static CWRItems CWR(this Item item) {
            if (item.type == ItemID.None) {
                Text("ERROR!发生了一次空传递，该物品为None!");
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

        #region NetUtils

        /// <summary>
        /// 判断是否处于客户端状态，如果是在单人或者服务端下将返回false
        /// </summary>
        public static bool isClient => Main.netMode == NetmodeID.MultiplayerClient;
        /// <summary>
        /// 判断是否处于服务端状态，如果是在单人或者客户端下将返回false
        /// </summary>
        public static bool isServer => Main.netMode == NetmodeID.Server;
        /// <summary>
        /// 仅判断是否处于单人状态，在单人模式下返回true
        /// </summary>
        public static bool isSinglePlayer => Main.netMode == NetmodeID.SinglePlayer;
        /// <summary>
        /// 发收统一端口的实例
        /// </summary>
        public static ModPacket Packet => CWRMod.Instance.GetPacket();
        /// <summary>
        /// 检查一个 Projectile 对象是否属于当前客户端玩家拥有的，如果是，返回true
        /// </summary>
        public static bool IsOwnedByLocalPlayer(this Projectile projectile) => projectile.owner == Main.myPlayer;

        /// <summary>
        /// 生成Boss级实体，考虑网络状态
        /// </summary>
        /// <param name="player">触发生成的玩家实例</param>
        /// <param name="bossType">要生成的 Boss 的类型</param>
        /// <param name="obeyLocalPlayerCheck">是否要遵循本地玩家检查</param>
        public static void SpawnBossNetcoded(Player player, int bossType, bool obeyLocalPlayerCheck = true) {
            if (player.whoAmI == Main.myPlayer || !obeyLocalPlayerCheck) {
                // 如果使用物品的玩家是客户端
                // （在此明确排除了服务器端）

                _ = SoundEngine.PlaySound(SoundID.Roar, player.position);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // 如果玩家不在多人游戏中，直接生成 Boss
                    NPC.SpawnOnPlayer(player.whoAmI, bossType);
                }
                else {
                    // 如果玩家在多人游戏中，请求生成
                    // 仅当 NPCID.Sets.MPAllowedEnemies[type] 为真时才有效，需要在 NPC 代码中设置

                    NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: bossType);
                }
            }
        }

        /// <summary>
        /// 在易于使用的方式下生成一个新的 NPC，考虑网络状态
        /// </summary>
        /// <param name="source">生成 NPC 的实体源</param>
        /// <param name="spawnPos">生成的位置</param>
        /// <param name="type">NPC 的类型</param>
        /// <param name="start">NPC 的初始状态</param>
        /// <param name="ai0">NPC 的 AI 参数 0</param>
        /// <param name="ai1">NPC 的 AI 参数 1</param>
        /// <param name="ai2">NPC 的 AI 参数 2</param>
        /// <param name="ai3">NPC 的 AI 参数 3</param>
        /// <param name="target">NPC 的目标 ID</param>
        /// <param name="velocity">NPC 的初始速度</param>
        /// <returns>新生成的 NPC 的 ID</returns>
        public static int NewNPCEasy(IEntitySource source, Vector2 spawnPos, int type, int start = 0, float ai0 = 0, float ai1 = 0, float ai2 = 0, float ai3 = 0, int target = 255, Vector2 velocity = default) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return Main.maxNPCs;
            }

            int n = NPC.NewNPC(source, (int)spawnPos.X, (int)spawnPos.Y, type, start, ai0, ai1, ai2, ai3, target);
            if (n != Main.maxNPCs) {
                if (velocity != default) {
                    Main.npc[n].velocity = velocity;
                }

                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, n);
                }
            }
            return n;
        }
        #endregion

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
        /// 比较两个角度之间的差异，将结果限制在 -π 到 π 的范围内
        /// </summary>
        /// <param name="baseAngle">基准角度（参考角度）</param>
        /// <param name="targetAngle">目标角度（待比较角度）</param>
        /// <returns>从基准角度到目标角度的差异，范围在 -π 到 π 之间</returns>
        public static float CompareAngle(float baseAngle, float targetAngle) {
            return ((baseAngle - targetAngle + ((float)Math.PI * 3)) % MathHelper.TwoPi) - (float)Math.PI;// 计算两个角度之间的差异并将结果限制在 -π 到 π 的范围内
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

        public static Vector2 To(this Vector2 vr1, Vector2 vr2) => vr2 - vr1;

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
        /// 获取一个垂直于该向量的单位向量
        /// </summary>
        public static Vector2 GetNormalVector(this Vector2 vr) {
            Vector2 nVr = new(vr.Y, -vr.X);
            return Vector2.Normalize(nVr);
        }

        /// <summary>
        /// 简单安全的获取一个单位向量，如果出现非法情况则会返回 <see cref="Vector2.Zero"/>
        /// </summary>
        public static Vector2 UnitVector(this Vector2 vr) {
            return vr.SafeNormalize(Vector2.Zero);
        }

        /// <summary>
        /// 简单安全的获取一个单位向量，如果出现非法情况则会返回 <see cref="Vector2.Zero"/>
        /// </summary>
        public static Vector2 UnitVector(this Vector2 vr, float mode) {
            return vr.SafeNormalize(Vector2.Zero) * mode;
        }

        /// <summary>
        /// 计算两个向量的点积
        /// </summary>
        public static float DotProduct(this Vector2 vr1, Vector2 vr2) {
            return (vr1.X * vr2.X) + (vr1.Y * vr2.Y);
        }

        /// <summary>
        /// 检测索引的合法性
        /// </summary>
        /// <returns>合法将返回 <see cref="true"/></returns>
        public static bool ValidateIndex(this int index, Array array) {
            return index >= 0 && index < array.Length;
        }

        /// <summary>
        /// 检测索引的合法性
        /// </summary>
        public static bool ValidateIndex(this int index, int cap) {
            return index >= 0 && index < cap;
        }

        /// <summary>
        /// 会自动替补-1元素
        /// </summary>
        /// <param name="list">目标集合</param>
        /// <param name="valueToAdd">替换为什么值</param>
        /// <param name="valueToReplace">替换的目标对象的值，不填则默认为-1</param>
        public static void AddOrReplace(this List<int> list, int valueToAdd, int valueToReplace = -1) {
            int index = list.IndexOf(valueToReplace);
            if (index >= 0) {
                list[index] = valueToAdd;
            }
            else {
                list.Add(valueToAdd);
            }
        }

        /// <summary>
        /// 返回一个集合的筛选副本，排除数默认为-1，该扩展方法不会影响原集合
        /// </summary>
        public static List<int> GetIntList(this List<int> list, int valueToReplace = -1) {
            List<int> result = new(list);
            _ = result.RemoveAll(item => item == -1);
            return result;
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


        public const float TwoPi = MathF.PI * 2;
        public const float FourPi = MathF.PI * 4;
        public const float ThreePi = MathF.PI * 3;
        public const float PiOver3 = MathF.PI / 3f;
        public const float PiOver5 = MathF.PI / 5f;
        public const float PiOver6 = MathF.PI / 6f;

        #endregion

        #region DrawUtils

        #region 普通绘制工具

        public static void SimpleDrawItem(SpriteBatch spriteBatch, int itemType, Vector2 position, float size, float rotation, Color color, Vector2 orig = default) {
            Texture2D texture = TextureAssets.Item[itemType].Value;
            Rectangle? rectangle = Main.itemAnimations[itemType] != null ? Main.itemAnimations[itemType].GetFrame(texture) : texture.Frame(1, 1, 0, 0);
            if (orig == default) {
                orig = texture.Size() / 2;
                if (rectangle.HasValue) {
                    orig = rectangle.Value.Size() / 2;
                }
            }
            spriteBatch.Draw(texture, position, rectangle, color, rotation, orig, size, SpriteEffects.None, 0);
        }

        public static void DrawMarginEffect(SpriteBatch spriteBatch, Texture2D tex, int drawTimer, Vector2 position
            , Rectangle? rect, Color color, float rot, Vector2 origin, float scale, SpriteEffects effects = 0) {
            float time = Main.GlobalTimeWrappedHourly;
            float timer = drawTimer / 240f + time * 0.04f;
            time %= 4f;
            time /= 2f;
            if (time >= 1f)
                time = 2f - time;
            time = time * 0.5f + 0.5f;
            for (float i = 0f; i < 1f; i += 0.25f) {
                float radians = (i + timer) * MathHelper.TwoPi;
                spriteBatch.Draw(tex, position + new Vector2(0f, 8f).RotatedBy(radians) * time, rect
                    , new Color(color.R, color.G, color.B, 50), rot, origin, scale, effects, 0);
            }
            for (float i = 0f; i < 1f; i += 0.34f) {
                float radians = (i + timer) * MathHelper.TwoPi;
                spriteBatch.Draw(tex, position + new Vector2(0f, 4f).RotatedBy(radians) * time, rect
                    , new Color(color.R, color.G, color.B, 77), rot, origin, scale, effects, 0);
            }
        }

        public static void SetAnimation(int type, int tickValue, int maxFrame) {
            ItemID.Sets.AnimatesAsSoul[type] = true;
            Main.RegisterItemAnimation(type, new DrawAnimationVertical(tickValue, maxFrame));
        }

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

        public static void DrawEventProgressBar(SpriteBatch spriteBatch, Vector2 drawPos, Asset<Texture2D> iconAsset, float eventKillRatio, float size, int barWidth, int barHeight, string eventMainName, Color eventMainColor) {
            if (size < 0.1f) {
                return;
            }

            Vector2 textOffsetInCorePos = FontAssets.MouseText.Value.MeasureString(eventMainName);
            float x = 120f;
            if (textOffsetInCorePos.X > 200f) {
                x += textOffsetInCorePos.X - 200f;
            }

            Vector2 value1 = new Vector2(Main.screenWidth - x, Main.screenHeight - 80 + 1);
            Vector2 value2 = textOffsetInCorePos + new Vector2(iconAsset.Value.Width + 12, 6f);
            Rectangle iconRec = Utils.CenteredRectangle(value1, value2);

            Utils.DrawInvBG(spriteBatch, iconRec, eventMainColor * 0.5f * size);
            spriteBatch.Draw(iconAsset.Value, iconRec.Left() + Vector2.UnitX * 8f, null, Color.White * size
                , 0f, Vector2.UnitY * iconAsset.Value.Height / 2, 0.8f * size, SpriteEffects.None, 0f);
            Utils.DrawBorderString(spriteBatch, eventMainName, iconRec.Right() + Vector2.UnitX * -16f, Color.White * size, 0.9f * size, 1f, 0.4f, -1);

            drawPos += new Vector2(-100, 20);

            Rectangle screenCoordsRectangle = new Rectangle((int)drawPos.X - barWidth / 2, (int)drawPos.Y - barHeight / 2, barWidth, barHeight);
            Texture2D barTexture = TextureAssets.ColorBar.Value;

            Utils.DrawInvBG(spriteBatch, screenCoordsRectangle, new Color(6, 80, 84, 255) * 0.785f * size);
            spriteBatch.Draw(barTexture, drawPos, null, Color.White * size, 0f, new Vector2(barTexture.Width / 2, 0f), 1f * size, SpriteEffects.None, 0f);

            string barTextContent = Language.GetTextValue("Game.WaveCleared", (100 * eventKillRatio).ToString($"N{1}") + "%");
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(barTextContent);
            float barTextScale = 1f;
            if (textSize.Y > 22f)
                barTextScale *= 22f / textSize.Y;

            drawPos.Y += 10;
            Utils.DrawBorderString(spriteBatch, barTextContent, drawPos - Vector2.UnitY * 4f, Color.White * size, barTextScale, 0.5f, 1f, -1);

            float barDrawOffsetX = 169f;
            Vector2 barDrawPosition = drawPos + Vector2.UnitX * (eventKillRatio - 0.5f) * barDrawOffsetX;
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, barDrawPosition, new Rectangle(0, 0, 1, 1)
                , new Color(255, 241, 51) * size, 0f, new Vector2(1f, 0.5f), new Vector2(barDrawOffsetX * eventKillRatio, 8) * size, SpriteEffects.None, 0f);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, barDrawPosition, new Rectangle(0, 0, 1, 1)
                , new Color(255, 165, 0, 127) * size, 0f, new Vector2(1f, 0.5f), new Vector2(2f, 8) * size, SpriteEffects.None, 0f);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, barDrawPosition, new Rectangle(0, 0, 1, 1)
                , Color.Black * size, 0f, Vector2.UnitY * 0.5f, new Vector2(barDrawOffsetX * (1f - eventKillRatio), 8) * size, SpriteEffects.None, 0f);
        }

        public static string GetSafeText(string text, Vector2 textSize, float maxWidth) {
            int charWidth = (int)(textSize.X / text.Length);
            List<char> characters = text.ToList();
            List<char> wrappedText = [];
            int currentWidth = 0;

            foreach (char character in characters) {
                if (character == '\n') {
                    wrappedText.Add(character);
                    currentWidth = 0;
                }
                else {
                    int characterWidth = charWidth;

                    if (currentWidth + characterWidth > maxWidth) {
                        wrappedText.Add('\n');
                        currentWidth = 0;
                    }

                    wrappedText.Add(character);
                    currentWidth += characterWidth;
                }
            }

            return new string(wrappedText.ToArray());
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
        /// 将世界位置矫正为适应屏幕的画布位置
        /// </summary>
        /// <param name="pos">绘制目标的世界位置</param>
        /// <returns></returns>
        public static Vector2 WDEpos(Vector2 pos) {
            return pos - Main.screenPosition;
        }

        /// <summary>
        /// 获取纹理实例，类型为 Texture2D
        /// </summary>
        /// <param name="texture">纹理路径</param>
        /// <returns></returns>
        public static Texture2D GetT2DValue(string texture, bool immediateLoad = false) {
            return ModContent.Request<Texture2D>(texture
                , immediateLoad ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad).Value;
        }

        /// <summary>
        /// 获取纹理实例，类型为 AssetTexture2D
        /// </summary>
        /// <param name="texture">纹理路径</param>
        /// <returns></returns>
        public static Asset<Texture2D> GetT2DAsset(string texture, bool immediateLoad = false) {
            return ModContent.Request<Texture2D>(texture
                , immediateLoad ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad);
        }

        /// <summary>
        /// 便捷的获取模组内的Effect实例
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Effect GetEffectValue(string name, bool immediateLoad = false) {
            return CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffects + name
                , immediateLoad ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad).Value;
        }

        #endregion

        #region 高级绘制工具

        /// <summary>
        /// 使用反射来设置 _uImage1。它的底层数据是私有的，唯一可以公开更改它的方式是通过一个只接受原始纹理路径的方法
        /// </summary>
        /// <param name="shader">着色器</param>
        /// <param name="texture">要使用的纹理</param>
        public static void SetMiscShaderAsset_1(this MiscShaderData shader, Asset<Texture2D> texture) {
            EffectLoader.Shader_Texture_FieldInfo_1.SetValue(shader, texture);
        }

        /// <summary>
        /// 使用反射来设置 _uImage2。它的底层数据是私有的，唯一可以公开更改它的方式是通过一个只接受原始纹理路径的方法
        /// </summary>
        /// <param name="shader">着色器</param>
        /// <param name="texture">要使用的纹理</param>
        public static void SetMiscShaderAsset_2(this MiscShaderData shader, Asset<Texture2D> texture) {
            EffectLoader.Shader_Texture_FieldInfo_2.SetValue(shader, texture);
        }

        /// <summary>
        /// 使用反射来设置 _uImage3。它的底层数据是私有的，唯一可以公开更改它的方式是通过一个只接受原始纹理路径的方法
        /// </summary>
        /// <param name="shader">着色器</param>
        /// <param name="texture">要使用的纹理</param>
        public static void SetMiscShaderAsset_3(this MiscShaderData shader, Asset<Texture2D> texture) {
            EffectLoader.Shader_Texture_FieldInfo_3.SetValue(shader, texture);
        }

        /// <summary>
        /// 任意设置 <see cref=" SpriteBatch "/> 的 <see cref=" BlendState "/>。
        /// </summary>
        /// <param name="spriteBatch">绘制模式</param>
        /// <param name="blendState">要使用的混合状态</param>
        public static void ModifyBlendState(this SpriteBatch spriteBatch, BlendState blendState) {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, blendState, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 将 <see cref="SpriteBatch"/> 的 <see cref="BlendState"/> 重置为典型的 <see cref="BlendState.AlphaBlend"/>。
        /// </summary>
        /// <param name="spriteBatch">绘制模式</param>
        public static void ResetBlendState(this SpriteBatch spriteBatch) {
            spriteBatch.ModifyBlendState(BlendState.AlphaBlend);
        }

        /// <summary>
        /// 将 <see cref="SpriteBatch"/> 的 <see cref="BlendState"/> 设置为 <see cref="BlendState.Additive"/>。
        /// </summary>
        /// <param name="spriteBatch">绘制模式</param>
        public static void SetAdditiveState(this SpriteBatch spriteBatch) {
            spriteBatch.ModifyBlendState(BlendState.Additive);
        }

        /// <summary>
        /// 将 <see cref="SpriteBatch"/> 重置为无效果的UI画布状态，在大多数情况下，这个适合结束一段在UI中的绘制
        /// </summary>
        /// <param name="spriteBatch">绘制模式</param>
        public static void ResetUICanvasState(this SpriteBatch spriteBatch) {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);
        }

        public static Vector3 Vec3(this Vector2 vector) => new Vector3(vector.X, vector.Y, 0);
        #endregion

        #endregion

        #region TileUtils

        public static void SafeSquareTileFrame(Vector2 tilePos, Tile tile, bool resetFrame = true) {
            int i = (int)tilePos.X;
            int j = (int)tilePos.Y;
            TMLModifyFromeTileUtilsCode.TileFrame(i - 1, j - 1);
            TMLModifyFromeTileUtilsCode.TileFrame(i - 1, j);
            TMLModifyFromeTileUtilsCode.TileFrame(i - 1, j + 1);
            TMLModifyFromeTileUtilsCode.TileFrame(i, j - 1);
            try {
                TMLModifyFromeTileUtilsCode.TileFrame(i, j, resetFrame);
            } catch {
                TMLModifyFromeTileUtilsCode.DoErrorTile(tilePos, tile);
                return;
            }
            TMLModifyFromeTileUtilsCode.TileFrame(i, j + 1);
            TMLModifyFromeTileUtilsCode.TileFrame(i + 1, j - 1);
            TMLModifyFromeTileUtilsCode.TileFrame(i + 1, j);
            TMLModifyFromeTileUtilsCode.TileFrame(i + 1, j + 1);
        }

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

        #region UIUtils

        public static Rectangle GetClippingRectangle(SpriteBatch spriteBatch, Rectangle r) {
            Vector2 vector = new(r.X, r.Y);
            Vector2 position = new Vector2(r.Width, r.Height) + vector;
            vector = Vector2.Transform(vector, Main.UIScaleMatrix);
            position = Vector2.Transform(position, Main.UIScaleMatrix);
            Rectangle result = new((int)vector.X, (int)vector.Y, (int)(position.X - vector.X), (int)(position.Y - vector.Y));
            int width = spriteBatch.GraphicsDevice.Viewport.Width;
            int height = spriteBatch.GraphicsDevice.Viewport.Height;
            result.X = Utils.Clamp<int>(result.X, 0, width);
            result.Y = Utils.Clamp<int>(result.Y, 0, height);
            result.Width = Utils.Clamp<int>(result.Width, 0, width - result.X);
            result.Height = Utils.Clamp<int>(result.Height, 0, height - result.Y);
            return result;
        }

        #endregion

        #region SoundUtils

        /// <summary>
        /// 播放声音
        /// </summary>
        /// <param name="pos">声音播放的位置</param>
        /// <param name="sound">要播放的声音样式（SoundStyle）</param>
        /// <param name="volume">声音的音量</param>
        /// <param name="pitch">声音的音调</param>
        /// <param name="pitchVariance">音调的变化范围</param>
        /// <param name="maxInstances">最大实例数，允许同时播放的声音实例数量</param>
        /// <param name="soundLimitBehavior">声音限制行为，用于控制当达到最大实例数时的行为</param>
        /// <returns>返回声音实例的索引</returns>
        public static SlotId SoundPlayer(
            Vector2 pos,
            SoundStyle sound,
            float volume = 1,
            float pitch = 1,
            float pitchVariance = 1,
            int maxInstances = 1,
            SoundLimitBehavior soundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            ) {
            sound = sound with {
                Volume = volume,
                Pitch = pitch,
                PitchVariance = pitchVariance,
                MaxInstances = maxInstances,
                SoundLimitBehavior = soundLimitBehavior
            };

            SlotId sid = SoundEngine.PlaySound(sound, pos);
            return sid;
        }

        /// <summary>
        /// 更新声音位置
        /// </summary>
        public static void PanningSound(Vector2 pos, SlotId sid) {
            if (!SoundEngine.TryGetActiveSound(sid, out ActiveSound activeSound)) {
                return;
            }
            else {
                activeSound.Position = pos;
            }
        }

        #endregion
    }
}
