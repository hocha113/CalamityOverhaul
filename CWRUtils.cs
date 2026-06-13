using CalamityOverhaul.Content;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace CalamityOverhaul
{
    public static class CWRUtils
    {
        #region System
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

        /// //<summary>
        /// //将 Item 数组的信息写入指定路径的文件中
        /// //</summary>
        /// //<param name="items">要导出的 Item 数组</param>
        /// //<param name="path">写入文件的路径，默认为 "D:\\Mod_Resource\\input.cs"</param>
        //public static void ExportItemTypesToFile(Item[] items, string path = "D:\\Mod_Resource\\input.cs") {
        //  try {
        //      int columnIndex = 0;
        //      using System.IO.StreamWriter sw = new(path);
        //      sw.Write("string[] fullItems = new string[] {");
        //      foreach (Item item in items) {
        //          columnIndex++;
        //          //根据是否有 ModItem 决定写入的内容
        //          string itemInfo = item.ModItem == null ? $"\"{item.type}\"" : $"\"{item.ModItem.FullName}\"";
        //          sw.Write(itemInfo);
        //          sw.Write(", ");
        //          //每行最多写入9个元素，然后换行
        //          if (columnIndex >= 9) {
        //              sw.WriteLine();
        //              columnIndex = 0;
        //          }
        //      }
        //      sw.Write("};");
        //  } catch (UnauthorizedAccessException) {
        //      CWRMod.Instance.Logger.Info($"UnauthorizedAccessException: 无法访问文件路径 '{path}'. 权限不足");
        //  } catch (System.IO.DirectoryNotFoundException) {
        //      CWRMod.Instance.Logger.Info($"DirectoryNotFoundException: 文件路径 '{path}' 中的目录不存在");
        //  } catch (System.IO.PathTooLongException) {
        //      CWRMod.Instance.Logger.Info($"PathTooLongException: 文件路径 '{path}' 太长");
        //  } catch (System.IO.IOException) {
        //      CWRMod.Instance.Logger.Info($"IOException: 无法打开文件 '{path}' 进行写入");
        //  } catch (Exception e) {
        //      CWRMod.Instance.Logger.Info($"An error occurred: {e.Message}");
        //  }
        //}

        public static Type[] GetModTypes(Mod mod) => AssemblyManager.GetLoadableTypes(mod.Code);

        public static Type GetTargetTypeInStringKey(Type[] types, string key) {
            Type reset = null;
            foreach (Type type in types) {
                if (type.Name == key) {
                    reset = type;
                }
            }
            return reset;
        }

        private static string FailedLoadMessage => VaultUtils.Translation("未成功加载", "Failed load");

        private static string VerificationMessage => VaultUtils.Translation("是否是", "whether it is");

        private static string ChangeStatusMessage => VaultUtils.Translation("已经改动?", "Has it been changed?");

        private static string ModNotLoadedMessage => VaultUtils.Translation("未加载模组", "The mod is not loaded");

        internal static void LogFailedLoad(string value1, string value2)
            => CWRMod.Instance.Logger.Info($"{FailedLoadMessage} {value1} {VerificationMessage} {value2} {ChangeStatusMessage}");

        internal static void LogModNotLoaded(string value1) => CWRMod.Instance.Logger.Info($"{ModNotLoadedMessage} {value1}");
        #endregion

        #region AIUtils

        #region 工具部分

        public const float atoR = MathHelper.Pi / 180;

        public static float AtoR(this float num) => num * atoR;

        public static float RtoA(this float num) => num / atoR;

        public static void SetArrowRot(int proj) => Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation() + MathHelper.PiOver2;
        public static void SetArrowRot(this Projectile proj) => proj.rotation = proj.velocity.ToRotation() + MathHelper.PiOver2;

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

        public static void EntityToRot(this NPC entity, float toRot, float rotSpeed) => entity.rotation = ToRot(entity.rotation, toRot, rotSpeed);

        public static float ToRot(float setRot, float toRot, float rotSpeed) {
            setRot = MathHelper.WrapAngle(setRot);
            float diff = MathHelper.WrapAngle(toRot - setRot);
            return setRot + MathHelper.Clamp(diff, -rotSpeed, rotSpeed);
        }

        /// <summary>
        /// 处理实体的旋转行为
        /// </summary>
        public static void EntityToRot(this Projectile entity, float targetRot, float rotSpeed) {
            entity.rotation = MathHelper.WrapAngle(entity.rotation);
            float diff = MathHelper.WrapAngle(targetRot - entity.rotation);
            entity.rotation += diff * rotSpeed;
        }

        /// <summary>
        /// 在必要的时候使用这个发送NPC基本数据
        /// </summary>
        /// <param name="npc"></param>
        public static void SendNPCbasicData(this NPC npc, int player = -1) {
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.NPCbasicData);
            modPacket.Write((byte)npc.whoAmI);
            modPacket.WriteVector2(npc.position);
            modPacket.Write(npc.rotation);
            modPacket.Send(player);
        }

        #endregion

        #endregion

        #region GameUtils
        public static bool IsTool(this Item item) => item.pick > 0 || item.axe > 0 || item.hammer > 0;

        public static DamageClass GiveMeleeType(bool isGiveTrueMelee = false) => isGiveTrueMelee ? CWRRef.GetTrueMeleeDamageClass() : DamageClass.Melee;

        public static bool IsWaterBucket(this Item item) => item.type == ItemID.WaterBucket || item.type == ItemID.BottomlessBucket;

        public static IItemDropRule SimpleAdd(this ILoot loot, int itemID, int dropRateInt = 1, int minQuantity = 1, int maxQuantity = 1) {
            var rule = ItemDropRule.Common(itemID, dropRateInt, minQuantity, maxQuantity);
            return loot.Add(rule);
        }

        public static IItemDropRule SimpleAdd(this LeadingConditionRule mainRule, int itemID, int dropRateInt = 1, int minQuantity = 1, int maxQuantity = 1, bool hideLootReport = false) {
            var rule = ItemDropRule.Common(itemID, dropRateInt, minQuantity, maxQuantity);
            return mainRule.OnSuccess(rule, hideLootReport);
        }

        /// <summary>
        /// 查询指定物品数量
        /// </summary>
        /// <param name="items"></param>
        /// <param name="itemTypes"></param>
        /// <returns></returns>
        public static int InquireItem(this IList<Item> items, params HashSet<int> itemTypes) {
            int num = 0;
            foreach (var item in items.ToList()) {
                if (!item.Alives()) {
                    continue;
                }
                if (itemTypes.Contains(item.type)) {
                    num += item.stack;
                }
            }
            return num;
        }

        /// <summary>
        /// 查询玩家拥有的指定物品数量
        /// </summary>
        /// <param name="player"></param>
        /// <param name="itemType"></param>
        /// <param name="checkBank"></param>
        /// <returns></returns>
        public static int InquireItem(this Player player, int itemType, bool checkBank = false) {
            int num = player.inventory.InquireItem(itemType);
            if (checkBank) {
                num += player.bank.item.InquireItem(itemType);
                num += player.bank2.item.InquireItem(itemType);
                num += player.bank3.item.InquireItem(itemType);
                num += player.bank4.item.InquireItem(itemType);
            }
            return num;
        }

        /// <summary>
        /// 查询玩家拥有的指定物品数量
        /// </summary>
        /// <param name="player"></param>
        /// <param name="checkBank"></param>
        /// <param name="itemTypes"></param>
        /// <returns></returns>
        public static int InquireItem(this Player player, bool checkBank, params HashSet<int> itemTypes) {
            int num = player.inventory.InquireItem(itemTypes);
            if (checkBank) {
                num += player.bank.item.InquireItem(itemTypes);
                num += player.bank2.item.InquireItem(itemTypes);
                num += player.bank3.item.InquireItem(itemTypes);
                num += player.bank4.item.InquireItem(itemTypes);
            }
            return num;
        }

        /// <summary>
        /// 尝试获取玩家的ADV存档实例
        /// </summary>
        /// <param name="player"></param>
        /// <param name="save"></param>
        /// <returns></returns>
        internal static bool TryGetADVSave(this Player player, out ADVSave save) {
            save = null;
            if (player.TryGetModPlayer<ADVSavePlayer>(out var advSavePlayer)) {
                save = advSavePlayer.ADVSave;
                return save != null;
            }
            return false;
        }

        /// <summary>
        /// 尝试获取玩家的HalibutPlayer实例
        /// </summary>
        /// <param name="player"></param>
        /// <param name="halibutPlayer"></param>
        /// <returns></returns>
        internal static bool TryGetHalibutPlayer(this Player player, out HalibutPlayer halibutPlayer) {
            halibutPlayer = null;
            if (player.TryGetOverride(out halibutPlayer)) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 玩家是否拥有比目鱼传说武器
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        internal static bool HasHalibut(this Player player) => player.TryGetHalibutPlayer(out var halibutPlayer) && halibutPlayer.HasHalubut;

        public static void SetItemLegendContentTops(ref List<TooltipLine> tooltips, string itemKey) {
            TooltipLine legendtops = tooltips.FirstOrDefault((TooltipLine x) => x.Text.Contains("[legend]") && x.Mod == "Terraria");
            if (legendtops != null) {
                KeyboardState state = Keyboard.GetState();
                if ((state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift))) {
                    legendtops.Text = Language.GetTextValue($"Mods.CalamityOverhaul.Items.{itemKey}.Legend");
                    legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                }
                else {
                    legendtops.Text = Content.CWRItem.ItemLegendOnMouseLang.Value;
                    legendtops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.Gold, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                }
            }
        }

        /// <summary>
        /// 获取来自灾厄的物品名
        /// </summary>
        /// <param name="itemKey"></param>
        /// <returns></returns>
        public static string GetCalItem(string itemKey) => $"CalamityMod/{itemKey}";

        /// <summary>
        /// 获取来自灾厄的物品ID
        /// </summary>
        /// <param name="itemKey"></param>
        /// <returns></returns>
        public static int GetCalItemID(string itemKey) => VaultUtils.GetItemTypeFromFullName(GetCalItem(itemKey));

        public static void ModifyLegendWeaponDamageFunc(Item item, int GetOnDamage, int GetStartDamage, ref StatModifier damage) {
            float oldMultiplicative = damage.Multiplicative;
            damage *= GetOnDamage / (float)GetStartDamage;
            damage /= oldMultiplicative;
            //首先，因为SD的运行优先级并不可靠，有的模组的修改在SD之后运行，比如炼狱模式，这个基础伤害缩放保证一些情况不会发生
            damage *= GetStartDamage / (float)item.damage;
            damage *= item.GetPrefixState().damageMult;
        }

        public static void ModifyLegendWeaponKnockbackFunc(Item item, float GetOnKnockback, float GetStartKnockback, ref StatModifier Knockback) {
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
            .AddOnCraftCallback(CWRCrafted.SpawnAction);

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
        /// 快速修改一个物品的简介文本，从宿主类的本地化字段中拉取资源
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
            tooltips.Clear(); //清空原 tooltips 集合
            tooltips.AddRange(newTooltips); //添加修改后的 newTooltips 集合
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

        public static CWRItem CWR(this Item item) {
            if (item.type == ItemID.None) {
                string message = "ERROR: An Empty Transfer Occurred! The Value of Item.type is Zero!";
                VaultUtils.Text(message, Color.Red);
                CWRMod.Instance.Logger.Error(message);
                //throw new InvalidOperationException(message); //明确终止执行，抛出异常              
                return null;
            }
            return item.GetGlobalItem<CWRItem>();
        }

        public static CWRProjectile CWR(this Projectile projectile) {
            return projectile.GetGlobalProjectile<CWRProjectile>();
        }

        public static void Initialize(this Item item) {
            if (item.CWR().ai == null) {
                item.CWR().ai = [0, 0, 0];
            }
        }

        public static void BlastingSputteringDust(Projectile Projectile, int dustID1, int dustID2, int dustID3, int dustID4, int dustID5) {
            for (int i = 0; i < 40; i++) {
                int idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustID1, 0f, 0f, 100, default, 2f);
                Main.dust[idx].velocity *= 3f;
                if (Main.rand.NextBool()) {
                    Main.dust[idx].scale = 0.5f;
                    Main.dust[idx].fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                }
            }
            for (int i = 0; i < 70; i++) {
                int idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustID2, 0f, 0f, 100, default, 3f);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].velocity *= 5f;
                idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustID3, 0f, 0f, 100, default, 2f);
                Main.dust[idx].velocity *= 2f;
            }
            Vector2 ver = Projectile.velocity * -1;
            for (int i = 0; i < 70; i++) {
                int idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustID4, 0f, 0f, 100, default, 3f);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].velocity = ver.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.2f, 3.6f);
                idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustID5, 0f, 0f, 100, default, 2f);
                Main.dust[idx].velocity *= ver.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.2f, 1.6f);
            }
        }

        public static void SplashDust(Projectile Projectile, int mode, int dustID1, int dustID2, float speed, Color dustColor, ArmorShaderData shader = null) {
            for (int i = 4; i < mode; i++) {
                Vector2 vector = Projectile.velocity.UnitVector() * speed;
                float oldXPos = vector.X * (30f / i);
                float oldYPos = vector.Y * (30f / i);
                int killDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - oldXPos, Projectile.oldPosition.Y - oldYPos), 2, 2
                    , dustID1, vector.X, vector.Y, 100, default, 1.8f);
                Main.dust[killDust].noGravity = true;
                Dust dust2 = Main.dust[killDust];
                dust2.velocity *= 0.5f;
                dust2.color = dustColor;
                if (shader != null) {
                    dust2.shader = shader;
                    dust2.shader.UseColor(dust2.color);
                }
                killDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - oldXPos, Projectile.oldPosition.Y - oldYPos), 2, 2
                    , dustID2, vector.X, vector.Y, 100, default, 1.4f);
                dust2 = Main.dust[killDust];
                dust2.velocity *= 0.05f;
                dust2.noGravity = true;
            }
        }

        public static void SpanCycleDust(Projectile Projectile, int dustID1, int dustID2) {
            for (int i = 0; i < 1; i++) {
                if (Main.rand.NextBool()) {
                    Vector2 vector3 = Vector2.UnitY.RotatedByRandom(MathHelper.TwoPi);
                    Dust dust = Main.dust[Dust.NewDust(Projectile.Center - vector3 * 30f, 0, 0, dustID1)];
                    dust.noGravity = true;
                    dust.position = Projectile.Center - vector3 * Main.rand.Next(10, 21);
                    dust.velocity = vector3.RotatedBy(MathHelper.PiOver2) * 6f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                    vector3 = Vector2.UnitY.RotatedByRandom(MathHelper.TwoPi);
                    dust.noGravity = true;
                    dust.position = Projectile.Center - vector3 * Main.rand.Next(10, 21);
                    dust.velocity = vector3.RotatedBy(MathHelper.PiOver2) * 6f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                    dust.color = Color.Crimson;
                }
                else {
                    Vector2 vector4 = Vector2.UnitY.RotatedByRandom(MathHelper.TwoPi);
                    Dust dust = Main.dust[Dust.NewDust(Projectile.Center - vector4 * 30f, 0, 0, dustID2)];
                    dust.noGravity = true;
                    dust.position = Projectile.Center - vector4 * Main.rand.Next(20, 31);
                    dust.velocity = vector4.RotatedBy(-MathHelper.PiOver2) * 5f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                }
            }
        }

        #endregion

        #region MathUtils
        /// <summary>
        /// 指数缓出函数
        /// 速度起初极快并迅速减缓，在接近结束时趋于平缓
        /// 常用于需要强烈减速感的动画
        /// </summary>
        public static float EaseOutExpo(float t) => t >= 1f ? 1f : 1f - (float)Math.Pow(2, -10 * t);

        /// <summary>
        /// 计算平滑的缓动函数
        /// </summary>
        public static float EaseOutElastic(float t) {
            const float c4 = (2f * MathHelper.Pi) / 3f;
            return t == 0f ? 0f
                : t == 1f ? 1f
                : (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1);
        }

        /// <summary>
        /// 三次缓出函数
        /// 起初快速加速，随后平滑减速
        /// 常用于自然的物体停止效果
        /// </summary>
        public static float EaseOutCubic(float t) {
            t = MathHelper.Clamp(t, 0, 1);
            t = 1 - (float)Math.Pow(1 - t, 3);
            return t;
        }

        /// <summary>
        /// 三次缓出函数的简化版
        /// 功能等同于 EaseOutCubic
        /// </summary>
        public static float EaseOut(float t) {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }

        /// <summary>
        /// 二次缓入函数
        /// 从慢到快加速，适合平滑启动的动画
        /// </summary>
        public static float EaseInQuad(float t) => t * t;

        /// <summary>
        /// 二次缓出函数
        /// 从快到慢减速，适合平滑停止的动画
        /// </summary>
        public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>
        /// 反向缓入缓出函数
        /// 在开始和结束阶段略有“回弹”效果
        /// 常用于强调弹性或动感的过渡
        /// </summary>
        public static float EaseInOutBack(float t) {
            const float c1 = 1.70158f;
            const float c2 = c1 * 1.525f;
            t = MathHelper.Clamp(t, 0, 1);
            return t < 0.5f
                ? (float)(Math.Pow(2 * t, 2) * ((c2 + 1) * 2 * t - c2)) / 2f
                : (float)(Math.Pow(2 * t - 2, 2) * ((c2 + 1) * (t * 2 - 2) + c2) + 2) / 2f;
        }

        /// <summary>
        /// 反向缓出函数
        /// 在结束阶段会略微超出目标后反弹回终点
        /// 常用于产生弹性离场的视觉效果
        /// </summary>
        public static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }

        /// <summary>
        /// 三次缓入缓出函数
        /// 前半段加速 后半段减速
        /// 常用于平滑的镜像对称运动
        /// </summary>
        public static float EaseInOutCubic(float t) {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
        }

        /// <summary>
        /// 三次缓入函数
        /// 起始阶段缓慢 加速度随时间增加
        /// </summary>
        public static float EaseInCubic(float t) {
            return t * t * t;
        }

        /// <summary>
        /// 二次缓入缓出函数
        /// 前半部分加速 后半部分减速
        /// 常用于平滑自然的过渡动画
        /// </summary>
        public static float EaseInOutQuad(float t) {
            return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
        }

        /// <summary>
        /// 二次贝塞尔曲线
        /// 由三个控制点定义的平滑曲线
        /// 用于简单插值或平滑路径计算
        /// </summary>
        public static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        /// <summary>
        /// 三次贝塞尔曲线
        /// 由四个控制点定义的高阶平滑曲线
        /// 适用于复杂轨迹与自然运动插值
        /// </summary>
        public static Vector2 CubicBezier(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3) {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            Vector2 p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;
            return p;
        }

        /// <summary>
        /// 正弦缓出函数
        /// 使用正弦曲线模拟平滑的停止运动
        /// 常用于自然的轻缓收尾效果
        /// </summary>
        public static float EaseOutSine(float t) {
            return (float)Math.Sin(t * MathHelper.PiOver2);
        }

        /// <summary>
        /// 检测索引的合法性
        /// </summary>
        /// <returns>合法将返回 <see cref="true"/></returns>
        public static bool ValidateIndex(this int index, Array array) {
            return index >= 0 && index < array.Length;
        }
        #endregion

        #region DrawUtils
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
        public static Texture2D GetT2DValue(string texture, bool immediateLoad = false) => GetT2DAsset(texture, immediateLoad).Value;
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
            if (string.IsNullOrEmpty(texture) || !ModContent.HasAsset(texture)) {
                return VaultAsset.placeholder3;
            }
            return ModContent.Request<Texture2D>(texture
                , immediateLoad ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad);
        }

        #endregion
    }
}
