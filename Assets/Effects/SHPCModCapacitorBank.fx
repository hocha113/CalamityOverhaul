// ============================================================================
//SHPCModCapacitorBank.fx 储能阵列
//CapacitorCell：SpriteBatch 四边形电容柱（充能液面+极板+满格顶端电光泄漏）
//FeedArc：Trail 供能电弧（折跳滤波 + 沿弧推进的白热能量包头）
//噪声固定绑定 s1（s0 留给 SpriteBatch 精灵纹理，不采样）
// ============================================================================

float uTime;
float fadeAlpha;        //整体透明度 0~1
float3 coreColor;       //白热芯色
float3 glowColor;       //储能黄绿主色
float3 auraColor;       //暗绿外壳色

//───── CapacitorCell 专用 ─────
float fillLevel;        //0~1 充能液面高度
float cellFlash;        //点亮/放电/泄压瞬间闪光 0~1
float cellSeed;         //每格随机种子

//───── FeedArc 专用 ─────
float4x4 transformMatrix;
float arcSeed;          //每道电弧的随机种子
float pulseT;           //能量包头沿弧位置 0~1，>1 表示已送达

sampler noiseSamp : register(s1);

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//圆角矩形 SDF
float sdRoundBox(float2 p, float2 b, float r)
{
    float2 q = abs(p) - b + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

// ============================================================================
//电容柱：外壳框 + 底部向上充能液 + 堆叠极板 + 顶端电极与满格电光泄漏
// ============================================================================
float4 CellPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0; //-1..1，y 向下为正

    //主体柱身与顶端电极
    float sd = sdRoundBox(p - float2(0.0, 0.16), float2(0.42, 0.60), 0.14);
    float interior = 1.0 - smoothstep(-0.03, 0.03, sd);
    float edge = 1.0 - smoothstep(0.0, 0.07, abs(sd));
    float tsd = sdRoundBox(p - float2(0.0, -0.57), float2(0.11, 0.13), 0.05);
    float term = 1.0 - smoothstep(0.0, 0.06, abs(tsd));
    float termFill = 1.0 - smoothstep(-0.02, 0.04, tsd);

    //充能液面：底 0.70 → 顶 -0.38，充能中液面带噪声波动
    float wob = (tex2D(noiseSamp, float2(coords.x * 1.4 + cellSeed, uTime * 0.35 + cellSeed)).r - 0.5)
              * 0.06 * saturate(fillLevel * (1.0 - fillLevel) * 6.0);
    float surfaceY = lerp(0.70, -0.38, saturate(fillLevel)) + wob;
    float filled = interior * smoothstep(surfaceY - 0.02, surfaceY + 0.02, p.y);

    //内部能量对流与极板分隔线
    float en = tex2D(noiseSamp, float2(coords.x * 1.1 + cellSeed * 3.7, coords.y * 0.8 - uTime * 0.30)).r;
    float seg = frac((p.y + 1.0) * 2.4 + 0.25);
    float plate = 1.0 - 0.30 * smoothstep(0.42, 0.50, abs(seg - 0.5));

    //液面高光：仅充能途中显示
    float surf = exp(-abs(p.y - surfaceY) * 16.0) * interior
               * step(0.02, fillLevel) * (1.0 - step(0.995, fillLevel));

    //满格顶端电光泄漏：电极上方间歇性细电弧
    float full = step(0.995, fillLevel);
    float strobe = floor(uTime * 22.0);
    float leakGate = step(0.45, hash21(float2(strobe, cellSeed))) * full;
    float wander = (tex2D(noiseSamp, float2(p.y * 0.7 + strobe * 0.17, cellSeed * 1.3)).g - 0.5) * 0.5;
    float leakZone = (1.0 - smoothstep(-0.66, -0.58, p.y)) * smoothstep(-1.0, -0.90, p.y);
    float leak = (1.0 - smoothstep(0.0, 0.07, abs(p.x - wander))) * leakZone * leakGate;

    //满格呼吸脉动
    float breathe = full * (0.12 + 0.10 * sin(uTime * 2.6 + cellSeed * 9.0));

    float3 col = float3(0.0, 0.0, 0.0);
    //空壳基线：六个槽位在未充能时也保持可见
    col += auraColor * edge * 0.38;
    col += auraColor * interior * 0.10;
    //充能内容物
    float3 liquid = lerp(glowColor * 0.5, glowColor, en) * plate;
    liquid = lerp(liquid, coreColor, pow(saturate(en - 0.35), 2.0) * 0.8);
    col += liquid * filled * (0.85 + breathe);
    col += coreColor * surf * 0.9;
    //电极随填充变亮
    col += lerp(auraColor, coreColor, saturate(fillLevel + cellFlash)) * (term * 0.8 + termFill * 0.5 * full);
    //泄漏电光
    col += coreColor * leak * 1.2;
    //闪光：整柱爆亮 + 外圈溢光
    float burst = exp(-max(sd, 0.0) * 5.0);
    col += (coreColor * 0.85 + glowColor * 0.35) * cellFlash * (interior * 1.3 + burst * 0.8);

    float alpha = saturate(edge * 0.5 + interior * 0.15 + filled * 0.75 + surf * 0.8
                 + term * 0.5 + leak + cellFlash * (interior + burst * 0.6));
    alpha *= fadeAlpha;

    return float4(col * alpha, alpha) * vertexColor;
}

