// ============================================================================
//OverseerIronCast.fx 铸造监工的铸铁材质统一层
//以原版贴图（Cog/石拳/火轮/浇包）为底做锈橙铸铁重染：
//源明度经伽马拉开后映入铁灰蓝三阶（乘色贴纸的根治），
//叠炉锈浮斑（双频噪声阈值，L6 炉锈橙）+ 铸痕点蚀 +
//体内受热辉光（低频热区，uHeat 驱动：战斗恒温呼吸/硬直挣扎/死亡冷却归零）
//+ 轮廓热 rim（alpha 4 邻域，热机件的缝隙漏光）。
//结构承 ScrapForm 的 alpha 轮廓 + 帧区域钳制方案；
//噪声三次采样，门控走 step/lerp，无动态分支。
//s0=贴图（帧区域由 uUvRect 归一）s1=PerlinNoise（实测值域 0.22~0.776，阈值已按此定标）
//预乘输出，AlphaBlend 批
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uSeed;        //实例随机相位
float uRust;        //0=新铁 1=锈透（监工常驻 ~0.55，吊臂蛰伏 ~0.85）
float uHeat;        //受热辉光 0..1（死亡冷却归零）
float4 uUvRect;     //贴图帧区域 xy=偏移 zw=尺寸（纹理 uv 空间）
float2 uTexel;      //一像素的 uv 尺寸，轮廓检测用
float uAspect;      //帧宽/帧高，噪声采样防拉伸

//====== 铸铁调色（对位 FoundryOverseer 色板）======
static const float3 IRON_SHADOW = float3(0.135, 0.140, 0.170);  //缝隙深铁
static const float3 IRON_MID    = float3(0.420, 0.430, 0.480);  //铸铁中间调
static const float3 IRON_LIGHT  = float3(0.700, 0.710, 0.760);  //受光铁面
static const float3 RUST_WARM   = float3(0.660, 0.380, 0.170);  //炉锈橙浮斑
static const float3 RUST_DEEP   = float3(0.310, 0.150, 0.075);  //缝隙深锈
static const float3 HEAT_GLOW   = float3(1.000, 0.560, 0.180);  //受热炉橙
static const float3 HEAT_CORE   = float3(1.000, 0.820, 0.470);  //热芯熔金

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//采样贴图 alpha，钳在帧区域内防串帧
float frameAlpha(float2 uv) {
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    return tex2D(uImage0, clamp(uv, lo, hi)).a;
}

float4 PSIronCast(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uImage0, uv);
    float srcA = src.a;

    //帧内归一坐标；噪声采样用等比坐标防拉伸
    float2 luv = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001);
    float2 nuv = float2(luv.x * uAspect, luv.y);

    //====== 铸铁三阶重调色：源明度伽马拉开再映射，保留原贴图的形体明暗 ======
    float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));
    float g = pow(saturate(lum * 1.12), 1.45);
    float3 body = lerp(IRON_SHADOW, IRON_MID, saturate(g * 2.0));
    body = lerp(body, IRON_LIGHT, saturate(g * 2.0 - 1.0));

    //====== 炉锈浮斑：低频定块、高频咬缝（noise 实测中值 ~0.5，窗取 0.52~0.72 档）======
    float rn = noiseTex(nuv * 1.9 + uSeed);
    float rn2 = noiseTex(nuv * 4.6 + uSeed * 2.3);
    float patch = smoothstep(0.60 - uRust * 0.22, 0.74 - uRust * 0.16, rn);
    body = lerp(body, RUST_WARM * (0.55 + g * 0.65), patch * 0.58 * uRust);
    //高频点蚀：细碎深锈嵌进斑内
    float pit = step(0.66, rn2) * patch;
    body = lerp(body, RUST_DEEP * (0.5 + rn * 0.5), pit * 0.5 * uRust);

    //====== 体内受热辉光：低频热区自帧心向外衰减（炉芯在毂心）======
    float2 fromCore = (luv - float2(0.5, 0.5)) * float2(uAspect, 1.0);
    float coreDist = length(fromCore);
    float heatField = exp(-coreDist * coreDist * 5.5);
    //热区噪声撕成不均匀的透火缝
    float hn = noiseTex(nuv * 3.2 + float2(uSeed, uTime * 0.06));
    float heatVein = smoothstep(0.42, 0.70, hn);
    float heat = heatField * heatVein * uHeat;
    //暗处透光更明显（铸铁厚薄不均，薄处先透）
    heat *= 1.0 - g * 0.55;
    float3 heatCol = lerp(HEAT_GLOW, HEAT_CORE, saturate(heat * 2.2));

    //====== 轮廓热 rim：缝隙漏光，热度呼吸 ======
    float aL = frameAlpha(uv - float2(uTexel.x, 0.0));
    float aR = frameAlpha(uv + float2(uTexel.x, 0.0));
    float aU = frameAlpha(uv - float2(0.0, uTexel.y));
    float aD = frameAlpha(uv + float2(0.0, uTexel.y));
    float minN = min(min(aL, aR), min(aU, aD));
    float rim = saturate((srcA - minN) * 2.0);
    float breathe = 0.72 + 0.28 * sin(uTime * 3.6 + uSeed * 3.0 + luv.y * 5.0);
    float3 rimGlow = HEAT_GLOW * rim * uHeat * breathe * 0.85;

    //====== 合成（预乘输出：热光走加色项，不吃 alpha）======
    float aOut = srcA * vc.a;
    float3 outRgb = body * vc.rgb * aOut + (heatCol * heat * 1.35 + rimGlow) * aOut;
    return float4(outRgb, aOut);
}

technique TechIronCast {
    pass P0 {
        PixelShader = compile ps_3_0 PSIronCast();
    }
}
