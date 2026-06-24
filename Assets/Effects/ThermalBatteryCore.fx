// ============================================================================
//ThermalBatteryCore.fx 热能电池熔核
//密封玻璃腔内的发光熔岩：随电量上涨的液面 + 上浮火星 + 内部热流 +
//液面亮线与白炽态 + 满电沸腾 + 腔壁余辉。世界空间绘制，AlphaBlend 预乘 alpha
//可合批：逐电池数据走顶点色(r=电量比例, g=近期充能活跃度)，其余为整批共享 uniform
//uChamberMin/uChamberMax=熔腔在电池本地坐标(0~1)中的矩形范围
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;
float2 uChamberMin;
float2 uChamberMax;

#define PI 3.14159265
#define TAU 6.28318530

float hash21(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//熔岩色阶：暗红→赤→橙→琥珀→白炽，与 ThermalBar.fx 同系
static const float3 COL_DEEP   = float3(0.240, 0.045, 0.020);   //熔体底部
static const float3 COL_MID    = float3(0.780, 0.220, 0.040);   //赤橙
static const float3 COL_HOT    = float3(0.990, 0.560, 0.130);   //橙琥珀
static const float3 COL_WHITE  = float3(1.000, 0.930, 0.640);   //白炽液面
static const float3 COL_EMBER  = float3(1.000, 0.780, 0.380);   //火星

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 res = uResolution;
    float2 p = coords * res;          //电池本地像素坐标
    float t = uTime;
    float fill = saturate(vertexColor.r);   //逐电池：电量比例
    float act = saturate(vertexColor.g);    //逐电池：充能活跃度

    //----- 熔腔圆角矩形 SDF -----
    float2 cMin = uChamberMin * res;
    float2 cMax = uChamberMax * res;
    float2 cCenter = (cMin + cMax) * 0.5;
    float2 cHalf = (cMax - cMin) * 0.5;
    float cornerR = min(2.6, min(cHalf.x, cHalf.y) * 0.6);
    float2 q = abs(p - cCenter) - (cHalf - cornerR);
    float chamberSDF = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - cornerR;

    //腔体之外、或空电池：完全透明（不填充，窗口透出后方墙体）
    if (chamberSDF > 1.5 || fill < 0.001) return float4(0, 0, 0, 0);
    float chamberMask = smoothstep(0.8, -0.8, chamberSDF);

    //----- 液面高度（底→顶随 fill 上升）-----
    float topY = cMin.y + 1.5;
    float botY = cMax.y - 1.5;
    float fillY = lerp(botY, topY, fill);
    //液面波动：基础正弦 + 噪声，活跃时更躁动
    float waveAmp = 0.6 + act * 1.1;
    float wave = sin(p.x * 0.9 + t * 3.4) * waveAmp
               + (valueNoise(float2(p.x * 0.35, t * 1.8)) - 0.5) * waveAmp * 1.6;
    float surfaceY = fillY + wave;
    //液体覆盖：液面以下且腔体以内
    float inFluid = smoothstep(-0.8, 1.0, p.y - surfaceY) * chamberMask;

    float3 col = float3(0, 0, 0);   //预乘色，加色辉光累加于此
    float a = inFluid;              //不透明度=液体覆盖；液面之上为 0 → 透明

    //----- 熔体本体 -----
    if (inFluid > 0.003) {
        //自液面向下加深的色带
        float depthT = saturate((p.y - surfaceY) / max(botY - surfaceY, 6.0));
        float3 fluid = lerp(COL_HOT, COL_MID, smoothstep(0.0, 0.7, depthT));
        fluid = lerp(fluid, COL_DEEP, smoothstep(0.55, 1.0, depthT));

        //内部热流扰动
        float flow = valueNoise(p * 0.16 + float2(t * 0.30, -t * 0.85));
        fluid *= 0.72 + flow * 0.55;

        //上浮火星：两列网格，速度随活跃度
        float emberSpeed = 0.45 + act * 1.3;
        for (int layer = 0; layer < 2; layer++) {
            float lf = (float)layer;
            float grid = 5.0 + lf * 3.5;
            float colId = floor((p.x - cMin.x) / grid);
            float s = hash21(float2(colId, lf * 23.0));
            float life = frac(s * 7.31 + t * emberSpeed * (0.5 + s * 0.7));
            float ey = lerp(botY - 1.0, surfaceY + 1.5, life);
            float ex = cMin.x + (colId + 0.5) * grid + sin(life * TAU + s * 9.0) * 1.6;
            float ed = length(p - float2(ex, ey));
            float ember = (1.0 - smoothstep(0.3, 1.1 + s * 0.7, ed)) * step(0.45, s);
            fluid += COL_EMBER * ember * 0.55 * sin(life * PI);
        }

        //焦散微光
        float caus = pow(saturate(1.0 - abs(valueNoise(p * 0.30 + float2(0.0, -t * 0.5))
            - valueNoise(p * 0.30 + float2(4.7, -t * 0.5 + 1.7))) * 4.5), 3.0);
        fluid += COL_WHITE * caus * 0.12;

        //满电底部白炽呼吸
        float hotPulse = smoothstep(0.6, 1.0, fill) * (0.5 + 0.5 * sin(t * 5.0));
        fluid += COL_HOT * hotPulse * depthT * 0.30;

        //内缘余辉：熔体贴近腔壁处更亮，凸显玻璃管体积
        float rimInner = 1.0 - smoothstep(0.0, 1.6, abs(chamberSDF + 1.0));
        fluid += lerp(COL_DEEP, COL_HOT, fill) * rimInner * 0.55;

        col += fluid * inFluid;   //预乘 alpha
    }

    //----- 液面亮线与辉光（加色，透出窗口）-----
    float surfDist = abs(p.y - surfaceY);
    float meniscus = exp(-surfDist * surfDist * 0.7) * chamberMask;
    col += COL_WHITE * meniscus * (0.9 + act * 0.6);
    col += COL_HOT * exp(-surfDist * 0.5) * chamberMask * 0.16;

    //满电沸腾：液面上方溅起的小火滴（加色）
    if (fill > 0.85) {
        float dropId = floor((p.x - cMin.x) / 3.5);
        float ds = hash21(float2(dropId, floor(t * 6.0)));
        float dLife = frac(t * 3.0 + ds * 5.0);
        float dy = surfaceY - dLife * 7.0;
        float dd = length(p - float2(cMin.x + (dropId + 0.5) * 3.5, dy));
        float drop = (1.0 - smoothstep(0.2, 1.0, dd)) * step(0.74, ds) * (1.0 - dLife) * chamberMask;
        col += COL_WHITE * drop * 0.9;
    }

    float fa = a * uAlpha;
    return float4(col * uAlpha, fa);   //顶点色仅承载逐电池数据，颜色全由程序生成
}

technique Technique1
{
    pass ThermalBatteryCorePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
