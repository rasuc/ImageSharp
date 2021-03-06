// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Enables the quantization of images to reduce the number of colors used in the image palette.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class QuantizeProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly IQuantizer quantizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuantizeProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="quantizer">The quantizer used to reduce the color palette.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public QuantizeProcessor(Configuration configuration, IQuantizer quantizer, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            Guard.NotNull(quantizer, nameof(quantizer));
            this.quantizer = quantizer;
        }

        /// <inheritdoc />
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(source.Bounds(), this.SourceRectangle);

            Configuration configuration = this.Configuration;
            using IFrameQuantizer<TPixel> frameQuantizer = this.quantizer.CreateFrameQuantizer<TPixel>(configuration);
            using QuantizedFrame<TPixel> quantized = frameQuantizer.QuantizeFrame(source, interest);

            var operation = new RowIntervalOperation(this.SourceRectangle, source, quantized);
            ParallelRowIterator.IterateRows(
                configuration,
                interest,
                in operation);
        }

        private readonly struct RowIntervalOperation : IRowIntervalOperation
        {
            private readonly Rectangle bounds;
            private readonly ImageFrame<TPixel> source;
            private readonly QuantizedFrame<TPixel> quantized;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowIntervalOperation(
                Rectangle bounds,
                ImageFrame<TPixel> source,
                QuantizedFrame<TPixel> quantized)
            {
                this.bounds = bounds;
                this.source = source;
                this.quantized = quantized;
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(in RowInterval rows)
            {
                ReadOnlySpan<byte> quantizedPixelSpan = this.quantized.GetPixelSpan();
                ReadOnlySpan<TPixel> paletteSpan = this.quantized.Palette.Span;
                int offsetY = this.bounds.Top;
                int offsetX = this.bounds.Left;
                int width = this.bounds.Width;

                for (int y = rows.Min; y < rows.Max; y++)
                {
                    Span<TPixel> row = this.source.GetPixelRowSpan(y);
                    int rowStart = (y - offsetY) * width;

                    for (int x = this.bounds.Left; x < this.bounds.Right; x++)
                    {
                        int i = rowStart + x - offsetX;
                        row[x] = paletteSpan[quantizedPixelSpan[i]];
                    }
                }
            }
        }
    }
}
