using CalamityOverhaul.Content;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        /// <summary>向游戏内打印对象 ToString</summary>
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

        /// <summary>向控制台打印对象 ToString 并换行</summary>
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

        // 已注释：Item 数组导出到文件
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

        /// <summary>蠕虫体节按 1/randomCount 概率返回 true</summary>
        public static bool FromWormBodysRandomSet(int targetNPCType, int randomCount) {
            return CWRLoad.WormBodys.Contains(targetNPCType) && !Main.rand.NextBool(randomCount);
        }
        /// <summary>蠕虫体节按 1/randomCount 概率返回 true</summary>
        public static bool FromWormBodysRandomSet(this NPC npc, int randomCount) => FromWormBodysRandomSet(npc.type, randomCount);

        /// <summary>是否蠕虫体节</summary>
        public static bool IsWormBody(this NPC npc) => CWRLoad.WormBodys.Contains(npc.type);

        /// <summary>按索引取 Player，非法或未存活返回 null</summary>
        public static Player GetPlayerInstance(int playerIndex) {
            if (playerIndex.ValidateIndex(Main.player)) {
                Player player = Main.player[playerIndex];

                return player.Alives() ? player : null;
            }
            else {
                return null;
            }
        }

        /// <summary>按索引取 NPC，非法或未存活返回 null</summary>
        public static NPC GetNPCInstance(int npcIndex) {
            if (npcIndex.ValidateIndex(Main.npc)) {
                NPC npc = Main.npc[npcIndex];

                return npc.Alives() ? npc : null;
            }
            else {
                return null;
            }
        }

        /// <summary>鞭类弹幕路径控制点</summary>
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

        /// <summary>弹幕旋转插值逼近目标角</summary>
        public static void EntityToRot(this Projectile entity, float targetRot, float rotSpeed) {
            entity.rotation = MathHelper.WrapAngle(entity.rotation);
            float diff = MathHelper.WrapAngle(targetRot - entity.rotation);
            entity.rotation += diff * rotSpeed;
        }

        /// <summary>同步 NPC 位置与旋转</summary>
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

        /// <summary>统计物品列表中指定 type 总数量</summary>
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

        /// <summary>统计玩家背包(可选银行)中指定物品数量</summary>
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

        /// <summary>统计玩家背包(可选银行)中多 type 总数量</summary>
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

        /// <summary>尝试取玩家 ADV 存档</summary>
        internal static bool TryGetADVSave(this Player player, out ADVSave save) {
            save = null;
            if (player.TryGetModPlayer<ADVSavePlayer>(out var advSavePlayer)) {
                save = advSavePlayer.ADVSave;
                return save != null;
            }
            return false;
        }

        /// <summary>尝试取玩家 HalibutPlayer</summary>
        internal static bool TryGetHalibutPlayer(this Player player, out HalibutPlayer halibutPlayer) {
            halibutPlayer = null;
            if (player.TryGetOverride(out halibutPlayer)) {
                return true;
            }
            return false;
        }

        /// <summary>玩家是否持有比目鱼传说武器</summary>
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

        /// <summary>灾厄物品 FullName 前缀</summary>
        public static string GetCalItem(string itemKey) => $"CalamityMod/{itemKey}";

        /// <summary>灾厄物品 type ID</summary>
        public static int GetCalItemID(string itemKey) => VaultUtils.GetItemTypeFromFullName(GetCalItem(itemKey));

        public static void ModifyLegendWeaponDamageFunc(Item item, int GetOnDamage, int GetStartDamage, ref StatModifier damage) {
            float oldMultiplicative = damage.Multiplicative;
            damage *= GetOnDamage / (float)GetStartDamage;
            damage /= oldMultiplicative;
            // SD 优先级不可靠，回缩到 item.damage 再乘前缀
            damage *= GetStartDamage / (float)item.damage;
            damage *= item.GetPrefixState().damageMult;
        }

        public static void ModifyLegendWeaponKnockbackFunc(Item item, float GetOnKnockback, float GetStartKnockback, ref StatModifier Knockback) {
            Knockback *= GetOnKnockback / (float)GetStartKnockback;
            // SD 优先级不可靠，回缩到 item.knockBack 再乘前缀
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

        /// <summary>赋予玩家无敌，类似 <see cref="Player.SetImmuneTimeForAllTypes(int)"/></summary>
        public static void GivePlayerImmuneState(this Player player, int time, bool blink = false) {
            player.immuneNoBlink = !blink;
            player.immune = true;
            player.immuneTime = time;
            for (int k = 0; k < player.hurtCooldowns.Length; k++) {
                player.hurtCooldowns[k] = player.immuneTime;
            }
        }

        /// <summary>用宿主本地化文本替换原版 Tooltip 行</summary>
        public static void OnModifyTooltips(Mod mod, List<TooltipLine> tooltips, LocalizedText value) {
            List<TooltipLine> newTooltips = new(tooltips);
            List<TooltipLine> overTooltips = [];
            List<TooltipLine> prefixTooltips = [];
            foreach (TooltipLine line in tooltips.ToList()) {
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
            tooltips.Clear();
            tooltips.AddRange(newTooltips);
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
                // throw new InvalidOperationException(message);
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
        /// <summary>指数缓出</summary>
        public static float EaseOutExpo(float t) => t >= 1f ? 1f : 1f - (float)Math.Pow(2, -10 * t);

        /// <summary>弹性缓出</summary>
        public static float EaseOutElastic(float t) {
            const float c4 = (2f * MathHelper.Pi) / 3f;
            return t == 0f ? 0f
                : t == 1f ? 1f
                : (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1);
        }

        /// <summary>三次缓出</summary>
        public static float EaseOutCubic(float t) {
            t = MathHelper.Clamp(t, 0, 1);
            t = 1 - (float)Math.Pow(1 - t, 3);
            return t;
        }

        /// <summary>三次缓出简化版</summary>
        public static float EaseOut(float t) {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }

        /// <summary>二次缓入</summary>
        public static float EaseInQuad(float t) => t * t;

        /// <summary>二次缓出</summary>
        public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>反向缓入缓出(带回弹)</summary>
        public static float EaseInOutBack(float t) {
            const float c1 = 1.70158f;
            const float c2 = c1 * 1.525f;
            t = MathHelper.Clamp(t, 0, 1);
            return t < 0.5f
                ? (float)(Math.Pow(2 * t, 2) * ((c2 + 1) * 2 * t - c2)) / 2f
                : (float)(Math.Pow(2 * t - 2, 2) * ((c2 + 1) * (t * 2 - 2) + c2) + 2) / 2f;
        }

        /// <summary>反向缓出(超调回弹)</summary>
        public static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }

        /// <summary>三次缓入缓出</summary>
        public static float EaseInOutCubic(float t) {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
        }

        /// <summary>三次缓入</summary>
        public static float EaseInCubic(float t) {
            return t * t * t;
        }

        /// <summary>二次缓入缓出</summary>
        public static float EaseInOutQuad(float t) {
            return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
        }

        /// <summary>二次贝塞尔曲线</summary>
        public static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        /// <summary>三次贝塞尔曲线</summary>
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

        /// <summary>正弦缓出</summary>
        public static float EaseOutSine(float t) {
            return (float)Math.Sin(t * MathHelper.PiOver2);
        }

        /// <summary>数组索引是否合法</summary>
        public static bool ValidateIndex(this int index, Array array) {
            return index >= 0 && index < array.Length;
        }
        #endregion

        #region DrawUtils
        /// <summary>按路径取 Texture2D</summary>
        public static Texture2D GetT2DValue(string texture, bool immediateLoad = false) => GetT2DAsset(texture, immediateLoad).Value;
        /// <summary>按路径取 Asset&lt;Texture2D&gt;，immediateLoad 同步加载</summary>
        public static Asset<Texture2D> GetT2DAsset(string texture, bool immediateLoad = false) {
            if (string.IsNullOrEmpty(texture) || !ModContent.HasAsset(texture)) {
                return VaultAsset.placeholder3;
            }
            return ModContent.Request<Texture2D>(texture
                , immediateLoad ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad);
        }

        #endregion

        #region 文本排版
        /// <summary>文本测量，字体缺失时保守兜底</summary>
        public static Vector2 MeasureText(string text, DynamicSpriteFont font, float scale = 1f) {
            if (string.IsNullOrEmpty(text)) {
                return Vector2.Zero;
            }
            font ??= Terraria.GameContent.FontAssets.MouseText?.Value;
            if (font == null) {
                return new Vector2(text.Length * 8f * scale, 16f * scale);
            }
            return font.MeasureString(text) * scale;
        }

        /// <summary>默认鼠标字体测量</summary>
        public static Vector2 MeasureText(string text, float scale = 1f)
            => MeasureText(text, Terraria.GameContent.FontAssets.MouseText?.Value, scale);

        /// <summary>CJK 感知自动换行，替代 Utils.WordwrapString</summary>
        public static List<string> WrapText(string text, DynamicSpriteFont font, float maxWidth
            , float scale = 1f, int maxLines = int.MaxValue, bool ellipsis = false) {
            List<string> result = [];
            if (string.IsNullOrEmpty(text)) {
                return result;
            }
            font ??= Terraria.GameContent.FontAssets.MouseText?.Value;
            if (font == null) {
                result.Add(text);
                return result;
            }

            // 折行在未缩放空间内计算
            float effWidth = scale > 0f ? maxWidth / scale : maxWidth;

            string normalized = text.Replace("\r", string.Empty);
            foreach (string block in normalized.Split('\n')) {
                if (string.IsNullOrEmpty(block)) {
                    result.Add(string.Empty);
                    continue;
                }
                WrapBlockCJKAware(block, font, effWidth, result);
            }

            if (maxLines < result.Count) {
                if (ellipsis && maxLines > 0) {
                    result[maxLines - 1] = AppendEllipsis(result[maxLines - 1], font, effWidth);
                }
                result.RemoveRange(maxLines, result.Count - maxLines);
            }

            return result;
        }

        /// <summary>默认鼠标字体换行</summary>
        public static List<string> WrapText(string text, float maxWidth
            , float scale = 1f, int maxLines = int.MaxValue, bool ellipsis = false)
            => WrapText(text, Terraria.GameContent.FontAssets.MouseText?.Value, maxWidth, scale, maxLines, ellipsis);

        /// <summary>换行结果转数组</summary>
        public static string[] WrapTextArray(string text, DynamicSpriteFont font, float maxWidth
            , float scale = 1f, int maxLines = int.MaxValue, bool ellipsis = false)
            => [.. WrapText(text, font, maxWidth, scale, maxLines, ellipsis)];

        /// <summary>与 Utils.WordwrapString 同签名，CJK 感知替代</summary>
        public static string[] WrapTextArray(string text, DynamicSpriteFont font, float maxWidth, int maxLines, out int lineCount) {
            string[] arr = [.. WrapText(text, font, maxWidth, 1f, maxLines)];
            lineCount = arr.Length;
            return arr;
        }

        /// <summary>换行后以换行符拼接</summary>
        public static string WrapTextJoin(string text, DynamicSpriteFont font, float maxWidth
            , float scale = 1f, int maxLines = int.MaxValue, bool ellipsis = false)
            => string.Join('\n', WrapText(text, font, maxWidth, scale, maxLines, ellipsis));

        private static string AppendEllipsis(string line, DynamicSpriteFont font, float maxWidth) {
            const string dots = "…";
            if (string.IsNullOrEmpty(line)) {
                return dots;
            }
            string trimmed = line.TrimEnd();
            while (trimmed.Length > 0 && font.MeasureString(trimmed + dots).X > maxWidth) {
                trimmed = trimmed[..^1].TrimEnd();
            }
            return trimmed + dots;
        }

        /// <summary>单段落 CJK 感知折行</summary>
        private static void WrapBlockCJKAware(string text, DynamicSpriteFont font, float maxWidth, List<string> output) {
            if (string.IsNullOrEmpty(text)) {
                output.Add(string.Empty);
                return;
            }
            if (maxWidth < 1f) {
                output.Add(text);
                return;
            }

            // CJK 参考宽度，测量偏小时用字高近似
            float fontHeight = font.MeasureString("A").Y;
            if (fontHeight < 1f) {
                fontHeight = 18f;
            }
            float cjkRefWidth = font.MeasureString("汉").X;
            float expectedCJKWidth = fontHeight * 0.95f;
            if (cjkRefWidth < expectedCJKWidth * 0.6f) {
                cjkRefWidth = expectedCJKWidth;
            }

            StringBuilder currentLine = new();
            float currentWidth = 0f;
            // 当前拉丁词在 currentLine 中的起始下标，-1 表示无
            int latinWordStart = -1;
            float latinWordWidth = 0f;

            for (int i = 0; i < text.Length; i++) {
                char ch = text[i];
                bool isCJK = IsCJKChar(ch);
                bool isWhite = char.IsWhiteSpace(ch);

                float charWidth;
                if (isCJK) {
                    float measured = font.MeasureString(ch.ToString()).X;
                    charWidth = Math.Max(measured, cjkRefWidth);
                }
                else {
                    charWidth = font.MeasureString(ch.ToString()).X;
                }

                bool needWrap = currentWidth + charWidth > maxWidth && currentLine.Length > 0;
                if (needWrap) {
                    // 超宽拉丁词硬断，避免死循环
                    if (isCJK || isWhite || latinWordStart <= 0) {
                        output.Add(currentLine.ToString().TrimEnd(' '));
                        currentLine.Clear();
                        currentWidth = 0f;
                        latinWordStart = -1;
                        latinWordWidth = 0f;
                        if (isWhite) {
                            continue;
                        }
                    }
                    else {
                        // 整词移到下一行
                        string head = currentLine.ToString(0, latinWordStart).TrimEnd(' ');
                        string tail = currentLine.ToString(latinWordStart, currentLine.Length - latinWordStart);
                        output.Add(head);
                        currentLine.Clear();
                        currentLine.Append(tail);
                        currentWidth = latinWordWidth;
                        latinWordStart = 0;
                    }
                }

                currentLine.Append(ch);
                currentWidth += charWidth;

                if (isCJK || isWhite) {
                    latinWordStart = -1;
                    latinWordWidth = 0f;
                }
                else {
                    if (latinWordStart < 0) {
                        latinWordStart = currentLine.Length - 1;
                        latinWordWidth = charWidth;
                    }
                    else {
                        latinWordWidth += charWidth;
                    }
                }
            }

            if (currentLine.Length > 0) {
                output.Add(currentLine.ToString());
            }
        }

        /// <summary>CJK 表意字符判定</summary>
        private static bool IsCJKChar(char c) {
            return c is >= '\u4E00' and <= '\u9FFF'
                or >= '\u3400' and <= '\u4DBF'
                or >= '\u3040' and <= '\u309F'
                or >= '\u30A0' and <= '\u30FF'
                or >= '\uAC00' and <= '\uD7AF'
                or >= '\uFF00' and <= '\uFFEF'
                or >= '\u3000' and <= '\u303F';
        }
        #endregion
    }
}
