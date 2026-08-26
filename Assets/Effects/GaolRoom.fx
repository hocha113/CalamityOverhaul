// ============================================================================
//GaolRoom.fx 深牢禁室房内氛围三件套（B1 独占，Weight 1.640 频段取用）
//TechWindow：玫瑰窗透光辉。圆窗盘面冷粉呼吸辉 + 中心过曝芯 + 铅条十字影，
//  uFigure>0 时窗后游走一团暗影（关过的东西还在动），uGlow 随房态压亮度。
//TechShaft：彩窗光柱。自窗斜落的楔形光带，铅条影随行、浮尘上飘，
//  uv.y 0=窗端 1=落端，落端羽化进祭坛背景带。
//TechGrate：封门能量栅。门洞竖排幽链栏 + 横链扣 + 上涌怨气脉冲，
//  uReveal 0~1 自顶向下织成（解封反放），蚀口缘挂热线。
//全部预乘输出进 AlphaBlend；无动态分支；cos(k*theta) k 整数，极角连续无缝。
//s1=PerlinNoise（实测值域 0.227~0.776，过 nrm）
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;
float uGlow;        //窗辉总强度 0~1（Cleared 房压到 0.25 档的余烬感）
float uFigure;      //窗后暗影存在感 0~1（Armed/Sealed 时 >0）
float uStrength;    //光柱强度 0~1
float uReveal;      //能量栅织成进度 0~1（0=无 1=封满；解封期回退）
float uPulse;       //能量栅受击/开战脉冲包络 0~1
float3 uColGlow;    //冷粉主辉（对齐 GaolPink 236,116,156）
float3 uColDeep;    //深粉暗缘（对齐 GaolPinkDeep 118,34,66）
float3 uColHot;     //过曝白芯

sampler noiseSamp : register(s1);

//PerlinNoise.r 实测值域 0.227~0.776
float nrm(float x) {
    return saturate((x - 0.227) / 0.549);
}

float noiseTex(float2 uv) {
    return nrm(tex2D(noiseSamp, uv).r);
}

