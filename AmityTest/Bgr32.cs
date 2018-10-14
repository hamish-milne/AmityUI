// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Pixel type containing three 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in blue, green, red order (least significant to most significant byte).
    /// <para>
    /// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct Bgr32 : IPixel<Bgr32>
    {
        /// <summary>
        /// The blue component.
        /// </summary>
        [FieldOffset(0)]
        public byte B;

        /// <summary>
        /// The green component.
        /// </summary>
        [FieldOffset(1)]
        public byte G;

        /// <summary>
        /// The red component.
        /// </summary>
        [FieldOffset(2)]
        public byte R;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgr24"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bgr32(byte r, byte g, byte b)
        {
            this.R = r;
            this.G = g;
            this.B = b;
        }

        /// <inheritdoc/>
        public PixelOperations<Bgr32> CreatePixelOperations() => new PixelOperations<Bgr32>();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Bgr32 other)
        {
            return this.R == other.R && this.G == other.G && this.B == other.B;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is Bgr24 other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            int hash = System.HashCode.Combine(this.R.GetHashCode(), this.G.GetHashCode());
            return System.HashCode.Combine(hash, this.B.GetHashCode());
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromArgb32(Argb32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromBgra32(Bgra32 source)
        {
            this.R = source.R;
            this.G = source.G;
            this.B = source.B;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromScaledVector4(Vector4 vector)
        {
            this.PackFromVector4(vector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToScaledVector4()
        {
            return this.ToVector4();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            Rgba32 rgba = default;
            rgba.PackFromVector4(vector);
            this.PackFromRgba32(rgba);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Rgba32(this.R, this.G, this.B, 255).ToVector4();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
            dest.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
            dest.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        public void ToBgr24(ref Bgr24 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
        }

        /// <inheritdoc/>
        public void ToBgra32(ref Bgra32 dest)
        {
            dest.R = this.R;
            dest.G = this.G;
            dest.B = this.B;
            dest.A = byte.MaxValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgb48(Rgb48 source)
        {
            this.R = (byte)(((source.R * 255) + 32895) >> 16);
            this.G = (byte)(((source.G * 255) + 32895) >> 16);
            this.B = (byte)(((source.B * 255) + 32895) >> 16);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb48(ref Rgb48 dest) => dest.PackFromScaledVector4(this.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba64(Rgba64 source)
        {
            this.R = (byte)(((source.R * 255) + 32895) >> 16);
            this.G = (byte)(((source.G * 255) + 32895) >> 16);
            this.B = (byte)(((source.B * 255) + 32895) >> 16);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba64(ref Rgba64 dest) => dest.PackFromScaledVector4(this.ToScaledVector4());

        /// <inheritdoc />
        public override string ToString()
        {
            return $"({this.B},{this.G},{this.R})";
        }
    }
}