// ============================================================================
//ScrapMagnetField.fx 废钢统帅的磁力场：
//整数倍角力线（sin(Nθ+径向相位) 的螺旋收束纹）+ 向心奔涌的环脉
//+ 刚体旋转坐标里的尘屑闪点。uPull 翻转流向（收束/外掷），
//uStrength 主控强度，边缘与中心双向软融。
//极角审计：theta 的唯一消费是 sin(N*theta + f(r))，N=6 整数，跨 ±π 连续；
//噪声采样全走刚体旋转的直角坐标，不吃 theta。
//只在 BlendState.Additive 批内绘制（rgb 不预乘、a 携带包络）。
//噪声 2 次采样。s0=白像素 s1=PerlinNoise
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float uTime;        //秒
float uSeed;        //实例相位
float uStrength;    //0..1 主控
float uPull;        //+1 向心收束 / -1 离心外掷
float3 uColorHot;   //焊橙热色
float3 uColorDeep;  //锈红深色

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

float4 PSMagnetField(float4 vc : COLOR0, float2 uv : TEXCOORD0) : COLOR0 {
    float2 p = uv - 0.5;
    float r = length(p) * 2.0;          //0=中心 1=边缘
    float theta = atan2(p.y, p.x);

    //====== 力线：六臂螺旋，径向相位卷入，随时间向心流动 ======
    float spiral = theta * 6.0 + r * 7.5 - uTime * uPull * 5.2 + uSeed;
    float lines = pow(abs(sin(spiral)), 6.0);

    //====== 环脉：向心奔涌的同心波 ======
    float rings = pow(abs(sin(r * 14.0 - uTime * uPull * 7.0 + uSeed * 2.0)), 10.0);

    //====== 尘屑闪点：刚体旋转坐标里的噪声阈值 ======
    float cs = cos(uTime * uPull * 0.8);
    float sn = sin(uTime * uPull * 0.8);
    float2 rp = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
    float dust = step(0.78, noiseTex(rp * 1.6 + uSeed)) * step(0.2, r);

    //====== 包络：边缘软融 + 中心留洞（统帅本体在洞里） ======
    float env = smoothstep(1.0, 0.62, r) * smoothstep(0.10, 0.30, r);

    float body = lines * 0.55 + rings * 0.35 + dust * 0.5;
    float3 color = lerp(uColorDeep, uColorHot, saturate(lines + rings * 0.5))
        * (0.6 + 0.4 * saturate(1.0 - r));
    float a = saturate(body * env * uStrength) * vc.a;
    return float4(color, a);
}

technique TechMagnetField {
    pass P0 {
        PixelShader = compile ps_3_0 PSMagnetField();
    }
}
