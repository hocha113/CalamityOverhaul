using System.Collections.Generic;
using System.Text;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    /// <summary>
    /// 简易拼音辅助工具，支持常用汉字的拼音首字母和全拼匹配
    /// </summary>
    internal static class PinyinHelper
    {
        //Unicode区间对应拼音首字母的分界码点(GB2312顺序)
        private static readonly char[] PinyinInitials = [
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
            'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q',
            'R', 'S', 'T', 'W', 'X', 'Y', 'Z'
        ];

        private static readonly int[] PinyinBoundary = [
            0xB0A1, 0xB0C5, 0xB2C1, 0xB4EE, 0xB6EA,
            0xB7A2, 0xB8C1, 0xB9FE, 0xBBF7, 0xBFA6,
            0xC0AC, 0xC2E8, 0xC4C3, 0xC5B6, 0xC5BE,
            0xC6DA, 0xC8BB, 0xC8F6, 0xCBFA, 0xCDDA,
            0xCEF4, 0xD1B9, 0xD4D1
        ];

        //常用武器/物品相关汉字的全拼映射表
        private static readonly Dictionary<char, string> CommonPinyin = new() {
            // 武器相关
            {'剑', "jian"}, {'刀', "dao"}, {'枪', "qiang"}, {'弓', "gong"},
            {'弩', "nu"}, {'矛', "mao"}, {'戟', "ji"}, {'斧', "fu"},
            {'锤', "chui"}, {'鞭', "bian"}, {'杖', "zhang"}, {'棍', "gun"},
            {'弹', "dan"}, {'炮', "pao"}, {'箭', "jian"}, {'镰', "lian"},
            {'盾', "dun"}, {'甲', "jia"}, {'铠', "kai"}, {'靴', "xue"},

            // 材质/元素
            {'火', "huo"}, {'水', "shui"}, {'冰', "bing"}, {'雷', "lei"},
            {'风', "feng"}, {'土', "tu"}, {'光', "guang"}, {'暗', "an"},
            {'毒', "du"}, {'血', "xue"}, {'骨', "gu"}, {'魂', "hun"},
            {'星', "xing"}, {'月', "yue"}, {'日', "ri"}, {'天', "tian"},
            {'地', "di"}, {'海', "hai"}, {'山', "shan"}, {'雪', "xue"},
            {'雨', "yu"}, {'云', "yun"}, {'雾', "wu"}, {'电', "dian"},

            // 品质/修饰
            {'大', "da"}, {'小', "xiao"}, {'强', "qiang"}, {'弱', "ruo"},
            {'圣', "sheng"}, {'魔', "mo"}, {'神', "shen"}, {'鬼', "gui"},
            {'龙', "long"}, {'虎', "hu"}, {'凤', "feng"}, {'鹰', "ying"},
            {'蛇', "she"}, {'狼', "lang"}, {'熊', "xiong"}, {'鹿', "lu"},
            {'金', "jin"}, {'银', "yin"}, {'铜', "tong"}, {'铁', "tie"},
            {'钢', "gang"}, {'玉', "yu"}, {'石', "shi"}, {'木', "mu"},

            // 常用动词/形容词
            {'破', "po"}, {'碎', "sui"}, {'灭', "mie"}, {'斩', "zhan"},
            {'裂', "lie"}, {'爆', "bao"}, {'燃', "ran"}, {'冻', "dong"},
            {'毁', "hui"}, {'灾', "zai"}, {'怒', "nu"}, {'狂', "kuang"},
            {'烈', "lie"}, {'炎', "yan"}, {'焰', "yan"}, {'焚', "fen"},

            // 灾厄mod常见
            {'硫', "liu"}, {'磺', "huang"}, {'瘟', "wen"}, {'疫', "yi"},
            {'渊', "yuan"}, {'深', "shen"}, {'极', "ji"}, {'至', "zhi"},
            {'远', "yuan"}, {'古', "gu"}, {'终', "zhong"}, {'末', "mo"},
            {'混', "hun"}, {'沌', "dun"}, {'虚', "xu"}, {'空', "kong"},
            {'世', "shi"}, {'界', "jie"}, {'梦', "meng"}, {'幻', "huan"},

            // 其他常用
            {'的', "de"}, {'了', "le"}, {'是', "shi"}, {'不', "bu"},
            {'在', "zai"}, {'有', "you"}, {'和', "he"}, {'人', "ren"},
            {'这', "zhe"}, {'中', "zhong"}, {'上', "shang"}, {'下', "xia"},
            {'左', "zuo"}, {'右', "you"}, {'前', "qian"}, {'后', "hou"},
            {'武', "wu"}, {'器', "qi"}, {'装', "zhuang"}, {'备', "bei"},
            {'修', "xiu"}, {'改', "gai"}, {'管', "guan"}, {'理', "li"},
            {'设', "she"}, {'置', "zhi"}, {'开', "kai"}, {'关', "guan"},
            {'启', "qi"}, {'用', "yong"}, {'禁', "jin"}, {'止', "zhi"},
            {'全', "quan"}, {'部', "bu"}, {'选', "xuan"}, {'择', "ze"},
            {'搜', "sou"}, {'索', "suo"}, {'查', "cha"}, {'找', "zhao"},
            {'名', "ming"}, {'称', "cheng"}, {'类', "lei"}, {'型', "xing"},
            {'红', "hong"}, {'蓝', "lan"}, {'绿', "lv"}, {'黄', "huang"},
            {'紫', "zi"}, {'白', "bai"}, {'黑', "hei"}, {'灰', "hui"},
            {'晶', "jing"}, {'钻', "zuan"}, {'宝', "bao"}, {'珠', "zhu"},
            {'链', "lian"}, {'环', "huan"}, {'带', "dai"}, {'帽', "mao"},
            {'衣', "yi"}, {'裤', "ku"}, {'翼', "yi"}, {'角', "jiao"},
            {'牙', "ya"}, {'爪', "zhua"}, {'鳞', "lin"}, {'羽', "yu"},
            {'真', "zhen"}, {'伪', "wei"}, {'暴', "bao"}, {'怒', "nu"},
            {'无', "wu"}, {'尽', "jin"}, {'永', "yong"}, {'恒', "heng"},
            {'死', "si"}, {'生', "sheng"}, {'亡', "wang"}, {'活', "huo"},
            {'阳', "yang"}, {'阴', "yin"}, {'寒', "han"}, {'热', "re"},
            {'腐', "fu"}, {'蚀', "shi"}, {'锈', "xiu"}, {'朽', "xiu"},
            {'苍', "cang"}, {'穹', "qiong"}, {'彼', "bi"}, {'岸', "an"},
        };

        /// <summary>
        /// 获取字符串中所有汉字的拼音首字母(小写)
        /// </summary>
        public static string GetInitials(string text) {
            if (string.IsNullOrEmpty(text)) return "";

            StringBuilder sb = new();
            foreach (char c in text) {
                if (c >= 0x4E00 && c <= 0x9FFF) {
                    sb.Append(char.ToLowerInvariant(GetInitial(c)));
                }
                else if (char.IsLetterOrDigit(c)) {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 获取字符串的全拼(小写，无空格分隔)
        /// </summary>
        public static string GetFullPinyin(string text) {
            if (string.IsNullOrEmpty(text)) return "";

            StringBuilder sb = new();
            foreach (char c in text) {
                if (CommonPinyin.TryGetValue(c, out string pinyin)) {
                    sb.Append(pinyin);
                }
                else if (c >= 0x4E00 && c <= 0x9FFF) {
                    sb.Append(char.ToLowerInvariant(GetInitial(c)));
                }
                else if (char.IsLetterOrDigit(c)) {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 获取单个汉字的拼音首字母
        /// </summary>
        private static char GetInitial(char ch) {
            //将Unicode字符转换为GB2312编码来判断拼音首字母
            byte[] bytes;
            try {
                bytes = System.Text.Encoding.GetEncoding("GB2312").GetBytes(ch.ToString());
            }
            catch {
                return ch;
            }

            if (bytes.Length < 2) return char.ToUpperInvariant(ch);

            int code = bytes[0] * 256 + bytes[1];

            for (int i = PinyinBoundary.Length - 1; i >= 0; i--) {
                if (code >= PinyinBoundary[i]) {
                    return PinyinInitials[i];
                }
            }

            //如果在映射表中有，直接取首字母
            if (CommonPinyin.TryGetValue(ch, out string pinyin) && pinyin.Length > 0) {
                return char.ToUpperInvariant(pinyin[0]);
            }

            return ch;
        }
    }
}
