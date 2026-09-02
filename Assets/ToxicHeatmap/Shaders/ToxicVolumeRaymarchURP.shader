Shader "Custom/ToxicVolumeRaymarchURP"
{
    Properties
    {
        _VolumeTex ("Volume Texture", 3D) = "" {}
        _Steps ("Raymarch Steps", Int) = 64
        _DensityMultiplier ("Density Multiplier", Float) = 2
        _MinAlpha ("Minimum Visibility (0 = clean air invisible)", Range(0,1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Front
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ToxicVolumePass"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE3D(_VolumeTex);
            SAMPLER(sampler_VolumeTex);

            int _Steps;
            float _DensityMultiplier;
            float _MinAlpha;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 camPosOS   : TEXCOORD1; // object-space camera position
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;

                // Object-space camera position — computed per-vertex, constant across the mesh,
                // used in the fragment shader to build a proper entry point for the ray.
                OUT.camPosOS = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                return OUT;
            }

            // 5-stop heatmap gradient: blue -> cyan -> green -> yellow -> red
            half3 HeatmapColor(float t)
            {
                t = saturate(t);
                half3 c0 = half3(0.0, 0.0, 1.0); // blue    (low)
                half3 c1 = half3(0.0, 1.0, 1.0); // cyan
                half3 c2 = half3(0.0, 1.0, 0.0); // green
                half3 c3 = half3(1.0, 1.0, 0.0); // yellow
                half3 c4 = half3(1.0, 0.0, 0.0); // red     (high)

                float seg = t * 4.0;
                if (seg < 1.0) return lerp(c0, c1, seg);
                if (seg < 2.0) return lerp(c1, c2, seg - 1.0);
                if (seg < 3.0) return lerp(c2, c3, seg - 2.0);
                return lerp(c3, c4, saturate(seg - 3.0));
            }

            // Ray-box (slab method) intersection in object space. Box spans -0.5..0.5
            // on every axis — matches Unity's built-in cube mesh, which is what
            // ToxicVolumeRenderer now builds, and matches the box bounds
            // (boundsMin/boundsMax) HeatmapVolumeBaker actually samples toxicity in.
            // Using a box instead of a sphere means corners and walls of a
            // rectangular room are no longer clipped away.
            bool IntersectBox(float3 ro, float3 rd, out float tNear, out float tFar)
            {
                float3 invD = 1.0 / rd;
                float3 t0 = (-0.5 - ro) * invD;
                float3 t1 = ( 0.5 - ro) * invD;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                tNear = max(max(tmin.x, tmin.y), tmin.z);
                tFar  = min(min(tmax.x, tmax.y), tmax.z);
                return tFar >= max(tNear, 0.0);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 rayDir = normalize(IN.positionOS - IN.camPosOS);

                float tNear, tFar;
                bool hit = IntersectBox(IN.camPosOS, rayDir, tNear, tFar);
                if (!hit || tFar <= 0.0) discard;

                // If the camera is inside the box, tNear will be negative — clip the
                // marching start to the camera itself instead of an out-of-bounds point.
                // This is what makes it work correctly as the player walks INTO the room,
                // not just when viewing it from outside.
                float tStart = max(tNear, 0.0);

                float marchDist = tFar - tStart;
                float stepSize = marchDist / _Steps;

                float3 pos = IN.camPosOS + rayDir * tStart + 0.5; // object space -0.5..0.5 -> texture 0..1
                float3 stepVec = rayDir * stepSize;

                half4 accum = half4(0,0,0,0);

                [loop]
                for (int s = 0; s < _Steps; s++)
                {
                    if (any(pos < 0) || any(pos > 1)) break;

                    half4 samp = SAMPLE_TEXTURE3D_LOD(_VolumeTex, sampler_VolumeTex, pos, 0);
                    float normalizedToxicity = samp.r; // 0..1, baked from your Excel values

                    // Always some visibility ("_MinAlpha") so clean air still reads as blue,
                    // rather than disappearing like the old threshold-cutoff version did.
                    float density = lerp(_MinAlpha, 1.0, normalizedToxicity) * _DensityMultiplier;

                    half3 col = HeatmapColor(normalizedToxicity);
                    float a = saturate(density * stepSize);
                    accum.rgb += (1 - accum.a) * a * col;
                    accum.a += (1 - accum.a) * a;

                    pos += stepVec;
                    if (accum.a > 0.98) break;
                }

                return accum;
            }
            ENDHLSL
        }
    }
}