// ============================================================================
//供能电弧：Trail 条带，uv.x=0 电容端 → 1 球端
// ============================================================================
struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput ArcVS(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 ArcPS(PSInput input) : COLOR0
{
    float along = input.TexCoords.x;
    float cross_ = input.TexCoords.y;

    //折跳滤波路径：时间离散成放电帧，弧线阶跃式抖动
    float strobe = floor(uTime * 26.0);
    float n1 = tex2D(noiseSamp, float2(along * 2.6 + arcSeed, strobe * 0.09)).r;
    float n2 = tex2D(noiseSamp, float2(along * 6.5 - arcSeed * 1.3, strobe * 0.13 + 0.41)).g;
    float swing = sin(along * 3.14159);
    float path = 0.5 + (n1 - 0.5) * 0.46 * swing + (n2 - 0.5) * 0.20 * swing;
    float d = abs(cross_ - path);

    float core = 1.0 - smoothstep(0.0, 0.045, d);
    float glow = 1.0 - smoothstep(0.0, 0.20, d);

    //微分叉：放电帧随机闪现的细枝，自主弧岔出瞬灭
    float branchHash = hash21(float2(floor(along * 16.0), strobe + arcSeed * 11.0));
    float branchOn = step(0.74, branchHash);
    float branchPath = 0.5 + (branchHash - 0.5) * 1.2 * swing;
    float branch = (1.0 - smoothstep(0.0, 0.05, abs(cross_ - branchPath))) * branchOn * 0.6;

    //供能包头：白热能量团沿弧推进，抵达后熄灭
    float head = saturate(pulseT);
    float packet = exp(-abs(along - head) * 9.0) * 0.7 + exp(-abs(along - head) * 30.0) * 1.7;
    packet *= 1.0 - smoothstep(1.0, 1.15, pulseT);

    //流向明暗节：能量朝球端流动
    float flow = tex2D(noiseSamp, float2(along * 4.5 - uTime * 2.8 + arcSeed, cross_ * 1.4)).b;
    float dash = smoothstep(0.60, 0.80, flow) * glow * 0.4;

    float endFade = smoothstep(0.0, 0.05, along) * smoothstep(1.0, 0.95, along);
    float flicker = 0.75 + 0.25 * hash21(float2(strobe, arcSeed));

    float3 col = float3(0.0, 0.0, 0.0);
    col += coreColor * core * 1.1;
    col += glowColor * glow * 0.55;
    col += coreColor * branch * 0.85;
    col += coreColor * dash;
    col += (coreColor * 0.9 + glowColor * 0.4) * packet;

    float alpha = saturate(core + glow * 0.45 + branch * 0.7 + dash * 0.5 + packet * 0.9);
    alpha *= fadeAlpha * endFade * flicker;

    return float4(col * alpha, alpha) * input.Color;
}

technique CapacitorCell
{
    pass CellPass
    {
        PixelShader = compile ps_3_0 CellPS();
    }
}

technique FeedArc
{
    pass ArcPass
    {
        VertexShader = compile vs_2_0 ArcVS();
        PixelShader = compile ps_3_0 ArcPS();
    }
}
