namespace ebuild.api.Compiler
{
    /// <summary>
    /// CPU extension level identifiers used to specialize code generation for specific
    /// instruction set extensions or microarchitectures.
    /// </summary>
    public enum CPUExtensions
    {
        /// <summary>
        /// No special CPU extensions targeted; emit generic code.
        /// </summary>
        Default,

        /// <summary>
        /// 32-bit x86 (IA-32) target.
        /// </summary>
        IA32,

        /// <summary>
        /// Streaming SIMD Extensions (SSE) family - early SSE support.
        /// </summary>
        SSE,

        /// <summary>
        /// SSE2 instruction set support.
        /// </summary>
        SSE2,

        /// <summary>
        /// SSE4.2 instruction set support.
        /// </summary>
        SSE4_2,

        /// <summary>
        /// AVX (Advanced Vector Extensions) support.
        /// </summary>
        AVX,

        /// <summary>
        /// AVX2 support with wider integer vector instructions.
        /// </summary>
        AVX2,

        /// <summary>
        /// AVX-512 family support for 512-bit wide vector operations.
        /// </summary>
        AVX512,

        /// <summary>
        /// AVX1.0 (labelled AVX10_1 in this enum) - first AVX release/variant.
        /// </summary>
        AVX10_1,

        /// <summary>
        /// ARMv7-A with V7VE extensions (Vector Floating Point and DSP extensions).
        /// </summary>
        ARMv7VE,

        /// <summary>
        /// ARM VFP version 4 floating-point support.
        /// </summary>
        VFPv4,

        // ARMv8 family - progressive feature levels.
        /// <summary>ARMv8.0 baseline AArch64 support.</summary>
        armv8_0,
        /// <summary>ARMv8.1 incremental extension.</summary>
        armv8_1,
        /// <summary>ARMv8.2 incremental extension.</summary>
        armv8_2,
        /// <summary>ARMv8.3 incremental extension.</summary>
        armv8_3,
        /// <summary>ARMv8.4 incremental extension.</summary>
        armv8_4,
        /// <summary>ARMv8.5 incremental extension.</summary>
        armv8_5,
        /// <summary>ARMv8.6 incremental extension.</summary>
        armv8_6,
        /// <summary>ARMv8.7 incremental extension.</summary>
        armv8_7,
        /// <summary>ARMv8.8 incremental extension.</summary>
        armv8_8,
        /// <summary>ARMv8.9 incremental extension.</summary>
        armv8_9,

        // ARMv9 family
        /// <summary>ARMv9.0 baseline extensions.</summary>
        armv9_0,
        /// <summary>ARMv9.1 incremental extension.</summary>
        armv9_1,
        /// <summary>ARMv9.2 incremental extension.</summary>
        armv9_2,
        /// <summary>ARMv9.3 incremental extension.</summary>
        armv9_3,
        /// <summary>ARMv9.4 incremental extension.</summary>
        armv9_4
    }
}
