namespace JungleBooze.Editor.Perf
{
    /// <summary>Static cost of one compiled shader stage (Metal source from Unity's compiler).</summary>
    public sealed class PerfShaderCost
    {
        public PerfShaderCost(string key, int lines, int scalarOps, int transcendentals, int samples, int shadowSamples, bool success, string message)
        {
            Key = key;
            Lines = lines;
            ScalarOps = scalarOps;
            Transcendentals = transcendentals;
            Samples = samples;
            ShadowSamples = shadowSamples;
            Success = success;
            Message = message;
        }

        public string Key { get; }

        /// <summary>Statements in the entry function body.</summary>
        public int Lines { get; }

        /// <summary>Scalar ALU operations: written components of every statement (a float3 op counts 3).</summary>
        public int ScalarOps { get; }

        /// <summary>exp2, log2, pow, sqrt, rsqrt, sin, cos, divisions (several ALU slots each on Apple GPUs).</summary>
        public int Transcendentals { get; }

        public int Samples { get; }

        public int ShadowSamples { get; }

        public bool Success { get; }

        public string Message { get; }

        /// <summary>
        /// ALU-slot estimate per invocation: scalar ops + 4 per transcendental (Apple GPUs run them at about a
        /// quarter rate). Texture work is reported separately.
        /// </summary>
        public int AluSlots => ScalarOps + Transcendentals * 4;
    }
}