struct VSInput {
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VSCommon(VSInput v) {
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

// ============================== TechWindow ==============================

float4 PSWindow(PSInput input) : COLOR0 {
    float2 p = (input.TexCoords - 0.5) * 2.0;
    float r = length(p);

    //窗盘：缘上软収，盘外快速归零
    float disc = smoothstep(1.0, 0.82, r);

    //呼吸：慢正弦 + 噪声细闪（狱火不稳定，火在喘）
    float breath = 0.82 + 0.18 * sin(uTime * 0.7 + uSeed * 11.0);
    float flick = 0.9 + 0.2 * noiseTex(float2(uTime * 0.11 + uSeed, 0.37));

    //铅条十字影：横竖两道压暗带（与字符画 m 十字对位）
    float barX = exp(-pow(p.x / 0.11, 2.0));
    float barY = exp(-pow(p.y / 0.11, 2.0));
    float lead = 1.0 - 0.5 * saturate(barX + barY);

    //花瓣调制：八瓣冷粉起伏（k 整数，theta 过 cos 连续；幅度压低防读成风车）
    float theta = atan2(p.y, p.x);
    float petal = 0.93 + 0.07 * cos(theta * 8.0 + uSeed * 5.0);

    //窗后暗影：噪声驱动的游走团（玻璃后有东西路过），芯亮一并吃掉
    float2 drift = float2(
        noiseTex(float2(uTime * 0.05 + uSeed * 3.0, 0.21)) - 0.5,
        noiseTex(float2(uTime * 0.043 + uSeed * 7.0, 0.68)) - 0.5) * 1.1;
    float figure = exp(-dot(p - drift, p - drift) / 0.09) * uFigure;
    float shade = 1.0 - 0.85 * figure;

    //合成：盘面辉 + 独立过曝芯（芯不吃铅影只吃暗影，火在铅条后面）+ 缘环压深
    float core = exp(-r * r * 4.0);
    float rim = disc * (1.0 - smoothstep(0.98, 0.80, r));
    float body = disc * breath * flick * petal * lead * shade;
    float3 col = uColGlow * body * 0.5
        + uColHot * core * breath * shade * 0.85
        + uColDeep * disc * (1.0 - body) * 0.30
        - uColDeep * rim * 0.18;

    float alpha = saturate(disc * (0.32 + 0.22 * body) + core * 0.35 * shade)
        * uGlow * input.Color.a;
    return float4(max(col, 0.0) * uGlow * input.Color.a, alpha);
}

// ============================== TechShaft ==============================

float4 PSShaft(PSInput input) : COLOR0 {
    float2 uv = input.TexCoords;

    //楔形横截面：向落端微张，缘软
    float spread = lerp(0.62, 1.0, uv.y);
    float cross_ = (uv.x - 0.5) * 2.0 / spread;
    float body = saturate(1.0 - abs(cross_));
    body = pow(body, 1.6);

    //铅条影随行：窗芯十字在光柱里投下一道暗缝
    float leadShadow = 1.0 - 0.4 * exp(-pow(cross_ / 0.16, 2.0));

    //沿程衰减：落端羽化 + 噪声絮化（光穿过灰尘的糙感）
    float fall = pow(1.0 - uv.y, 0.85);
    float grain = 0.8 + 0.35 * noiseTex(float2(uv.x * 1.7 + uSeed, uv.y * 0.9 - uTime * 0.04));

    //浮尘：高阈值噪声亮斑沿柱身缓慢上飘
    float mote = noiseTex(float2(uv.x * 6.0 + uSeed * 9.0, uv.y * 4.0 + uTime * 0.08));
    float dust = smoothstep(0.86, 0.97, mote) * body;

    float3 col = uColGlow * body * fall * grain * leadShadow * 0.6
        + uColHot * dust * 0.55;
    float alpha = saturate(body * fall * 0.42 + dust * 0.45) * uStrength * input.Color.a;
    return float4(col * uStrength * input.Color.a, alpha);
}

// ============================== TechGrate ==============================

float4 PSGrate(PSInput input) : COLOR0 {
    float2 uv = input.TexCoords;

    //三根竖栏（每 tile 列一根）：窄占空比，栏是栏、缝是缝
    float fx_ = abs(frac(uv.x * 3.0) - 0.5);
    float bar = smoothstep(0.17, 0.05, fx_);

    //横链扣：每 tile 行一道细档，只在竖栏间搭桥
    float fy = abs(frac(uv.y * 4.0) - 0.5);
    float link = smoothstep(0.09, 0.03, fy) * 0.5;

    //织成进度：自顶向下，噪声咬边 + 织造前沿热线
    float n = noiseTex(float2(uv.x * 2.3 + uSeed * 5.0, uv.y * 1.4 + uSeed));
    float edgeY = uv.y + (n - 0.5) * 0.14;
    float keep = smoothstep(uReveal + 0.04, uReveal - 0.05, edgeY);
    float front = exp(-abs(edgeY - uReveal) * 22.0) * step(0.001, uReveal) * step(uReveal, 0.999);

    //上涌怨气：沿栏身向上滚动的亮波
    float surge = noiseTex(float2(uv.x * 1.3 + uSeed * 3.0, uv.y * 2.2 + uTime * 0.55));
    float wave = 0.55 + 0.45 * surge;

    //受击/开战脉冲：整栅短促提亮（不整片刷白）
    float grid = saturate(bar + link);
    float body = grid * keep;
    float3 col = uColGlow * body * wave * (0.75 + uPulse * 0.5)
        + uColDeep * body * (1.0 - wave) * 0.5
        + uColHot * (front * grid * 0.8 + body * bar * uPulse * 0.35);

    float alpha = saturate(body * (0.60 + 0.25 * surge) + front * grid * 0.45) * input.Color.a;
    return float4(col * input.Color.a, alpha);
}

technique TechWindow {
    pass P0 {
        VertexShader = compile vs_3_0 VSCommon();
        PixelShader = compile ps_3_0 PSWindow();
    }
}

technique TechShaft {
    pass P0 {
        VertexShader = compile vs_3_0 VSCommon();
        PixelShader = compile ps_3_0 PSShaft();
    }
}

technique TechGrate {
    pass P0 {
        VertexShader = compile vs_3_0 VSCommon();
        PixelShader = compile ps_3_0 PSGrate();
    }
}
