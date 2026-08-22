// ============================================================================
//ScrapForm.fx 废钢统帅的废钢材质：以原版贴图为底，
//叠锈蚀斑块（噪声阈值的暖锈两频）+ 油渍流层（缓慢下淌的暗色油光）
//+ 焊缝热光（alpha 轮廓一线焊橙，uHeat 驱动，过载拉满）。
//uRust 随血量抬升，机体一路打一路烂。
//结构承 KikasaItemForm 的 alpha 轮廓 + 帧区域钳制方案；
//噪声只取三次采样，门控走 step/lerp，无动态分支。
//s0=贴图（帧区域由 uUvRect 归一）s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uSeed;        //实例随机相位
float uRust;        //0=原装 1=锈透（随血量抬升）
float uSheen;       //油渍流层强度
float uHeat;        //焊缝热光 0..1
float4 uUvRect;     //贴图帧区域 xy=偏移 zw=尺寸（纹理 uv 空间）
float2 uTexel;      //一像素的 uv 尺寸，轮廓检测用
float uAspect;      //帧宽/帧高，噪声采样防拉伸

//====== 废钢调色 ======
static const float3 RUST_WARM = float3(0.560, 0.330, 0.190);  //浮锈暖棕
static const float3 RUST_DEEP = float3(0.300, 0.140, 0.080);  //缝隙深锈
static const float3 OIL_DARK  = float3(0.100, 0.085, 0.070);  //油渍暗色
static const float3 WELD_HOT  = float3(1.000, 0.590, 0.230);  //焊缝热橙

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

//采样贴图 alpha，钳在帧区域内防串帧
float frameAlpha(float2 uv) {
    float2 lo = uUvRect.xy + uTexel * 0.5;
    float2 hi = uUvRect.xy + uUvRect.zw - uTexel * 0.5;
    return tex2D(uImage0, clamp(uv, lo, hi)).a;
}

float4 PSScrapForm(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float4 src = tex2D(uImage0, uv);
    float srcA = src.a;

    //帧内归一坐标；噪声采样用等比坐标防拉伸
    float2 luv = (uv - uUvRect.xy) / max(uUvRect.zw, 0.0001);
    float2 nuv = float2(luv.x * uAspect, luv.y);

    //====== 锈蚀斑块：低频定块、高频咬缝 ======
    float rn = noiseTex(nuv * 1.7 + uSeed);
    float rn2 = noiseTex(nuv * 4.3 + uSeed * 2.1);
    //斑块遮罩：uRust 越高阈值越松，锈面越大
    float patch = smoothstep(0.62 - uRust * 0.30, 0.80 - uRust * 0.22, rn);
    float3 body = lerp(src.rgb, src.rgb * RUST_WARM * 2.0, patch * 0.62 * uRust);
    //高频咬缝：细碎深锈点蚀
    float pit = step(0.76, rn2) * patch;
    body = lerp(body, RUST_DEEP * (0.5 + rn * 0.5), pit * 0.55 * uRust);

    //====== 油渍流层：缓慢下淌的暗色油光，偶有湿亮 ======
    float on = noiseTex(nuv * 1.1 + float2(uSeed * 0.7, -uTime * 0.045 + uSeed));
    float oilBand = pow(saturate(on * 1.1), 5.0);
    body = lerp(body, body * 0.55 + OIL_DARK * 0.45, oilBand * uSheen);
    //油面湿亮一线
    float gloss = pow(saturate(on * 1.25), 12.0);
    body += WELD_HOT * gloss * 0.10 * uSheen;

    //====== 焊缝热光：剪影轮廓一线焊橙，热度呼吸 ======
    float aL = frameAlpha(uv - float2(uTexel.x, 0.0));
    float aR = frameAlpha(uv + float2(uTexel.x, 0.0));
    float aU = frameAlpha(uv - float2(0.0, uTexel.y));
    float aD = frameAlpha(uv + float2(0.0, uTexel.y));
    float minN = min(min(aL, aR), min(aU, aD));
    float rim = saturate((srcA - minN) * 2.2);
    float breathe = 0.75 + 0.25 * sin(uTime * 5.0 + uSeed * 3.0 + luv.y * 6.0);
    float3 weld = WELD_HOT * rim * uHeat * breathe;

    //====== 合成（预乘输出：热光走加色项，不吃 alpha） ======
    float aOut = srcA * vc.a;
    return float4(body * vc.rgb * aOut + weld * aOut, aOut);
}

technique TechScrapForm {
    pass P0 {
        PixelShader = compile ps_3_0 PSScrapForm();
    }
}
