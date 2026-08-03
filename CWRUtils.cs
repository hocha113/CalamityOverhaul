using CalamityOverhaul.Content;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using ReLogic.Graphics;
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
        /// <summary>游戏内打印 ToString</summary>
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

        /// <summary>控制台打印 ToString</summary>
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

        //旧 Item 数组导出
        //public static void ExportItemTypesToFile(Item[] items, string path = "D:\\Mod_Resource\\input.cs") {
        //try {
        //int columnIndex = 0;
        //using System.IO.StreamWriter sw = new(path);
        //sw.Write("string[] fullItems = new string[] {");
        //foreach (Item item in items) {
        //columnIndex++;
        ////根据是否有 ModItem 决定写入的内容
        //string itemInfo = item.ModItem == null ? $"\"{item.type}\"" : $"\"{item.ModItem.FullName}\"";
        //sw.Write(itemInfo);
        //sw.Write(", ");
        ////每行最多写入9个元素，然后换行
        //if (columnIndex >= 9) {
        //sw.WriteLine();
        //columnIndex = 0;
        //}
        //}
        //sw.Write("};");
        //} catch (UnauthorizedAccessException) {
        //CWRMod.Instance.Logger.Info($"UnauthorizedAccessException: 无法访问文件路径 '{path}'. 权限不足");
        //} catch (System.IO.DirectoryNotFoundException) {
        //CWRMod.Instance.Logger.Info($"DirectoryNotFoundException: 文件路径 '{path}' 中的目录不存在");
        //} catch (System.IO.PathTooLongException) {
        //CWRMod.Instance.Logger.Info($"PathTooLongException: 文件路径 '{path}' 太长");
        //} catch (System.IO.IOException) {
        //CWRMod.Instance.Logger.Info($"IOException: 无法打开文件 '{path}' 进行写入");
        //} catch (Exception e) {
        //CWRMod.Instance.Logger.Info($"An error occurred: {e.Message}");
        //}
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

        /// <summary>蠕虫体节 1/randomCount</summary>
        public static bool FromWormBodysRandomSet(int targetNPCType, int randomCount) {
            return CWRLoad.WormBodys.Contains(targetNPCType) && !Main.rand.NextBool(randomCount);
        }
        /// <summary>蠕虫体节 1/randomCount</summary>
        public static bool FromWormBodysRandomSet(this NPC npc, int randomCount) => FromWormBodysRandomSet(npc.type, randomCount);

        /// <summary>蠕虫体节</summary>
        public static bool IsWormBody(this NPC npc) => CWRLoad.WormBodys.Contains(npc.type);

        /// <summary>鞭路径控制点</summary>
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

        public static void EntityToRot(this NPC entity, float toRot, float rotSpeed) => entity.rotation = entity.rotation.RotTowards(toRot, rotSpeed);

        /// <summary>弹幕旋转逼近</summary>
        public static void EntityToRot(this Projectile entity, float targetRot, float rotSpeed) {
            entity.rotation = MathHelper.WrapAngle(entity.rotation);
            float diff = MathHelper.WrapAngle(targetRot - entity.rotation);
            entity.rotation += diff * rotSpeed;
        }

        /// <summary>同步 NPC position/rotation</summary>
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

        /// <summary>列表内指定 type 总量</summary>
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

        /// <summary>背包(+银行)指定物品量</summary>
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

        /// <summary>背包(+银行)多 type 总量</summary>
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

        /// <summary>取 HalibutPlayer</summary>
        internal static bool TryGetHalibutPlayer(this Player player, out HalibutPlayer halibutPlayer) {
            halibutPlayer = null;
            if (player.TryGetOverride(out halibutPlayer)) {
                return true;
            }
            return false;
        }

        /// <summary>持有比目鱼</summary>
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

        /// <summary>灾厄 FullName</summary>
        public static string GetCalItem(string itemKey) => $"CalamityMod/{itemKey}";

        /// <summary>灾厄 item type</summary>
        public static int GetCalItemID(string itemKey) => VaultUtils.GetItemTypeFromFullName(GetCalItem(itemKey));

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

        /// <summary>终局站台，嘉登熔炉缺席则远古操纵机</summary>
        public static Recipe AddEndgameStation(this Recipe recipe) =>
            recipe.AddTile(CWRID.Tile_DraedonsForge > 0 ? CWRID.Tile_DraedonsForge : TileID.LunarCraftingStation);

        /// <summary>替换 Tooltip 行为宿主本地化</summary>
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
                //throw new InvalidOperationException(message);
                return null;
            }
            return item.GetGlobalItem<CWRItem>();
        }

        public static CWRProjectile CWR(this Projectile projectile) {
            return projectile.GetGlobalProjectile<CWRProjectile>();
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

        #region TextLayout

        public static string[] WrapTextArray(string text, DynamicSpriteFont font,
            int maxWidth, int maxLines, out int lineCount)
            => VaultUtils.WrapTextArray(text, font, maxWidth, maxLines, out lineCount);

        #endregion

        #region MathUtils
        /// <summary>EaseOutElastic</summary>
        public static float EaseOutElastic(float t) {
            const float c4 = (2f * MathHelper.Pi) / 3f;
            return t == 0f ? 0f
                : t == 1f ? 1f
                : (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1);
        }
        #endregion

        #region DrawUtils
        /// <summary>按路径取 Texture2D，immediateLoad 同步</summary>
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
