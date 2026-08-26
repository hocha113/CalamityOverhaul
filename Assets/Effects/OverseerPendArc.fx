// ============================================================================
//OverseerPendArc.fx 钟摆弧光（P3 摆刑的达速弧带）
//材质=铸铁劈风的摩擦弧，非对称截面是"光带≠发光香蕉"的分水岭：
//外弧缘（v=0）白热锐线（exp2 紧高斯），向内（v=1）渐散并被噪声撕成风压细缕。
//沿弧 u：1=摆锤头（最亮）→ 0=尾（噪声蚀断成离散缕）。
//uSpeed 门控整体强度与尾长（摆到弧端慢速时自动熄灭，与伤害窗同源可读）。
//顶点色 A=C# 逐顶点沿弧衰减。
//只进 Additive 批：rgb 不预乘、a 携带包络。
//极角审计：strip UV 已是弧长参数化，无 atan2。s1=PerlinNoise（值域 0.22~0.776）
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float4x4 transformMatrix;  //世界→屏幕（C# 喂 GetTransfromMatrix，顶点吃世界坐标）

float uTime;    //秒
float uSpeed;   //摆速归一 0..1（达速窗=1）
float uSeed;    //实例相位

static const float3 ARC_IRON = float3(0.360, 0.230, 0.150);  //暗铁外鞘
static const float3 ARC_ORANGE = float3(1.000, 0.520, 0.160);  //炉橙中带
static const float3 ARC_HOT = float3(1.000, 0.880, 0.620);  //白热前缘

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

PSInput VSArc(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PSArc(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float u = uv.x;   //0 尾 → 1 头
    float v = uv.y;   //0 外弧缘 → 1 内弧缘

    //====== 尾部噪声蚀断：头实尾缕 ======
    float en = noiseTex(float2(u * 2.6 - uTime * 1.4 + uSeed, v * 1.3 + uSeed * 2.0));
    float erode = smoothstep(0.05, 0.62, u + (en - 0.5) * 0.55);

    //====== 非对称截面：外缘锐线 + 内侧散带 ======
    float edge = exp2(-v * v * 260.0);                       //外弧缘白热锐线
    float bodyFall = exp2(-v * v * 9.0) * (1.0 - v * 0.55);  //向内渐散

    //风压细缕：沿弧向拉长的高频纹，向尾滚动
    float sn = noiseTex(float2(u * 5.5 - uTime * 2.6 + uSeed * 3.0, v * 2.2 + uSeed));
    float streak = smoothstep(0.44, 0.72, sn) * bodyFall * 0.6;

    //====== 沿弧亮度：头端聚能 ======
    float headK = smoothstep(0.25, 1.0, u);
    float lum = (edge * (0.6 + headK * 0.9) + bodyFall * 0.30 + streak) * erode;

    //====== 速度门控 ======
    lum *= smoothstep(0.15, 0.75, uSpeed);

    //====== 色阶：暗铁 → 炉橙 → 白热（白热只在头端外缘）======
    float3 col = lerp(ARC_IRON, ARC_ORANGE, saturate(lum * 2.0));
    col = lerp(col, ARC_HOT, saturate(edge * headK * uSpeed * 1.3 - 0.25));

    float alpha = saturate(lum) * vc.a;
    //Additive 契约：rgb 不预乘，a 携带包络
    return float4(col * vc.rgb, alpha);
}

technique TechArc {
    pass P0 {
        VertexShader = compile vs_3_0 VSArc();
        PixelShader = compile ps_3_0 PSArc();
    }
}
