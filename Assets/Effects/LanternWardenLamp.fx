// ============================================================================
//LanternWardenLamp.fx 提灯巡守的金属灯具+灯焰材质(TechLamp)
//s0=灯具物品贴图(游戏内 ChainLantern,单帧物品图,无帧表渗色问题),按亮度域盲拆分:
//  亮暖像素=玻璃灯窗 → 内焰双频火舌翻滚 + 呼吸闪变 + uFlash 白热过曝(≤一拍)
//  暗像素=锈铁框架 → 保色调受光 + 邻域玻璃透光染暖(灯从内部照亮自己的框) + glint 巡缘扫光
//  轮廓 rim=4-tap alpha 边检,下半加权(焰光从灯内向下勾金属外缘)
//盲校准的意义:不依赖具体贴图布局,换任何灯具贴图自适应
//
//环境光合同:金属吃满 vc.rgb(drawColor),玻璃焰/rim/glint 为自发光,75% 免疫环境光
//预乘输出,进 AlphaBlend 批(Immediate,消费端上参后 Apply 再 Draw,s0 由 SpriteBatch.Draw 绑定)
//坐标全笛卡尔无 atan2;直线算术+普通 tex2D,无动态分支,FNA3D 安全
//绑定噪声 PerlinNoise 实测值域 0.227~0.776,阈值一律过 nrm() 归一
//消费入口 Content/Scenarios/Dungeonworld/NPCs/Elites/LanternWarden.cs (DrawLanternBody)
// ============================================================================

sampler uImage0 : register(s0);   //灯具物品贴图
sampler uNoiseTex : register(s1); //PerlinNoise,LinearWrap

float uTime;      //秒
float uSeed;      //个体相位
float uLevel;     //灯焰强度 0..1.6(FlameLevel 直喂)
float uAlert;     //警觉 0..1(焰体高频双闪)
float uFlash;     //白热过曝 0..1(鸣警首拍/三响 recoil,消费端保证 ≤ 数帧)
float2 uTexSize;  //贴图像素尺寸(texel 折算)

static const float3 LAMP_WARM = float3(1.000, 0.706, 0.353);
static const float3 LAMP_CORE = float3(1.000, 0.902, 0.667);
static const float3 FLASH_HOT = float3(1.000, 0.980, 0.920);

//绑定噪声实测值域归一(0.227~0.776)
float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

//亮暖像素=玻璃灯窗(对预乘贴图,内容区 a≈1,亮度即本色亮度)
float glassMask(float3 rgb) {
    float luma = dot(rgb, float3(0.299, 0.587, 0.114));
    return smoothstep(0.30, 0.52, luma) * smoothstep(0.04, 0.16, rgb.r - rgb.b);
}

float4 PSLamp(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uImage0, uv);
    float luma = dot(src.rgb, float3(0.299, 0.587, 0.114));
    float glassM = glassMask(src.rgb);
    float metalM = src.a * (1.0 - glassM);

    //邻域 4-tap:同批采样喂两件事——alpha 边检(rim)与玻璃透光染暖(neighborGlow)
    float2 off = 1.8 / uTexSize;
    float4 tL = tex2D(uImage0, uv - float2(off.x, 0.0));
    float4 tR = tex2D(uImage0, uv + float2(off.x, 0.0));
    float4 tU = tex2D(uImage0, uv - float2(0.0, off.y));
    float4 tD = tex2D(uImage0, uv + float2(0.0, off.y));
    float edge = saturate(src.a * 4.0 - tL.a - tR.a - tU.a - tD.a);
    float nbGlow = (glassMask(tL.rgb) + glassMask(tR.rgb)
        + glassMask(tU.rgb) + glassMask(tD.rgb)) * 0.25;

    //焰体时域:常态慢呼吸 + 警觉 ~11Hz 双闪(峰值过冲,与灯锥同语汇)
    float breath = 0.90 + 0.10 * sin(uTime * 9.3 + uSeed);
    float flick = breath * lerp(1.0, 0.82 + 0.38 * sin(uTime * 72.0 + uSeed), uAlert);
    float lvl = saturate(uLevel * 0.75) * 1.333; //0..1.6 → 0..1.33,保留过档余量

    //玻璃灯窗:双频火舌上翻,根部(窗下缘)由 tongue 自身分布承担
    float2 p = uv * uTexSize;
    float n1 = nrm(tex2D(uNoiseTex, float2(p.x * 0.050 + uSeed, p.y * 0.034 - uTime * 0.55)).r);
    float n2 = nrm(tex2D(uNoiseTex, float2(p.x * 0.110 + uSeed * 2.1, p.y * 0.066 - uTime * 1.15)).r);
    float tongue = saturate(n1 * 0.62 + n2 * 0.48);
    float3 flameCol = lerp(LAMP_WARM, LAMP_CORE, tongue);
    float3 glass = flameCol * (0.62 + 0.95 * tongue) * lvl * flick;
    glass = lerp(glass, FLASH_HOT * 1.20, uFlash * 0.80);

    //锈铁框架:保色受光 + 内焰透光染暖 + glint 巡缘扫光(只亮受光金属,慢周期)
    float3 metal = src.rgb * (0.84 + 0.55 * luma);
    metal += LAMP_WARM * nbGlow * 0.34 * lvl;
    metal += FLASH_HOT * uFlash * 0.30;
    float gPhase = frac(uTime * 0.09 + uSeed * 0.37);
    float gd = (uv.x + uv.y * 0.4) * 0.72 - gPhase * 1.9 + 0.35;
    float glint = exp2(-gd * gd * 90.0) * metalM * (0.20 + 0.80 * luma) * 0.45;

    //轮廓 rim:焰光勾金属外缘,下半加权(光源在灯内)
    float rim = edge * (0.22 + 0.34 * lvl) * (0.55 + 0.45 * uv.y);

    //合成:金属吃环境光,自发光件 75% 免疫
    float3 lit = lerp(vc.rgb, float3(1.0, 1.0, 1.0), 0.75);
    float3 col = metal * metalM * vc.rgb
        + glass * glassM * lit
        + (LAMP_WARM * rim + float3(1.0, 0.95, 0.85) * glint) * lit;

    return float4(col, 1.0) * (src.a * vc.a);
}

technique TechLamp {
    pass P0 {
        PixelShader = compile ps_3_0 PSLamp();
    }
}
