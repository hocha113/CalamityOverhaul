// ============================================================================
//BRelicIonArm.fx 过载指令核心：离子全息机械臂
//s0 = 臂/骨节精灵（真 alpha NPC 贴图），消费端在 Additive 批里画
//（加色批源因子 = SourceAlpha，A 通道携带强度包络，禁 A=0）
//纯 ALU 哈希，无绑定噪声；全笛卡尔无极角
// ============================================================================

sampler uImage0 : register(s0);

float uTime;            //秒
float3 uColor;          //离子青主色
float3 uSecondaryColor; //白热高光
float uGhost;           //成形度 0~1：沿帧 v 轴自上而下显影，>=1 完全成形
float uSeed;            //逐臂错相种子
float uAlpha;           //总包络（生灭）
float2 uTexel;          //1/整张贴图尺寸，边缘检测步长
float uFlicker;         //全息不稳定闪烁强度 0~1（窗口尾声升高）
float2 uUvRow;          //帧区域：x=帧 v 起点 y=帧 v 跨度（NPC 精灵表逐帧钳制，整图传 (0,1)）

float hash11(float p)
{
    return frac(sin(p * 127.1 + 74.7) * 43758.5453);
}

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 src = tex2D(uImage0, uv);

    //帧内归一化 v（多帧精灵表下 uv.y 只跨帧区域）
    float vNorm = saturate((uv.y - uUvRow.x) / max(uUvRow.y, 0.0001));

    //亮度承结构：钢的明暗细节全保
    float lum = dot(src.rgb, float3(0.3, 0.59, 0.11));

    //=== 成形线：沿帧 v 轴扫描显影，列哈希毛边 ===
    float edgeN = hash11(floor(uv.x * 24.0) + uSeed * 91.0) * 0.16;
    float form = smoothstep(uGhost + edgeN, uGhost + edgeN - 0.22, vNorm);
    form = max(form, step(1.0, uGhost));

    //=== 全息扫描带：高频带缓慢下滚 ===
    float scan = 0.82 + 0.18 * sin(vNorm * 90.0 - uTime * 8.0 + uSeed * 17.0);

    //=== 不稳定闪烁：逐帧哈希跳变，uFlicker 门控 ===
    float flick = 1.0 - uFlicker * 0.45 * step(0.55, hash11(floor(uTime * 30.0) + uSeed * 53.0));

    //=== 轮廓缘光：4 邻域 alpha 落差，纵向采样钳在帧界内防串帧 ===
    float yLo = uUvRow.x + uTexel.y;
    float yHi = uUvRow.x + uUvRow.y - uTexel.y;
    float aL = tex2D(uImage0, uv - float2(uTexel.x, 0)).a;
    float aR = tex2D(uImage0, uv + float2(uTexel.x, 0)).a;
    float aU = tex2D(uImage0, float2(uv.x, max(uv.y - uTexel.y, yLo))).a;
    float aD = tex2D(uImage0, float2(uv.x, min(uv.y + uTexel.y, yHi))).a;
    float rim = saturate(src.a * 4.0 - (aL + aR + aU + aD));

    //=== 成形前沿亮线（完全成形后自然消失）===
    float front = saturate(1.0 - abs(vNorm - uGhost) * 9.0) * (1.0 - step(1.0, uGhost));

    float3 col = uColor * (0.35 + lum * 0.95) * scan;
    col += uSecondaryColor * rim * 0.8;
    col += uSecondaryColor * front * 0.9;
    col *= vertexColor.rgb;

    float intensity = src.a * form * flick * uAlpha * vertexColor.a;
    return float4(col * intensity, saturate(intensity));
}

technique Technique1
{
    pass BRelicIonArmPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
