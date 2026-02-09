Shader "Custom/HologramHack"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0.3, 0.1, 0.9, 0.6)
        _EdgeColor ("Edge Glow Color", Color) = (0.6, 0.2, 1.0, 1.0)
        _ScanlineColor ("Scanline Color", Color) = (0.1, 0.9, 1.0, 0.3)
        _GlitchColor ("Glitch Color", Color) = (1.0, 0.2, 0.6, 0.8)

        _ScanlineSpeed ("Scanline Speed", Float) = 3.0
        _ScanlineDensity ("Scanline Density", Float) = 40.0
        _ScanlineWidth ("Scanline Width", Range(0, 1)) = 0.4

        _GlitchIntensity ("Glitch Intensity", Range(0, 0.3)) = 0.06
        _GlitchSpeed ("Glitch Speed", Float) = 3.0

        _FresnelPower ("Fresnel Power", Range(0.5, 5)) = 2.0
        _FlickerSpeed ("Flicker Speed", Float) = 12.0
        _Alpha ("Base Alpha", Range(0, 1)) = 0.45

        _DataStreamDensity ("Data Stream Density", Float) = 15.0
        _DataStreamSpeed ("Data Stream Speed", Float) = 6.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "HologramHack"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir     : TEXCOORD2;
                float2 uv          : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _EdgeColor;
                float4 _ScanlineColor;
                float4 _GlitchColor;
                float  _ScanlineSpeed;
                float  _ScanlineDensity;
                float  _ScanlineWidth;
                float  _GlitchIntensity;
                float  _GlitchSpeed;
                float  _FresnelPower;
                float  _FlickerSpeed;
                float  _Alpha;
                float  _DataStreamDensity;
                float  _DataStreamSpeed;
            CBUFFER_END

            // Simple hash for glitch randomness
            float hash(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            float hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posOS = input.positionOS.xyz;

                // ── Glitch: horizontal displacement ──
                float time = _Time.y * _GlitchSpeed;
                float glitchLine = floor(posOS.y * 8.0);
                float glitchRandom = hash(glitchLine + floor(time * 3.0));

                // Only glitch some lines, some of the time
                float glitchActive = step(0.85, glitchRandom);
                float glitchOffset = (hash(glitchLine + floor(time * 7.0)) - 0.5) * 2.0;
                posOS.x += glitchOffset * _GlitchIntensity * glitchActive;

                // Also slight z jitter
                posOS.z += (hash(glitchLine + floor(time * 5.0)) - 0.5) * _GlitchIntensity * 0.5 * glitchActive;

                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS  = posInputs.positionCS;
                output.worldPos    = posInputs.positionWS;
                output.worldNormal = normInputs.normalWS;
                output.viewDir     = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.uv          = input.uv;
                output.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;

                // ── Fresnel / edge glow ──
                float3 normal = normalize(input.worldNormal);
                float3 viewDir = normalize(input.viewDir);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);

                // ── Scanlines (horizontal lines scrolling up) ──
                float scanY = input.worldPos.y * _ScanlineDensity + time * _ScanlineSpeed;
                float scanline = step(_ScanlineWidth, frac(scanY));
                // Thicker highlight scanlines every ~8 lines
                float bigScan = step(0.92, frac(scanY * 0.125 + time * 0.3));

                // ── Data stream effect (vertical columns of "data") ──
                float dataX = input.worldPos.x * _DataStreamDensity;
                float dataScroll = input.worldPos.y * 5.0 + time * _DataStreamSpeed;
                float dataColumn = step(0.7, frac(dataX));
                float dataBit = step(0.5, hash2(float2(floor(dataX), floor(dataScroll))));
                float dataStream = dataColumn * dataBit * 0.3;

                // ── Flicker ──
                float flicker = 0.85 + 0.15 * sin(time * _FlickerSpeed);
                float hardFlicker = step(0.97, hash(floor(time * 20.0))) * 0.5;

                // ── Compose color ──
                // Base: main hologram color
                float3 baseColor = _MainColor.rgb;

                // Edge glow
                float3 edgeGlow = _EdgeColor.rgb * fresnel * 2.0;

                // Scanline tint
                float3 scanColor = _ScanlineColor.rgb * bigScan * 0.6;

                // Data stream tint  
                float3 dataColor = _ScanlineColor.rgb * dataStream;

                // Glitch color flash (random bursts)
                float glitchFlash = step(0.92, hash(floor(time * 15.0) + input.worldPos.y * 3.0));
                float3 glitchCol = _GlitchColor.rgb * glitchFlash * 0.4;

                // Final color
                float3 finalColor = baseColor + edgeGlow + scanColor + dataColor + glitchCol;

                // ── Alpha ──
                float alpha = _Alpha;
                alpha *= scanline;                      // scanline cutout
                alpha += fresnel * 0.5;                 // edges always visible
                alpha += bigScan * 0.3;                 // big scanlines brighter
                alpha += dataStream;                    // data stream adds opacity
                alpha *= flicker;                       // overall flicker
                alpha -= hardFlicker;                   // occasional hard flicker dip
                alpha = saturate(alpha);

                // Fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    // Fallback for non-URP
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float3 worldPos  : TEXCOORD0;
                float3 worldNorm : TEXCOORD1;
                float3 viewDir   : TEXCOORD2;
                float2 uv        : TEXCOORD3;
            };

            float4 _MainColor;
            float4 _EdgeColor;
            float4 _ScanlineColor;
            float4 _GlitchColor;
            float  _ScanlineSpeed;
            float  _ScanlineDensity;
            float  _ScanlineWidth;
            float  _GlitchIntensity;
            float  _GlitchSpeed;
            float  _FresnelPower;
            float  _FlickerSpeed;
            float  _Alpha;
            float  _DataStreamDensity;
            float  _DataStreamSpeed;

            float hash_f(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            float hash2_f(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            v2f vert(appdata v)
            {
                v2f o;

                float3 posOS = v.vertex.xyz;

                float time = _Time.y * _GlitchSpeed;
                float glitchLine = floor(posOS.y * 8.0);
                float glitchRandom = hash_f(glitchLine + floor(time * 3.0));
                float glitchActive = step(0.85, glitchRandom);
                float glitchOffset = (hash_f(glitchLine + floor(time * 7.0)) - 0.5) * 2.0;
                posOS.x += glitchOffset * _GlitchIntensity * glitchActive;
                posOS.z += (hash_f(glitchLine + floor(time * 5.0)) - 0.5) * _GlitchIntensity * 0.5 * glitchActive;

                v.vertex.xyz = posOS;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNorm = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y;

                float3 normal = normalize(i.worldNorm);
                float3 viewDir = normalize(i.viewDir);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);

                float scanY = i.worldPos.y * _ScanlineDensity + time * _ScanlineSpeed;
                float scanline = step(_ScanlineWidth, frac(scanY));
                float bigScan = step(0.92, frac(scanY * 0.125 + time * 0.3));

                float dataX = i.worldPos.x * _DataStreamDensity;
                float dataScroll = i.worldPos.y * 5.0 + time * _DataStreamSpeed;
                float dataColumn = step(0.7, frac(dataX));
                float dataBit = step(0.5, hash2_f(float2(floor(dataX), floor(dataScroll))));
                float dataStream = dataColumn * dataBit * 0.3;

                float flicker = 0.85 + 0.15 * sin(time * _FlickerSpeed);
                float hardFlicker = step(0.97, hash_f(floor(time * 20.0))) * 0.5;

                float3 baseColor = _MainColor.rgb;
                float3 edgeGlow = _EdgeColor.rgb * fresnel * 2.0;
                float3 scanColor = _ScanlineColor.rgb * bigScan * 0.6;
                float3 dataColor = _ScanlineColor.rgb * dataStream;
                float glitchFlash = step(0.92, hash_f(floor(time * 15.0) + i.worldPos.y * 3.0));
                float3 glitchCol = _GlitchColor.rgb * glitchFlash * 0.4;

                float3 finalColor = baseColor + edgeGlow + scanColor + dataColor + glitchCol;

                float alpha = _Alpha;
                alpha *= scanline;
                alpha += fresnel * 0.5;
                alpha += bigScan * 0.3;
                alpha += dataStream;
                alpha *= flicker;
                alpha -= hardFlicker;
                alpha = saturate(alpha);

                return fixed4(finalColor, alpha);
            }
            ENDCG
        }
    }

    Fallback "Transparent/Diffuse"
}
